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
using System.Windows.Input;
using wsl_usb_manager.Controller;
using wsl_usb_manager.Domain;

namespace wsl_usb_manager.USBDevices;

public class USBDevicesViewModel : ViewModelBase
{
    public ICommand RefeshCommand { get; }
    public bool ShowRefreshProgress { get => _showRefreshProgresss; set => SetProperty(ref _showRefreshProgresss, value); }
    public bool PageEnabled { get => _pageEnabled; set => SetProperty(ref _pageEnabled, value); }
    public ObservableCollection<USBDeviceInfoModel>? USBDevicesItems { get => _usbDevicesItems; set => SetProperty(ref _usbDevicesItems, value);}
    public USBDeviceInfoModel? SelectedDevice { get => _selectedDevice; set => SetProperty(ref _selectedDevice, value); }

    public USBDevicesViewModel()
    {
        RefeshCommand = new CommandImplementations(RefeshDevicesCommand);
        ShowRefreshProgress = false;
        _usbDevicesItems = CreateData();
        SelectedDevice = _usbDevicesItems?.FirstOrDefault();
        PageEnabled = true;
    }

    private bool _showRefreshProgresss;
    private bool _pageEnabled;
    private ObservableCollection<USBDeviceInfoModel>? _usbDevicesItems;
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
                if (item.IsConnected)
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
        ShowRefreshProgress = true;
        PageEnabled = false;
        USBDeviceInfoModel? lastSelectedDevice = SelectedDevice;
        await Task.Run(() =>
        {
            USBDevicesItems = CreateData();
            PageEnabled = true;
            ShowRefreshProgress = false;
            if (lastSelectedDevice != null)
            {
                SelectedDevice = USBDevicesItems?.FirstOrDefault(x => x.HardwareId == lastSelectedDevice.HardwareId);
            }
        });
        
    }
    #endregion
}
