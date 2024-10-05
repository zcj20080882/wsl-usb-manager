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

// Ignore Spelling: infolist

using System.Collections.ObjectModel;
using System.Security.Permissions;
using System.Windows;
using System.Windows.Input;
using wsl_usb_manager.Controller;
using wsl_usb_manager.Domain;
using MessageBox = System.Windows.MessageBox;

namespace wsl_usb_manager.USBDevices;

public class USBDevicesViewModel : ViewModelBase
{
    public ICommand RefreshCommand { get; }
    public bool ShowRefreshProgress { get => _showRefreshProgresss; set => SetProperty(ref _showRefreshProgresss, value); }
    public bool MenuBindEnabled { get => _menuBindEnabled; set => SetProperty(ref _menuBindEnabled, value); }
    public bool MenuAttachEnabled { get => _menuAttachEnabled; set => SetProperty(ref _menuAttachEnabled, value); }
    public bool MenuDetachEnabled { get => _menuDetachEnabled; set => SetProperty(ref _menuDetachEnabled, value); }
    public bool MenuUnbindEnabled { get => _menuUnBindEnabled; set => SetProperty(ref _menuUnBindEnabled, value); }
    public ObservableCollection<USBDeviceInfoModel>? USBDevicesItems { get => _usbDevicesItems; set => SetProperty(ref _usbDevicesItems, value);}
    public USBDeviceInfoModel? SelectedDevice { get => _selectedDevice; set => SetProperty(ref _selectedDevice, value); }

    public USBDevicesViewModel(MainWindowViewModel mainDataContext)
    {
        RefreshCommand = new CommandImplementations(RefeshDevicesCommand);
        ShowRefreshProgress = false;
        MenuBindEnabled = true;
        MenuAttachEnabled = true;
        MainDataContext = mainDataContext;
    }

    private bool _showRefreshProgresss;
    private bool _menuBindEnabled;
    private bool _menuAttachEnabled;
    private bool _menuUnBindEnabled;
    private bool _menuDetachEnabled;

    private ObservableCollection<USBDeviceInfoModel>? _usbDevicesItems;
    private USBDeviceInfoModel? _selectedDevice;
    private USBDeviceInfoModel? _lastSelectedDevice;
    private MainWindowViewModel MainDataContext { get; }

    #region Commands
    /// <summary>
    /// Refresh Button command
    /// </summary>
    /// <param name="obj"></param>
    private void RefeshDevicesCommand(object? obj)
    {
        MainDataContext.UpdateUSBDevicesAsync(2, 100);
    }

    public async void BindDevice(USBDeviceInfoModel device,bool bind)
    {
        await Task.Run(() =>
        {
            if (!string.IsNullOrEmpty(device.HardwareId))
            {
                CommandResult result = bind ? USBIPD.BindDevice(device.HardwareId, device.IsForced) : USBIPD.UnbindDevice(device.HardwareId);
                if (result.ExitCode != 0 && result.StandardError.Length > 0)
                {
                    MessageBox.Show($"Failed to {(bind ? "bind" : "unbind")} {device.HardwareId}: {result.StandardError}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        });
        MainDataContext.UpdateUSBDevicesAsync(3, 100);
    }

    public async void AttachDevice(USBDeviceInfoModel device, bool attach)
    {
        await Task.Run(() =>
        {
            if(!string.IsNullOrEmpty(device.HardwareId))
            {
                CommandResult result = attach ? USBIPD.AttachDeviceLocal(device.HardwareId) : USBIPD.DetachDevice(device.HardwareId);
                if (result.ExitCode != 0 && result.StandardError.Length > 0)
                {
                    MessageBox.Show($"Failed to {(attach ? "attach" : "detach")} {device.HardwareId}: {result.StandardError}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        });
        MainDataContext.UpdateUSBDevicesAsync(3, 100);
    }
    #endregion

    public void UpdateDevices(List<USBDevicesInfo> infolist)
    {
        ObservableCollection<USBDeviceInfoModel> DeviceList = [];
        
        foreach (var device in infolist)
        {
            USBDeviceInfoModel item = new(device);
            if (item.IsConnected)
                DeviceList.Add(item);
        }
        USBDevicesItems = DeviceList;
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
            SelectedDevice = USBDevicesItems?.FirstOrDefault(di => string.Equals(di.HardwareId, _lastSelectedDevice.HardwareId, StringComparison.CurrentCultureIgnoreCase)) ?? USBDevicesItems?.First();
        }
    }
}
