/******************************************************************************
* SPDX-License-Identifier: MIT
* Project: wsl-usb-manager
* Class: MainWindow.xaml.cs
* NameSpace: wsl_usb_manager
* Author: Chuckie
* copyright: Copyright (c) Chuckie, 2025
* Description:
* Create Date: 2024/10/17 20:22
******************************************************************************/
using MaterialDesignThemes.Wpf;
using System.Windows;
using wsl_usb_manager.Resources;
using wsl_usb_manager.Settings;

namespace wsl_usb_manager;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{

    public MainWindow()
    {
        InitializeComponent();
        InitNotifition();
        DataContext = new MainWindowViewModel();
        ModifyTheme(App.GetAppConfig().DarkMode == true);
    }

    private void LangToggleButton_Click(object sender, RoutedEventArgs e)
    {
        Lang.ChangeLanguage(LangToggleButton.IsChecked ?? false);
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.UpdateLanguage();
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

    private void ListBoxNavigater_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.UpdateWindow();
        }
    }
    
}