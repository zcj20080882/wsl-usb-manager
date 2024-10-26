/******************************************************************************
* SPDX-License-Identifier: MIT
* Project: wsl-usb-manager
* Class: MainWindow.xaml.cs
* NameSpace: wsl_usb_manager
* Author: Chuckie
* copyright: Copyright (c) Chuckie, 2024
* Description:
* Create Date: 2024/10/17 20:22
******************************************************************************/
using MaterialDesignThemes.Wpf;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Markup;
using wsl_usb_manager.AutoAttach;
using wsl_usb_manager.Controller;
using wsl_usb_manager.Domain;
using wsl_usb_manager.PersistedDevice;
using wsl_usb_manager.Resources;
using wsl_usb_manager.Settings;
using wsl_usb_manager.USBDevices;

namespace wsl_usb_manager;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{

    public MainWindow()
    {
        InitializeComponent();
        USBMonitor m = new(OnUSBEvent);
        m.Start();
        InitNotifyIcon();
        DataContext = new MainWindowViewModel(this);
        ModifyTheme(App.GetAppConfig().DarkMode == true);
        log.Info("Starting...");
    }

    private void LangToggleButton_Click(object sender, RoutedEventArgs e)
    {
        Lang.ChangeLanguage(LangToggleButton.IsChecked??false);
        if (DataContext is MainWindowViewModel viewModel) { 
            viewModel.UpdateUI();
        }
    }

    private async void BtnSetting_Click(object sender, RoutedEventArgs e)
    {
        ApplicationConfig new_cfg = App.GetAppConfig().Clone();
        var view = new SettingsView(new SettingViewModel(new_cfg));

        if (view != null)
        {
            if (await DialogHost.Show(view, "RootDialog") is string result && result != null)
            {
                if (result == "OK")
                {
                    App.SetAppConfig(new_cfg);
                }
            }
        }
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if(DataContext is MainWindowViewModel vm)
        {
            await USBIPD.InitUSBIPD();
            await vm.UpdateUSBDevices(null);
        }
    }

    private void ListBoxNavigater_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if(DataContext is MainWindowViewModel vm)
        {
            if(vm.SelectedItem != null)
            {
                if(vm.SelectedItem.DataContext is USBDevicesViewModel uvm)
                {
                    uvm.UpdateDevices(vm.GetConnectedDeviceList());
                }
                else if(vm.SelectedItem.DataContext is PersistedDeviceViewModel pvm)
                {
                    pvm.UpdateDevices(vm.GetPersistedDeviceList());
                }
                else if (vm.SelectedItem.DataContext is AutoAttachViewModel avm)
                {
                    avm.UpdateDevices(App.GetSysConfig().AutoAttachDeviceList);
                }
            }
        }
    }
}