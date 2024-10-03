/******************************************************************************
* SPDX-License-Identifier: MIT
* Project: wsl-usb-manager
* Class: PersistedDeviceModel.cs
* NameSpace: wsl_usb_manager.PersistedDevice
* Author: Chuckie
* copyright: Copyright (c) Chuckie, 2024
* Description:
* Create Date: 2024/10/3 20:29
******************************************************************************/
using System.Collections.ObjectModel;
using System.Windows.Input;
using wsl_usb_manager.Controller;
using wsl_usb_manager.Domain;
using wsl_usb_manager.USBDevices;

namespace wsl_usb_manager.PersistedDevice;

public class PersistedDeviceViewModel : ViewModelBase
{
    public ICommand RefeshCommand { get; }
    public ObservableCollection<USBDeviceInfoModel>? DevicesItems { get => _devicesItems; set => SetProperty(ref _devicesItems, value); }
    public USBDeviceInfoModel? SelectedDevice { get => _selectedDevice; set => SetProperty(ref _selectedDevice, value); }

    public PersistedDeviceViewModel()
    {
        RefeshCommand = new CommandImplementations(RefeshDevicesCommand);
        _devicesItems = CreateData();
        SelectedDevice = _devicesItems?.FirstOrDefault();
    }

    private ObservableCollection<USBDeviceInfoModel>? _devicesItems;
    private USBDeviceInfoModel? _selectedDevice;

    private static ObservableCollection<USBDeviceInfoModel> CreateData()
    {
        ObservableCollection<USBDeviceInfoModel> DeviceList = [];
        List<Dictionary<string, string>>? devices = USBIPD.GetAllUSBDevices();
        if (devices != null)
        {
            foreach (var device in devices)
            {
                USBDeviceInfoModel item = new(device);
                if (!item.IsConnected && item.PersistedGuid != null && item.PersistedGuid != "")
                {
                    DeviceList.Add(item);
                }
            }
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
        USBDeviceInfoModel? lastSelectedDevice = SelectedDevice;
        await Task.Run(() =>
        {
            DevicesItems = CreateData();
            if (lastSelectedDevice != null)
            {
                SelectedDevice = DevicesItems?.FirstOrDefault(x => x.HardwareId == lastSelectedDevice.HardwareId);
            }
        });

    }
    #endregion
}
