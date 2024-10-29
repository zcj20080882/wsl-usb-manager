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
    public ObservableCollection<USBDeviceInfoModel> DevicesItems { get => _devicesItems; set => SetProperty(ref _devicesItems, value); }
    public USBDeviceInfoModel? SelectedDevice { get => _selectedDevice; set => SetProperty(ref _selectedDevice, value); }

    public AutoAttachViewModel(MainWindowViewModel mainDataContext)
    {
        MainDataContext = mainDataContext;
        foreach (var device in Sysconfig.AutoAttachDeviceList)
        {
            DevicesItems.Add(new USBDeviceInfoModel(device, mainDataContext));
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
                MainDataContext.ShowNotification($"{device.Description} is removed from auto attach list.");
            }

            if (DevicesItems != null && DevicesItems.Contains(device))
            {
                DevicesItems.Remove(device);
            }
        }
    }

    public void UpdateDevices(List<USBDevice> devices)
    {
        _lastSelectedDevice = SelectedDevice;
        ObservableCollection<USBDeviceInfoModel> new_list = [];
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
                MainDataContext.ShowNotification($"{device.Description} has been add to auto attach list.");
            }
            new_list.Add(new USBDeviceInfoModel(device, MainDataContext));
        }
        DevicesItems = new_list;
        if (_lastSelectedDevice != null && DevicesItems != null && DevicesItems.Any())
        {
            SelectedDevice = DevicesItems?.FirstOrDefault(di => string.Equals(di.HardwareId, _lastSelectedDevice.HardwareId, StringComparison.CurrentCultureIgnoreCase)) ?? DevicesItems?.First();
        }
    }
    private ObservableCollection<USBDeviceInfoModel> _devicesItems = [];
    private USBDeviceInfoModel? _selectedDevice;
    private USBDeviceInfoModel? _lastSelectedDevice;
    private MainWindowViewModel MainDataContext { get; }
    private readonly SystemConfig Sysconfig = App.GetSysConfig();
}
