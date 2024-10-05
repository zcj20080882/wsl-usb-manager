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
    public bool ShowRefreshProgress { get => _showRefreshProgresss; set => SetProperty(ref _showRefreshProgresss, value); }

    public PersistedDeviceViewModel(MainWindowViewModel mainDataContext)
    {
        RefreshCommand = new CommandImplementations(RefeshDevicesCommand);
        ShowRefreshProgress = false;
        _mainDataContext = mainDataContext;
    }

    private ObservableCollection<USBDeviceInfoModel>? _devicesItems;
    private USBDeviceInfoModel? _selectedDevice;
    private bool _showRefreshProgresss;
    private USBDeviceInfoModel? _lastSelectedDevice;
    private MainWindowViewModel _mainDataContext { get; }

    #region Commands
    /// <summary>
    /// Refresh Button command
    /// </summary>
    /// <param name="obj"></param>
    private void RefeshDevicesCommand(object? obj)
    {
        _mainDataContext.UpdateUSBDevicesAsync(2, 100);
    }
    #endregion

    public async void DeletePersistedDevices(List<USBDeviceInfoModel> devices)
    {
        await Task.Run(() =>
        {
            foreach (var item in devices)
            {
                if (!string.IsNullOrEmpty(item.HardwareId))
                    USBIPD.UnbindDevice(item.HardwareId);
            }
        });
        _mainDataContext.UpdateUSBDevicesAsync(2, 100);
    }

    public void BeforeRefresh()
    {
        _lastSelectedDevice = SelectedDevice;
        ShowRefreshProgress = true;
    }

    public void AfterRefresh()
    {
        ShowRefreshProgress = false;
        if (_lastSelectedDevice != null)
        {
            SelectedDevice = DevicesItems?.FirstOrDefault(di => string.Equals(di.HardwareId, _lastSelectedDevice.HardwareId, StringComparison.CurrentCultureIgnoreCase)) ?? DevicesItems?.First();
        }
    }

    public void UpdateDevices(List<USBDevicesInfo> UpdaterList)
    {
        ObservableCollection<USBDeviceInfoModel> DeviceList = [];

        foreach (var device in UpdaterList)
        {
            USBDeviceInfoModel item = new(device);
            if (!item.IsConnected)
                DeviceList.Add(item);
        }
        DevicesItems = DeviceList;
    }
}
