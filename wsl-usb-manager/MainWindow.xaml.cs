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
}