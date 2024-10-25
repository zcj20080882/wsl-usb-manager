/******************************************************************************
* SPDX-License-Identifier: MIT
* Project: wsl-usb-manager
* Class: MainWindowViewModelController.cs
* NameSpace: wsl_usb_manager
* Author: Chuckie
* copyright: Copyright (c) Chuckie, 2024
* Description:
* Create Date: 2024/10/17 20:28
******************************************************************************/
using Newtonsoft.Json.Bson;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Threading;
using wsl_usb_manager.AutoAttach;
using wsl_usb_manager.Controller;
using wsl_usb_manager.Domain;
using wsl_usb_manager.PersistedDevice;
using wsl_usb_manager.Settings;
using wsl_usb_manager.USBDevices;
using MessageBox = System.Windows.MessageBox;

namespace wsl_usb_manager;

public partial class MainWindowViewModel : ViewModelBase
{
    private List<USBDevice> ConnectedDeviceList { get; set; } = [];
    private List<USBDevice> PersistedDeviceList { get; set; } = [];

    public bool IsDeviceInConnectedList(string hardwareID) => ConnectedDeviceList.Any(d => d.HardwareId.Equals(hardwareID, StringComparison.OrdinalIgnoreCase));
    public bool IsDeviceInPersistedList(string hardwareID) => PersistedDeviceList.Any(d => d.HardwareId.Equals(hardwareID, StringComparison.OrdinalIgnoreCase));
   
    public List<USBDevice> GetConnectedDeviceList() => ConnectedDeviceList;
    public List<USBDevice> GetPersistedDeviceList() => PersistedDeviceList;

    public async Task<(string, USBDevice?,List<USBDevice>?)> GetAllDeviceAndFilter(string hardwareID)
    {
        (ExitCode _, string err_msg, List<USBDevice>? new_list) = await USBIPD.GetAllUSBDevices();
        USBDevice? changedDev = new_list?.FirstOrDefault(d => d.HardwareId.Equals(hardwareID, StringComparison.OrdinalIgnoreCase));
        if (changedDev == null && new_list != null) {
            err_msg = $"Cannot get the device with hardware ID \"{hardwareID}\"";
        }
        return (err_msg, changedDev, new_list);
    }

    public async Task USBEventProcess(USBEventArgs e)
    {
        string hardwareid = e.HardwareID ?? "";
        string? name = e.Name;
        string msg = "";
        
        log.Debug($"USB {hardwareid} {(e.IsConnected ? "connected" : "disconnected")}");

        if (Sysconfig.IsInFilterDeviceList(hardwareid))
        {
            log.Debug($"Device {e.Name}({hardwareid}) is in filter list, ignore it.");
            return;
        }

        (string err_msg, USBDevice ? changedDev, List<USBDevice>? new_list) = await GetAllDeviceAndFilter(hardwareid);

        List<USBDevice> curConnectedList = ConnectedDeviceList.Where(d => !Sysconfig.IsInFilterDeviceList(d.HardwareId)).ToList();
        if (new_list == null)
        {
            log.Error($"Failed to get USB devices: {err_msg}");
            return;
        }

        if (e.IsConnected)
        {
            if (changedDev == null)
            {
                log.Error($"Cannot get info from USBIPD for device {e.Name}({e.HardwareID})"); 
                return;
            }

            name ??= changedDev.Description;
            hardwareid = changedDev.HardwareId;
            if (Sysconfig.IsInAutoAttachDeviceList(changedDev))
            {
                await AutoAttachDevices(changedDev);
            }
            else
            {
                await UpdateUSBDevices(new_list);
            }
            msg = $"\"{name}({hardwareid})\" is connected to {(changedDev.IsAttached ? "WSL" : "Windows")}.";
            ShowNotification(msg);
        }
        else
        {
            if (changedDev != null)
            {
                name ??= changedDev.Description;
                hardwareid = changedDev.HardwareId;
                if (changedDev.IsConnected)
                {
                    msg = $"\"{name}({hardwareid})\" is connected to {(changedDev.IsAttached ? "WSL" : "Windows")}.";
                }
                else
                {
                    msg = $"\"{name}({hardwareid})\" is disconnected from Windows.";
                }
            }
            else
            {
                msg = $"\"{e.Name}({e.HardwareID})\" is disconnected from Windows.";
            }
            log.Debug("Update USB devices list....");
            await UpdateUSBDevices(new_list);
            ShowNotification(msg);
        }
    }

    public async Task<bool> BindDevice(USBDevice device)
    {
        string err_msg = "";

        try
        {
            DisableWindow();
            (ExitCode _, err_msg) = await USBIPD.BindDevice(device.HardwareId, device.IsForced);            
        }
        finally
        {
            await Task.Delay(device.IsForced ? 1000 : 200);
            (_, USBDevice? readback, List<USBDevice>? new_list) = await GetAllDeviceAndFilter(device.HardwareId);
            if (readback != null && readback.IsBound)
                err_msg = "";
            await UpdateUSBDevices(new_list);
            EnableWindow();
        }

        if (err_msg.Length > 0)
        {
            ShowErrorMessage(err_msg);
            return false;
        }
        return true;
    }

    public async Task UnbindDevice(USBDevice? device)
    {
        string err_msg = "";

        try
        {
            DisableWindow();
            (ExitCode _, err_msg) = await USBIPD.UnbindDevice(device?.HardwareId);
        }
        finally
        {
            await UpdateUSBDevices(null);
            EnableWindow();
        }

        if (err_msg.Length > 0)
            ShowErrorMessage(err_msg);
    }

    public async Task<bool> AttachDevice(USBDevice device)
    {
        string err_msg = "";
        ExitCode ret = ExitCode.Failure;
        ApplicationConfig cfg = App.GetAppConfig();
        try
        {
            DisableWindow();
            (ret, err_msg) = await Wsl.Attach(device.BusId, NetworkCardInfo.GetIPAddress(cfg.ForwardNetCard));
        }
        finally
        {
            if (ret != ExitCode.Success)
            {
                await Task.Delay(500);
            }
            (_, USBDevice? readback, List<USBDevice>? new_list) = await GetAllDeviceAndFilter(device.HardwareId);
            if (readback != null && readback.IsAttached)
                err_msg = "";
            await UpdateUSBDevices(new_list);
            EnableWindow();
        }

        if (err_msg.Length > 0)
        {
            ShowErrorMessage(err_msg);
            return false;
        }
        return true;
    }

    public async Task DetachDevice(USBDevice ?device)
    {
        string err_msg = "";
        try
        {
            DisableWindow();
            (ExitCode _, err_msg) = await USBIPD.DetachDevice(device?.HardwareId);
        }
        finally
        {
            await UpdateUSBDevices(null);
            EnableWindow();
        }

        if (err_msg.Length > 0)
            ShowErrorMessage(err_msg);
    }

    public async Task UpdateUSBDevices(List<USBDevice>? updateList)
    {
        string errormsg = "Unknown error.";

        try
        {
            DisableWindow();
            if(updateList == null || updateList.Count == 0)
                (_, errormsg, updateList) = await USBIPD.GetAllUSBDevices();
            if (updateList == null)
            {
                ShowErrorMessage($"Cannot obtain USB devices info:{errormsg}.");
                return;
            }
            ConnectedDeviceList = updateList.Where(d => d.IsConnected).ToList();
            PersistedDeviceList = updateList.Where(d => (d.IsBound && !d.IsConnected)).ToList();
            foreach (var item in BodyItems)
            {
                if (item.Content is USBDevicesView usbView && usbView.DataContext is USBDevicesViewModel usbDT)
                {
                    usbDT.UpdateDevices(ConnectedDeviceList);
                }
                else if (item.Content is PersistedDeviceView persistedView &&
                    persistedView.DataContext is PersistedDeviceViewModel persistedDT)
                {
                    persistedDT.UpdateDevices(PersistedDeviceList);
                }
                else if(item.Content is AutoAttachView autoAttachView &&
                    autoAttachView.DataContext is AutoAttachViewModel autoAttachDT)
                {
                    autoAttachDT.UpdateDevices(App.GetSysConfig().AutoAttachDeviceList);
                }
            }
        }
        finally
        {
            EnableWindow();
        }
    }

    public async Task AutoAttachDevices(USBDevice? dev)
    {
        if(dev == null || !Sysconfig.IsInAutoAttachDeviceList(dev))
        {
            return;
        }
        if(dev == null)
        {
            log.Error($"Cannot get device info for auto attach device");
            return;
        }
        if (!dev.IsConnected)
        {
            log.Error($"The device {dev.HardwareId} is not connected!");
            return;
        }
            
        if (!dev.IsBound)
        {
            if (!await BindDevice(dev))
            {
                await Task.Run(() => Task.Delay(1000));
            }
            if (!await BindDevice(dev))
            {
                log.Error($"Failed to bind {dev.Description}({dev.HardwareId}).");
                return;
            }
        }
        if (!dev.IsAttached)
        {
            if (!await AttachDevice(dev))
            {
                await Task.Run(() => Task.Delay(1000));
            }
            if (!await AttachDevice(dev))
            {
                log.Error($"Failed to attach {dev.Description}({dev.HardwareId}).");
                return;
            }
        }
    }
}
