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
using System.Windows;
using System.Windows.Input;
using wsl_usb_manager.Controller;
using wsl_usb_manager.Domain;
using wsl_usb_manager.USBDevices;
using MessageBox = System.Windows.MessageBox;

namespace wsl_usb_manager.PersistedDevice;

public class PersistedDeviceViewModel : ViewModelBase
{
    public ICommand RefreshCommand { get; }
    public ObservableCollection<USBDeviceInfoModel>? DevicesItems { get => _devicesItems; set => SetProperty(ref _devicesItems, value); }
    public USBDeviceInfoModel? SelectedDevice { get => _selectedDevice; set => SetProperty(ref _selectedDevice, value); }
    public bool PageEnabled { get => _pageEnabled; set => SetProperty(ref _pageEnabled, value); }
    public bool ShowRefreshProgress { get => _showRefreshProgresss; set => SetProperty(ref _showRefreshProgresss, value); }

    public PersistedDeviceViewModel()
    {
        RefreshCommand = new CommandImplementations(RefeshDevicesCommand);
        _devicesItems = CreateData(0);
        SelectedDevice = _devicesItems?.FirstOrDefault();
        PageEnabled = true;
        ShowRefreshProgress = false;
    }

    private ObservableCollection<USBDeviceInfoModel>? _devicesItems;
    private USBDeviceInfoModel? _selectedDevice;
    private bool _pageEnabled;
    private bool _showRefreshProgresss;
    private USBDeviceInfoModel? _lastSelectedDevice;

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
        else
        {
            foreach (var device in infolist)
            {
                USBDeviceInfoModel item = new(device);
                if(!item.IsConnected && item.PersistedGuid?.Length >0)
                    DeviceList.Add(item);
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
        BeforeRefresh();
        await Task.Run(() =>
        {
            DevicesItems = CreateData(2);
        });
        AfterRefresh();
    }
    #endregion

    public async void DeletePersistedDevices(List<USBDeviceInfoModel> devices)
    {
        BeforeRefresh();
        await Task.Run(() =>
        {
            foreach (var item in devices)
            {
                if(!string.IsNullOrEmpty(item.HardwareId))
                    USBIPD.UnbindDevice(item.HardwareId);
            }
        });
        DevicesItems = CreateData(3);
        AfterRefresh();
    }

    private void BeforeRefresh()
    {
        _lastSelectedDevice = SelectedDevice;
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
}
