/******************************************************************************
* SPDX-License-Identifier: MIT
* Project: wsl-usb-manager
* Class: MainWindow.xaml.cs
* NameSpace: wsl_usb_manager
* Author: Chuckie
* copyright: Copyright (c) Chuckie, 2024
* Description:
* Create Date: 2024/10/1 19:08
******************************************************************************/
using MaterialDesignThemes.Wpf;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Markup;
using wsl_usb_manager.Controller;
using wsl_usb_manager.Domain;
using wsl_usb_manager.Settings;

namespace wsl_usb_manager;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly string EnglishResource = @"pack://application:,,,/Resources/LangEnglish.xaml";
    private readonly string ChineseResource = @"pack://application:,,,/Resources/LangChinese.xaml";

    public MainWindow()
    {
        InitializeComponent();
        USBMonitor m = new(OnUSBEvent);
        m.Start();
        initNotifyIcon();
        DataContext = new MainWindowViewModel(this.Title);
        log.Info("Starting...");
    }

    private void ChangeLanguage(bool isChinese)
    {
        try
        {
            if (isChinese)
            {
                System.Windows.Application.Current.Resources.MergedDictionaries[0].Source = new Uri(ChineseResource);
            }
            else
            {
                System.Windows.Application.Current.Resources.MergedDictionaries[0].Source = new Uri(EnglishResource);
            }
        }
        catch (Exception e)
        {
            log.Error(e);
        }

    }

    private void LangToggleButton_Click(object sender, RoutedEventArgs e)
    {
        ChangeLanguage(LangToggleButton.IsChecked??false);
    }

    private async void btnSetting_Click(object sender, RoutedEventArgs e)
    {
        SystemConfig old_cfg;
        var view = new SettingsView();
        if (DataContext is MainWindowViewModel vm && vm.Config != null)
        {
            old_cfg = vm.Config;
        }
        else
            old_cfg = new SystemConfig();

        view.DataContext = new SettingViewModel(old_cfg);

        if (view != null)
        {
            var result = await DialogHost.Show(view, "RootDialog");
            //DebugOutput.WriteLine("接收到对话框结果: " + (result == null ? MessageResult.None : (MessageResult)result));
        }
    }
}