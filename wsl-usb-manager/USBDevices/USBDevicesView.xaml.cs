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
using System.Windows.Controls;
using MessageBox = System.Windows.MessageBox;

namespace wsl_usb_manager.USBDevices;

/// <summary>
/// View.xaml 的交互逻辑
/// </summary>
public partial class USBDevicesView : System.Windows.Controls.UserControl
{
    public USBDevicesView()
    {
        InitializeComponent();
    }

    private void BoundCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.CheckBox checkBox)
        {
            if (checkBox.DataContext is USBDeviceInfoModel device)
            {
                USBDevicesViewModel? dm = DataContext as USBDevicesViewModel;
                dm?.BindDevice(device, checkBox.IsChecked ?? false);
            }
        }
    }

    private void AttachCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.CheckBox checkBox)
        {
            if (checkBox.DataContext is USBDeviceInfoModel device)
            {
                USBDevicesViewModel? dm = DataContext as USBDevicesViewModel;
                dm?.AttachDevice(device, checkBox.IsChecked ?? false);
            }
        }
    }

    private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is USBDevicesViewModel dm && dm.SelectedDevice is USBDeviceInfoModel device)
        {
            dm.MenuBindEnabled = !device.IsBound;
            dm.MenuAttachEnabled = device.IsBound;
            dm.MenuDetachEnabled = device.IsAttached;
            dm.MenuUnbindEnabled = device.IsBound;
        }
    }

    private void MenuItem_Click(object sender, RoutedEventArgs e)
    {

        if(sender is System.Windows.Controls.MenuItem item && DataContext is USBDevicesViewModel dm)
        {
            if(dm.SelectedDevice is USBDeviceInfoModel device)
            {
                switch (item.Name)
                {
                    case "MenuItemBind":
                        dm.BindDevice(device, true);
                        break;

                    case "MenuItemUnbind":
                        dm.BindDevice(device, false);
                        break;
                    case "MenuItemAttach":
                        dm.AttachDevice(device, true);
                        break;
                    case "MenuItemDetach":
                        dm.AttachDevice(device, false);
                        break;
                    case "MenuItemAddToAutoAttach":
                        break;
                    default:
                        break;
                }
            }
            else
                MessageBox.Show("No device is selected.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
