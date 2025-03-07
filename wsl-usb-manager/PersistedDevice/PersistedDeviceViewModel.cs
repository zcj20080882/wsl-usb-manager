/******************************************************************************
* SPDX-License-Identifier: MIT
* Project: wsl-usb-manager
* Class: PersistedDeviceViewModel.cs
* NameSpace: wsl_usb_manager.PersistedDevice
* Author: Chuckie
* copyright: Copyright (c) Chuckie, 2025
* Description:
* Create Date: 2024/10/17 20:22
******************************************************************************/
using log4net;
using System.Collections.ObjectModel;
using wsl_usb_manager.Domain;
using wsl_usb_manager.USBIPD;

namespace wsl_usb_manager.PersistedDevice;

public class PersistedDeviceViewModel : ViewModelBase
{
    public ObservableCollection<USBDeviceInfoModel>? DeviceInfoModules { get => _deviceInfoModules; set => SetProperty(ref _deviceInfoModules, value); }
    public USBDeviceInfoModel? SelectedDevice { get => _selectedDevice; set => SetProperty(ref _selectedDevice, value); }

    private ObservableCollection<USBDeviceInfoModel>? _deviceInfoModules;
    private USBDeviceInfoModel? _selectedDevice;
    private USBDeviceInfoModel? _lastSelectedDevice;
    private static readonly ILog log = LogManager.GetLogger(typeof(PersistedDeviceViewModel));

    public async Task UpdateDevices()
    {
        _lastSelectedDevice = SelectedDevice;
        ObservableCollection<USBDeviceInfoModel> deviceInfoModules = [];
        (ErrorCode ErrCode, string ErrMsg, List<USBDevice>? DevicesList) = await USBIPDWin.ListPersistedDevices();
        if (ErrCode != ErrorCode.Success)
        {
            log.Error($"Failed to list persisted devices: {ErrMsg}");
            NotifyService.ShowUSBIPDError(ErrCode, ErrMsg, null);
            return;
        }
        if (DevicesList == null || DevicesList.Count < 1)
            return;

        foreach (var device in DevicesList)
        {
            USBDeviceInfoModel item = new(device);
            if (!item.IsConnected)
                deviceInfoModules.Add(item);
        }
        DeviceInfoModules = deviceInfoModules;
        if (_lastSelectedDevice != null)
        {
            SelectedDevice = DeviceInfoModules?.FirstOrDefault(di => string.Equals(di.HardwareId, _lastSelectedDevice.HardwareId, 
                StringComparison.CurrentCultureIgnoreCase)) ?? DeviceInfoModules?.First();
        }
    }
}
