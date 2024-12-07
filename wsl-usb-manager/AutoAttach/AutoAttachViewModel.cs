/******************************************************************************
* SPDX-License-Identifier: MIT
* Project: wsl-usb-manager
* Class: AutoAttachViewModel.cs
* NameSpace: wsl_usb_manager.AutoAttach
* Author: Chuckie
* copyright: Copyright (c) Chuckie, 2024
* Description:
* Create Date: 2024/10/17 20:31
******************************************************************************/
using System.Collections.ObjectModel;
using wsl_usb_manager.Controller;
using wsl_usb_manager.Domain;
using wsl_usb_manager.Settings;

namespace wsl_usb_manager.AutoAttach;

public class AutoAttachViewModel : ViewModelBase
{
    private ObservableCollection<USBDeviceInfoModel> _devicesItems = [];
    private USBDeviceInfoModel? _selectedDevice;
    private USBDeviceInfoModel? _lastSelectedDevice;
    private readonly SystemConfig Sysconfig = App.GetSysConfig();

    public ObservableCollection<USBDeviceInfoModel> DevicesItems { get => _devicesItems; set => SetProperty(ref _devicesItems, value); }
    public USBDeviceInfoModel? SelectedDevice { get => _selectedDevice; set => SetProperty(ref _selectedDevice, value); }

    public AutoAttachViewModel()
    {
        foreach (var device in Sysconfig.AutoAttachDeviceList)
        {
            DevicesItems.Add(new USBDeviceInfoModel(device));
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

            if (DevicesItems != null && DevicesItems.Contains(device))
            {
                DevicesItems.Remove(device);
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
            foreach (var item in DevicesItems)
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
        DevicesItems = new_list;
        if (_lastSelectedDevice != null && DevicesItems != null && DevicesItems.Any())
        {
            SelectedDevice = DevicesItems?.FirstOrDefault(di => string.Equals(di.HardwareId, _lastSelectedDevice.HardwareId, StringComparison.CurrentCultureIgnoreCase)) ?? DevicesItems?.First();
        }
    }
    
}
