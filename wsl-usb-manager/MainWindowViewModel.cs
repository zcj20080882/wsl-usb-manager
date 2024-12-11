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

using log4net;
using MaterialDesignThemes.Wpf;
using System.Collections.ObjectModel;
using System.Windows.Input;
using wsl_usb_manager.AutoAttach;
using wsl_usb_manager.Controller;
using wsl_usb_manager.Domain;
using wsl_usb_manager.PersistedDevice;
using wsl_usb_manager.Resources;
using wsl_usb_manager.Settings;
using wsl_usb_manager.USBDevices;

namespace wsl_usb_manager;

public partial class MainWindowViewModel : ViewModelBase
{
    #region Properties and Fields
    private BodyItem? _selectedItem;
    private int _selectedIndex;
    private string? _windowTitle;
    private bool _windowEnabled;
    private bool _isDarkMode;
    private bool _isChinese = App.GetAppConfig().Lang.Equals("zh", StringComparison.OrdinalIgnoreCase);
    private bool _showRefreshProgresss;
    private SnackbarMessageQueue _snackbarMessageQueue = new();
    private readonly ApplicationConfig AppConfig = App.GetAppConfig();
    private static readonly ILog log = LogManager.GetLogger(typeof(MainWindowViewModel));

    
    public string? WindowTitle { get => _windowTitle; set => SetProperty(ref _windowTitle, value); }
    public bool WindowEnabled { get => _windowEnabled; set => SetProperty(ref _windowEnabled, value); }
    public bool IsDarkMode
    {
        get => _isDarkMode;
        set
        {
            SetProperty(ref _isDarkMode, value);
            if (value != AppConfig.DarkMode)
            {
                AppConfig.DarkMode = value;
                App.SaveConfig();
            }
        }
    }
    public bool IsChinese
    {
        get => _isChinese;
        set
        {
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

    public MainWindowViewModel()
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

        this.BodyItems =
        [
            new BodyItem(device_tilte, typeof(USBDevicesView), PackIconKind.UsbFlashDrive, PackIconKind.UsbFlashDriveOutline, new USBDevicesViewModel()),
            new BodyItem(persisted_tilte, typeof(PersistedDeviceView), PackIconKind.StoreCheck, PackIconKind.StoreCheckOutline, new PersistedDeviceViewModel()),
            new BodyItem(autoattach_tilte, typeof(AutoAttachView), PackIconKind.StarBoxMultiple, PackIconKind.StarBoxMultipleOutline, new AutoAttachViewModel()),
        ];
        _windowTitle = Lang.GetText("WindowTitle") ?? "WSL USB Manager";
        _windowTitle += " v" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
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
        var (exitCode, _) = await USBIPD.CheckUsbipdWinInstallation();
        if (exitCode != ExitCode.Success)
        {
            if (ExitCode.NotFound == exitCode)
            {
                NotifyService.ShowErrorMessage(Lang.GetText("ErrMsgUSBIPDNotInstalled") ?? "usbipd-win is not installed, please install it firstly.");
            }
            else if (exitCode == ExitCode.LowVersion)
            {
                NotifyService.ShowErrorMessage(Lang.GetText("ErrMsgUSBIPDVersionLow") ?? "usbipd-win version is too low, please update it.");
            }
            else
            {
                NotifyService.ShowErrorMessage("Failed to check usbipd-win installation.");
            }
               return;
        }
        DisableWindow();
        if (SelectedItem?.DataContext is USBDevicesViewModel uvm)
        {
            await uvm.UpdateDevices();
        }
        else if (SelectedItem?.DataContext is PersistedDeviceViewModel pvm)
        {
            await pvm.UpdateDevices();
        }
        else if (SelectedItem?.DataContext is AutoAttachViewModel avm)
        {
            avm.UpdateDevices();
        }
        EnableWindow();
    }

    public void DisableWindow() => (ShowRefreshProgress, WindowEnabled) = (true, false);

    public void EnableWindow() => (ShowRefreshProgress, WindowEnabled) = (false, true);

    public async void UpdateWindow()
    {
        DisableWindow();
        if (SelectedItem != null)
        {
            if (SelectedItem.DataContext is USBDevicesViewModel uvm)
            {
                await uvm.UpdateDevices();
            }
            else if (SelectedItem.DataContext is PersistedDeviceViewModel pvm)
            {
                await pvm.UpdateDevices();
            }
            else if (SelectedItem.DataContext is AutoAttachViewModel avm)
            {
                avm.UpdateDevices();
            }
        }
        else
        {
            foreach (var item in BodyItems)
            {
                if (item.Content is USBDevicesView usbView && usbView.DataContext is USBDevicesViewModel uvm)
                {
                    await uvm.UpdateDevices();
                }
                else if (item.Content is PersistedDeviceView persistedView &&
                    persistedView.DataContext is PersistedDeviceViewModel pvm)
                {
                    await pvm.UpdateDevices();
                }
                else if (item.Content is AutoAttachView autoAttachView &&
                    autoAttachView.DataContext is AutoAttachViewModel avm)
                {
                    avm.UpdateDevices();
                }
            }
        }
        EnableWindow();
    }
    public void UpdateLanguage()
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

        WindowTitle = Lang.GetText("WindowTitle") ?? "WSL USB Manager";
        WindowTitle += " v" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
    }

    public async Task USBEventProcess(USBEventArgs e)
    {
        string hardwareid = e.HardwareID ?? "";
        string? name = e.Name;

        UpdateWindow();
        string msg;
        (_, _, USBDevice? changedDev) = await USBIPD.GetUSBDeviceByHardwareID(hardwareid);
        if (changedDev != null && changedDev.IsConnected)
        {
            msg = $"\"{name}({hardwareid})\" is connected to {(changedDev.IsAttached ? "WSL" : "Windows")}.";
            NotifyService.ShowNotification(msg);
        }
        else if(!e.IsConnected)
        {
            msg = $"\"{name}({hardwareid})\" is disconnected.";
            NotifyService.ShowNotification(msg);
        }
    }
}