/******************************************************************************
* SPDX-License-Identifier: MIT
* Project: wsl-usb-manager
* Class: MainWindowViewModel.cs
* NameSpace: wsl_usb_manager
* Author: Chuckie
* copyright: Copyright (c) Chuckie, 2024
* Description:
* Create Date: 2024/10/17 20:22
******************************************************************************/

// Ignore Spelling: Snackbar

using MaterialDesignThemes.Wpf;
using System.Collections.ObjectModel;
using wsl_usb_manager.Domain;
using wsl_usb_manager.PersistedDevice;
using wsl_usb_manager.USBDevices;
using wsl_usb_manager.Controller;
using wsl_usb_manager.Settings;
using log4net;
using wsl_usb_manager.AutoAttach;
using System.Windows.Input;
using wsl_usb_manager.Resources;

namespace wsl_usb_manager;

public partial class MainWindowViewModel : ViewModelBase
{
    #region Properties and Fields
    private readonly INotifyIconService _notifyIconService;
    private BodyItem? _selectedItem;
    private int _selectedIndex;
    private readonly string? _windowTitle;
    private bool _windowEnabled;
    private bool _isDarkMode;
    private bool _isChinese = App.GetAppConfig().Lang.Equals("zh",StringComparison.OrdinalIgnoreCase);
    private bool _showRefreshProgresss;
    private SnackbarMessageQueue _snackbarMessageQueue = new();
    private readonly SystemConfig Sysconfig = App.GetSysConfig();
    private readonly ApplicationConfig AppConfig = App.GetAppConfig();
    private static readonly ILog log = LogManager.GetLogger(typeof(MainWindowViewModel));

    public string? WindowTitle { get => _windowTitle; }
    public bool WindowEnabled { get => _windowEnabled; set => SetProperty(ref _windowEnabled, value); }
    public bool IsDarkMode { 
        get => _isDarkMode;
        set { 
            SetProperty(ref _isDarkMode, value);
            if (value != AppConfig.DarkMode)
            {
                AppConfig.DarkMode = value;
                App.SaveConfig();
            }
        }
    }
    public bool IsChinese { 
        get => _isChinese; 
        set { 
            SetProperty(ref _isChinese, value);
            AppConfig.Lang = value ? "zh" : "en";
            App.SaveConfig();
        } 
    }
    
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

    public SnackbarMessageQueue SnackbarMessageQueue { get => _snackbarMessageQueue; set => SetProperty(ref _snackbarMessageQueue, value); }

    public ICommand RefreshCommand { get; }
    public bool ShowRefreshProgress { get => _showRefreshProgresss; set => SetProperty(ref _showRefreshProgresss, value); }
    #endregion

    public MainWindowViewModel(string? windowTitle, INotifyIconService notifyIconService)
    {
        _notifyIconService = notifyIconService;
        if (Lang.GetText("Device") is not string device_tilte)
        {
            device_tilte = "Device";
        }

        if (Lang.GetText("Persisted") is not string persisted_tilte)
        {
            persisted_tilte = "Persisted";
        }

        if (Lang.GetText("AutoAttach") is not string autoattach_tilte)
        {
            autoattach_tilte = "Auto Attach";
        }

        this.BodyItems =
        [
            new BodyItem(device_tilte, typeof(USBDevicesView), PackIconKind.UsbFlashDrive, PackIconKind.UsbFlashDriveOutline, new USBDevicesViewModel(this)),
            new BodyItem(persisted_tilte, typeof(PersistedDeviceView), PackIconKind.StoreCheck, PackIconKind.StoreCheckOutline, new PersistedDeviceViewModel(this)),
            new BodyItem(autoattach_tilte, typeof(AutoAttachView), PackIconKind.StarBoxMultiple, PackIconKind.StarBoxMultipleOutline, new AutoAttachViewModel(this)),
        ];
        _windowTitle = windowTitle;
        SelectedItem = BodyItems.First();
        WindowEnabled = true;
        IsDarkMode = AppConfig.DarkMode;

        RefreshCommand = new CommandImplementations(RefeshDevicesCommand);
        ShowRefreshProgress = false;
    }

    /// <summary>
    /// Refresh Button command
    /// </summary>
    /// <param name="obj"></param>
    private async void RefeshDevicesCommand(object? obj)
    {
        log.Info("Refresh device...");
        if(SelectedItem?.Content is USBDevicesView || SelectedItem?.Content is PersistedDeviceView)
        {
            if (obj is List<USBDevice> list) {
                await UpdateUSBDevices(list);
            }
            else
            {
                await UpdateUSBDevices(null);
            }
        }
        else if(SelectedItem?.DataContext is AutoAttachViewModel avm)
        {
            avm.UpdateDevices(Sysconfig.AutoAttachDeviceList);
        }
    }

    private void DisableWindow()
    {
        ShowRefreshProgress = true;
        WindowEnabled = false;
    }

    private void EnableWindow()
    {
        WindowEnabled = true;
        ShowRefreshProgress = false;
    }

    public void UpdateUI()
    {
        if (Lang.GetText("Device") is not string device_tilte)
        {
            device_tilte = "Device";
        }

        if (Lang.GetText("Persisted") is not string persisted_tilte)
        {
            persisted_tilte = "Persisted";
        }

        if (Lang.GetText("AutoAttach") is not string autoattach_tilte)
        {
            autoattach_tilte = "Auto Attach";
        }

        foreach (BodyItem item in BodyItems)
        {
            if (item.Content is USBDevicesView)
            {
                item.Name = device_tilte;
            }
            else if (item.Content is PersistedDeviceView)
            {
                item.Name = persisted_tilte;
            }
            else if (item.Content is AutoAttachView)
            {
                item.Name = autoattach_tilte;
            }
        }
        RefeshDevicesCommand(ConnectedDeviceList);
    }
    public void ShowNotification(string message)
    {
        if (OperatingSystem.IsWindowsVersionAtLeast(7))
        {
            _notifyIconService.ShowNotification(message);
        }
    }

    public void ShowErrorMessage(string message) 
    {
        if (OperatingSystem.IsWindowsVersionAtLeast(7))
        {
            _notifyIconService.ShowErrorMessage(message);
        }
    }
}