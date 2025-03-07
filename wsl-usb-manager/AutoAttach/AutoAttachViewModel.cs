/******************************************************************************
* SPDX-License-Identifier: MIT
* Project: wsl-usb-manager
* Class: AutoAttachViewModel.cs
* NameSpace: wsl_usb_manager.AutoAttach
* Author: Chuckie
* copyright: Copyright (c) Chuckie, 2025
* Description:
* Create Date: 2024/10/17 20:31
******************************************************************************/
using System.Collections.ObjectModel;
using wsl_usb_manager.Domain;
using wsl_usb_manager.Settings;
using wsl_usb_manager.USBIPD;

namespace wsl_usb_manager.AutoAttach;

public class AutoAttachViewModel : ViewModelBase
{
    private ObservableCollection<USBDeviceInfoModel> _deviceInfoModules = [];
    private USBDeviceInfoModel? _selectedDevice;
    private USBDeviceInfoModel? _lastSelectedDevice;
    private readonly SystemConfig Sysconfig = App.GetSysConfig();

    public ObservableCollection<USBDeviceInfoModel> DeviceInfoModules { get => _deviceInfoModules; set => SetProperty(ref _deviceInfoModules, value); }
    public USBDeviceInfoModel? SelectedDevice { get => _selectedDevice; set => SetProperty(ref _selectedDevice, value); }

    public AutoAttachViewModel()
    {
        foreach (var device in Sysconfig.AutoAttachDeviceList)
        {
            DeviceInfoModules.Add(new USBDeviceInfoModel(device));
        }
    }

    public void RemoveDeviceFromAutoAttachProfile(List<USBDeviceInfoModel> devices)
    {
        foreach (var device in devices)
        {
            if (Sysconfig.IsInAutoAttachDeviceList(device.Device))
            {
                Sysconfig.RemoveFromAutoAttachDeviceList(device.Device);
                App.SaveConfig();
                NotifyService.ShowNotification($"{device.Description} is removed from auto attach list.");
            }

            if (DeviceInfoModules != null && DeviceInfoModules.Contains(device))
            {
                DeviceInfoModules.Remove(device);
            }
        }
    }

    public void UpdateDevices()
    {
        _lastSelectedDevice = SelectedDevice;
        ObservableCollection<USBDeviceInfoModel> new_list = [];
        List<USBDevice> devices = App.GetSysConfig().AutoAttachDeviceList;
        foreach (var device in devices)
        {
            bool exist = false;
            foreach (var item in DeviceInfoModules)
            {
                if (item.HardwareId == device.HardwareId)
                {
                    exist = true;
                    break;
                }
            }
            if (!exist)
            {
                NotifyService.ShowNotification($"{device.Description} has been add to auto attach list.");
            }
            new_list.Add(new USBDeviceInfoModel(device));
        }
        DeviceInfoModules = new_list;
        if (_lastSelectedDevice != null && DeviceInfoModules != null && DeviceInfoModules.Any())
        {
            SelectedDevice = DeviceInfoModules?.FirstOrDefault(di => string.Equals(di.HardwareId, _lastSelectedDevice.HardwareId, StringComparison.CurrentCultureIgnoreCase)) ?? DeviceInfoModules?.First();
        }
    }
    
}
