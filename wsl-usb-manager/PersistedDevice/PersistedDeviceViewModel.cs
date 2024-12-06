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
using log4net;
using System.Collections.ObjectModel;
using wsl_usb_manager.Controller;
using wsl_usb_manager.Domain;

namespace wsl_usb_manager.PersistedDevice;

public class PersistedDeviceViewModel : ViewModelBase
{
    public ObservableCollection<USBDeviceInfoModel>? DevicesItems { get => _devicesItems; set => SetProperty(ref _devicesItems, value); }
    public USBDeviceInfoModel? SelectedDevice { get => _selectedDevice; set => SetProperty(ref _selectedDevice, value); }

    private ObservableCollection<USBDeviceInfoModel>? _devicesItems;
    private USBDeviceInfoModel? _selectedDevice;
    private USBDeviceInfoModel? _lastSelectedDevice;
    private static readonly ILog log = LogManager.GetLogger(typeof(PersistedDeviceViewModel));

    public async Task UpdateDevices()
    {
        _lastSelectedDevice = SelectedDevice;
        ObservableCollection<USBDeviceInfoModel> DeviceList = [];
        (ExitCode ret, string err, List<USBDevice>? persistedList) = await USBIPD.ListPersistedDevices();
        if (ret != ExitCode.Success)
        {
            log.Error($"Failed to list persisted devices: {err}");
            return;
        }
        if (persistedList == null)
            return;
        foreach (var device in persistedList)
        {
            USBDeviceInfoModel item = new(device);
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
