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
using System.ComponentModel;
using System.Windows;
using wsl_usb_manager.Controller;
using wsl_usb_manager.Domain;
using MessageBox = System.Windows.MessageBox;

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
        initNotifyIcon();
        DataContext = new MainWindowViewModel(this.Title);
    }
}