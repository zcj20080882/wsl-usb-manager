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
using System.Reflection;
using System.Windows;
using wsl_usb_manager.Controller;
using MessageBox = System.Windows.MessageBox;
using System.Xml;
using Newtonsoft.Json;
using System.IO;

namespace wsl_usb_manager;

public class MainWindowViewModel : ViewModelBase
{
    private BodyItem? _selectedItem;
    private int _selectedIndex;
    private readonly string? _windowTitle;
    private bool _windowEnabled;
    private readonly string ConfigFilePath = Environment.CurrentDirectory + "/config.json";
    private bool _isDarkMode;
    private bool _isChinese = false;

    public MainWindowViewModel(string? windowTitle)
    {
        this.BodyItems =
        [
            new BodyItem("Devices", typeof(USBDevicesView), PackIconKind.UsbFlashDrive, PackIconKind.UsbFlashDriveOutline, new USBDevicesViewModel(this)),
            new BodyItem("Persisted", typeof(PersistedDeviceView), PackIconKind.StoreCheck, PackIconKind.StoreCheckOutline, new PersistedDeviceViewModel(this)),
        ];
        _windowTitle = windowTitle;
        UpdateUSBDevicesAsync(0,0);
        SelectedItem = BodyItems.First();
        WindowEnabled = true;

        if (File.Exists(ConfigFilePath))
        {
            string json = File.ReadAllText(ConfigFilePath);
            var cfg = JsonConvert.DeserializeObject<SystemConfig>(json);
            if (cfg != null) Config = cfg;
        }

        if (Config == null)
        {
            Config ??= new SystemConfig();
            SaveConfig();
        }
        IsDarkMode = Config.DarkMode;
    }

    public string? WindowTitle { get => _windowTitle; }
    public bool WindowEnabled { get => _windowEnabled; set => SetProperty(ref _windowEnabled, value); }
    public bool IsDarkMode { 
        get => _isDarkMode;
        set { 
            SetProperty(ref _isDarkMode, value);
            if (Config != null && value != Config.DarkMode)
            {
                Config.DarkMode = value;
                SaveConfig();
            }
        }
    }
    public bool IsChinese { 
        get => _isChinese; 
        set { 
            SetProperty(ref _isChinese, value); 
            if (Config != null)
            {
                if (value != Config.IsChinese)
                {
                    Config.IsChinese = value;
                    SaveConfig();
                }
            }
        } 
    }
    
    public ObservableCollection<BodyItem> BodyItems { get; }
    public SystemConfig? Config { get; set; }

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

    public static void CallReflectionMethod(object obj, string methodName, object?[]? parameters)
    {
        Type type = obj.GetType();
        type.GetMethod(methodName)?.Invoke(obj, parameters);
    }

    public static void SetReflectionProperty(object obj, string propertyName, object? value)
    {
        Type type = obj.GetType();
        type.GetProperty(propertyName)?.SetValue(obj, value);
    }

    public async void UpdateUSBDevicesAsync(int retryCount, int retryDelayMs = 100)
    {
        if (SelectedItem?.Content is FrameworkElement elm1 && elm1.DataContext is object obj1)
        {
            CallReflectionMethod(obj1, "BeforeRefresh", null);
        }
        WindowEnabled = false;
        int ret = -1;
        string errormsg = "Unknown error.";
        List<USBDevicesInfo>? infolist = null;
        
        await Task.Run(() =>
        {
            (ret, errormsg, infolist) = USBIPD.GetAllUSBDevices();
            while (retryCount > 0 && ret == 0)
            {
                (ret, errormsg, infolist) = USBIPD.GetAllUSBDevices();
                Task.Delay(retryDelayMs);
                retryCount--;
            }
        });
        
        if (ret == 0 && infolist != null)
        {
            foreach (var item in BodyItems)
            {
                if (item.Content is FrameworkElement item_content && item_content.DataContext is object obj)
                {
                    CallReflectionMethod(obj, "UpdateDevices", [infolist]);
                }
            }
        }
        else
            MessageBox.Show(errormsg, "Error", MessageBoxButton.OK, MessageBoxImage.Error);

        if (SelectedItem?.Content is FrameworkElement elm2 && elm2.DataContext is object obj2)
        {
            CallReflectionMethod(obj2, "AfterRefresh", null);
        }
        WindowEnabled = true;
    }

    public void SaveConfig()
    {
        string json = JsonConvert.SerializeObject(Config, Newtonsoft.Json.Formatting.Indented);
        File.WriteAllText(ConfigFilePath, json);
    }
}
