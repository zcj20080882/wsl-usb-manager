/******************************************************************************
* SPDX-License-Identifier: MIT
* Project: wsl-usb-manager
* Class: PersistedDeviceViewModel.cs
* NameSpace: wsl_usb_manager.PersistedDevice
* Author: Chuckie
* copyright: Copyright (c) Chuckie, 2024
* Description:
* Create Date: 2024/10/17 20:22
******************************************************************************/
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using wsl_usb_manager.Controller;
using wsl_usb_manager.Domain;
using MessageBox = System.Windows.MessageBox;

namespace wsl_usb_manager.PersistedDevice;

public class PersistedDeviceViewModel(MainWindowViewModel mainDataContext) : ViewModelBase
{
    public ObservableCollection<USBDeviceInfoModel>? DevicesItems { get => _devicesItems; set => SetProperty(ref _devicesItems, value); }
    public USBDeviceInfoModel? SelectedDevice { get => _selectedDevice; set => SetProperty(ref _selectedDevice, value); }

    private ObservableCollection<USBDeviceInfoModel>? _devicesItems;
    private USBDeviceInfoModel? _selectedDevice;
    private USBDeviceInfoModel? _lastSelectedDevice;
    private MainWindowViewModel MainDataContext { get; } = mainDataContext;

    public void UpdateDevices(List<USBDevice> UpdaterList)
    {
        _lastSelectedDevice = SelectedDevice;
        ObservableCollection<USBDeviceInfoModel> DeviceList = [];

        foreach (var device in UpdaterList)
        {
            USBDeviceInfoModel item = new(device, MainDataContext);
            if (!item.IsConnected)
                DeviceList.Add(item);
        }
        DevicesItems = DeviceList;
        if (_lastSelectedDevice != null && DevicesItems != null && DevicesItems.Any())
        {
            SelectedDevice = DevicesItems?.FirstOrDefault(di => string.Equals(di.HardwareId, _lastSelectedDevice.HardwareId, StringComparison.CurrentCultureIgnoreCase)) ?? DevicesItems?.First();
        }
    }
}
