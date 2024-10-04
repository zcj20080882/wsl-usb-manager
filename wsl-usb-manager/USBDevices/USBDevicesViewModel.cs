/******************************************************************************
* SPDX-License-Identifier: MIT
* Project: wsl-usb-manager
* Class: USBDevicesViewModel.cs
* NameSpace: wsl_usb_manager.USBDevices
* Author: Chuckie
* copyright: Copyright (c) Chuckie, 2024
* Description:
* Create Date: 2024/10/2 18:58
******************************************************************************/
using System.Collections.ObjectModel;
using System.Security.Permissions;
using System.Windows;
using System.Windows.Input;
using wsl_usb_manager.Controller;
using wsl_usb_manager.Domain;
using MessageBox = System.Windows.MessageBox;

namespace wsl_usb_manager.USBDevices;

public class USBDevicesViewModel : ViewModelBase
{
    public ICommand RefreshCommand { get; }
    public bool ShowRefreshProgress { get => _showRefreshProgresss; set => SetProperty(ref _showRefreshProgresss, value); }
    public bool PageEnabled { get => _pageEnabled; set => SetProperty(ref _pageEnabled, value); }
    public bool MenuBindEnabled { get => _menuBindEnabled; set => SetProperty(ref _menuBindEnabled, value); }
    public bool MenuAttachEnabled { get => _menuAttachEnabled; set => SetProperty(ref _menuAttachEnabled, value); }
    public bool MenuDetachEnabled { get => _menuDetachEnabled; set => SetProperty(ref _menuDetachEnabled, value); }
    public bool MenuUnbindEnabled { get => _menuUnBindEnabled; set => SetProperty(ref _menuUnBindEnabled, value); }
    public ObservableCollection<USBDeviceInfoModel>? USBDevicesItems { get => _usbDevicesItems; set => SetProperty(ref _usbDevicesItems, value);}
    public USBDeviceInfoModel? SelectedDevice { get => _selectedDevice; set => SetProperty(ref _selectedDevice, value); }

    public USBDevicesViewModel()
    {
        RefreshCommand = new CommandImplementations(RefeshDevicesCommand);
        ShowRefreshProgress = false;
        _usbDevicesItems = CreateData(0);
// SelectedDevice = _usbDevicesItems?.FirstOrDefault();
        PageEnabled = true;
        MenuBindEnabled = true;
        MenuAttachEnabled = true;
    }

    private bool _showRefreshProgresss;
    private bool _pageEnabled;
    private bool _menuBindEnabled;
    private bool _menuAttachEnabled;
    private bool _menuUnBindEnabled;
    private bool _menuDetachEnabled;

    private ObservableCollection<USBDeviceInfoModel>? _usbDevicesItems;
    private USBDeviceInfoModel? _selectedDevice;
    private USBDeviceInfoModel? _lastSelectedDevice;

    private void BeforeRefresh()
    {
        _lastSelectedDevice= SelectedDevice;
        ShowRefreshProgress = true;
        PageEnabled = false;
    }

    private void AfterRefresh()
    {
        PageEnabled = true;
        ShowRefreshProgress = false;
        if (_lastSelectedDevice != null)
        {
            SelectedDevice = _lastSelectedDevice;
        }
    }

    private static ObservableCollection<USBDeviceInfoModel> CreateData(int retryCount)
    {
        ObservableCollection<USBDeviceInfoModel> DeviceList = [];
        (int ret, string errormsg, List<USBDevicesInfo> infolist) = USBIPD.GetAllUSBDevices();
        while (retryCount > 0 && ret == 0)
        {
            (ret, errormsg, infolist) = USBIPD.GetAllUSBDevices();
            Task.Delay(100);
            retryCount--;
        }
        if (ret != 0 || infolist == null)
        {
            MessageBox.Show(errormsg, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return DeviceList;
        }

        foreach (var device in infolist)
        {
            USBDeviceInfoModel item = new(device);
            if (item.IsConnected)
                DeviceList.Add(item);
        }
        return DeviceList;
    }

    #region Commands
    /// <summary>
    /// Refresh Button command
    /// </summary>
    /// <param name="obj"></param>
    private async void RefeshDevicesCommand(object? obj)
    {
        BeforeRefresh();
        await Task.Run(() =>
        {
            USBDevicesItems = CreateData(2);
        });
        AfterRefresh();
    }

    public async void BindDevice(USBDeviceInfoModel device,bool bind)
    {
        BeforeRefresh();
        await Task.Run(() =>
        {
            if (!string.IsNullOrEmpty(device.HardwareId))
            {
                CommandResult result = bind ? USBIPD.BindDevice(device.HardwareId, device.IsForced) : USBIPD.UnbindDevice(device.HardwareId);
                if (result.ExitCode != 0 && result.StandardError.Length > 0)
                {
                    MessageBox.Show($"Failed to {(bind ? "bind" : "unbind")} {device.HardwareId}: {result.StandardError}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                USBDevicesItems = CreateData(3);
            }
        });
        AfterRefresh();
    }

    public async void AttachDevice(USBDeviceInfoModel device, bool attach)
    {
        BeforeRefresh();
        await Task.Run(() =>
        {
            if(!string.IsNullOrEmpty(device.HardwareId))
            {
                CommandResult result = attach ? USBIPD.AttachDeviceLocal(device.HardwareId) : USBIPD.DetachDevice(device.HardwareId);
                if (result.ExitCode != 0 && result.StandardError.Length > 0)
                {
                    MessageBox.Show($"Failed to {(attach ? "attach" : "detach")} {device.HardwareId}: {result.StandardError}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                USBDevicesItems = CreateData(3);
            }
        });
        AfterRefresh();
    }
    #endregion
}
