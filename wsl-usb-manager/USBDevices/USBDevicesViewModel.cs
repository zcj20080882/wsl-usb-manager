/******************************************************************************
* SPDX-License-Identifier: MIT
* Project: wsl-usb-manager
* Class: USBDevicesViewModel.cs
* NameSpace: wsl_usb_manager.USBDevices
* Author: Chuckie
* copyright: Copyright (c) Chuckie, 2024
* Description:
* Create Date: 2024/10/17 20:22
******************************************************************************/

// Ignore Spelling: infolist

using log4net;
using System.Collections.ObjectModel;
using wsl_usb_manager.Controller;
using wsl_usb_manager.Domain;

namespace wsl_usb_manager.USBDevices;

public class USBDevicesViewModel : ViewModelBase
{
    private static readonly ILog log = LogManager.GetLogger(typeof(USBDevicesViewModel));

    public bool MenuBindEnabled { get => _menuBindEnabled; set => SetProperty(ref _menuBindEnabled, value); }
    public bool MenuAttachEnabled { get => _menuAttachEnabled; set => SetProperty(ref _menuAttachEnabled, value); }
    public bool MenuDetachEnabled { get => _menuDetachEnabled; set => SetProperty(ref _menuDetachEnabled, value); }
    public bool MenuUnbindEnabled { get => _menuUnBindEnabled; set => SetProperty(ref _menuUnBindEnabled, value); }
    public bool MenuHideEnabled { get => _menuHideEnabled; set => SetProperty(ref _menuHideEnabled, value); }
    public bool MenuShowEnabled { get => _menuShowEnabled; set => SetProperty(ref _menuShowEnabled, value); }
    public bool MenuUnhiddenEnabled { get => _menuUnhidenEnabled; set => SetProperty(ref _menuUnhidenEnabled, value); }
    public bool MenuAddToAutoEnabled { get => _menuAddToAutoEanbled; set => SetProperty(ref _menuAddToAutoEanbled, value); }
    public bool MenuRemoveFromAutoEnabled { get => _menuRemoveFromAutoEnabled; set => SetProperty(ref _menuRemoveFromAutoEnabled, value); }
    public ObservableCollection<USBDeviceInfoModel>? USBDevicesItems { get => _usbDevicesItems; set => SetProperty(ref _usbDevicesItems, value); }
    public USBDeviceInfoModel? SelectedDevice { get => _selectedDevice; set => SetProperty(ref _selectedDevice, value); }

    public USBDevicesViewModel()
    {
        MenuBindEnabled = true;
        MenuAttachEnabled = true;
    }

    private bool _menuBindEnabled;
    private bool _menuAttachEnabled;
    private bool _menuUnBindEnabled;
    private bool _menuDetachEnabled;
    private bool _menuHideEnabled;
    private bool _menuShowEnabled;
    private bool _menuUnhidenEnabled;
    private bool _menuAddToAutoEanbled;
    private bool _menuRemoveFromAutoEnabled;

    private ObservableCollection<USBDeviceInfoModel>? _usbDevicesItems;
    private USBDeviceInfoModel? _selectedDevice;
    private USBDeviceInfoModel? _lastSelectedDevice;
    private List<USBDevice>? _connectedDeviceList;

    private bool _initialized = false;

    public List<USBDevice>? ConnectedDeviceList { get => _connectedDeviceList; }

    public async Task UpdateDevices()
    {
        ObservableCollection<USBDeviceInfoModel> DeviceList = [];
        (ExitCode ret, string err, List<USBDevice>? USBDeviceList) = await USBIPD.ListConnectedDevices();
        if (ret != ExitCode.Success)
        {
            log.Warn($"Failed to get USB devices: {err}");
            return;
        }
        if (USBDeviceList == null)
        {
            log.Debug($"No connected device is found.");
            return;
        }

        _lastSelectedDevice = SelectedDevice;
        foreach (var device in USBDeviceList)
        {
            USBDeviceInfoModel item = new(device);
            DeviceList.Add(item);

            if (item.IsConnected && _initialized)
            {
                await item.AutoAttach();
            }
        }

        _connectedDeviceList = USBDeviceList;
        USBDevicesItems = DeviceList;
        if (_lastSelectedDevice != null && USBDevicesItems != null && USBDevicesItems.Any())
        {
            SelectedDevice = USBDevicesItems?.FirstOrDefault(di => string.Equals(di.HardwareId, _lastSelectedDevice.HardwareId, StringComparison.CurrentCultureIgnoreCase)) ?? USBDevicesItems?.First();
        }
        _initialized=true;
    }
}
