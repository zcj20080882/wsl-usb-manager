/******************************************************************************
* SPDX-License-Identifier: MIT
* Project: wsl-usb-manager
* Class: USBDevicesView.xaml.cs
* NameSpace: wsl_usb_manager.USBDevices
* Author: Chuckie
* copyright: Copyright (c) Chuckie, 2024
* Description:
* Create Date: 2024/10/2 18:58
******************************************************************************/
using System.Windows;

namespace wsl_usb_manager.USBDevices;

/// <summary>
/// View.xaml 的交互逻辑
/// </summary>
public partial class USBDevicesView : System.Windows.Controls.UserControl
{
    public USBDevicesView()
    {
        InitializeComponent();
        DataContext = new USBDevicesViewModel();
    }

    private void BoundCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.CheckBox checkBox)
        {
            if (checkBox.DataContext is USBDeviceInfoModel)
            {
                USBDevicesViewModel? dm = DataContext as USBDevicesViewModel;
                dm?.RefeshCommand.Execute(null);
            }
        }
    }

    private void AttaachCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.CheckBox checkBox)
        {
            if (checkBox.DataContext is USBDeviceInfoModel)
            {
                USBDevicesViewModel? dm = DataContext as USBDevicesViewModel;
                dm?.RefeshCommand.Execute(null);
            }
        }
    }
}
