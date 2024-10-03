/******************************************************************************
* SPDX-License-Identifier: MIT
* Project: wsl-usb-manager
* Class: MainWindowViewModel.cs
* NameSpace: wsl_usb_manager
* Author: Chuckie
* copyright: Copyright (c) Chuckie, 2024
* Description:
* Create Date: 2024/10/2 19:06
******************************************************************************/
using MaterialDesignThemes.Wpf;
using System.Collections.ObjectModel;
using wsl_usb_manager.Domain;
using wsl_usb_manager.PersistedDevice;
using wsl_usb_manager.USBDevices;

namespace wsl_usb_manager;

public class MainWindowViewModel : ViewModelBase
{
    private BodyItem? _selectedItem;
    private int _selectedIndex;
    private readonly string? _windowTitle;

    public MainWindowViewModel(string? windowTitle)
    {
        this.BodyItems =
        [
            new BodyItem("Devices", typeof(USBDevices.USBDevicesView), PackIconKind.UsbFlashDrive, PackIconKind.UsbFlashDriveOutline, new USBDevicesViewModel()),
            new BodyItem("Persisted", typeof(PersistedDeviceView), PackIconKind.StoreCheck, PackIconKind.StoreCheckOutline, new PersistedDeviceViewModel()),
        ];
        _windowTitle = windowTitle;
        SelectedItem = BodyItems.First();
    }

    public string? WindowTitle { get => _windowTitle; }

    public ObservableCollection<BodyItem> BodyItems { get; }

    public BodyItem? SelectedItem
    {
        get => _selectedItem;
        set => SetProperty(ref _selectedItem, value);
    }

    public int SelectedIndex
    {
        get => _selectedIndex;
        set => SetProperty(ref _selectedIndex, value);
    }
}
