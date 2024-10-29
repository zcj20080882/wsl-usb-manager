/******************************************************************************
* SPDX-License-Identifier: MIT
* Project: wsl-usb-manager
* Class: USBDevicesView.xaml.cs
* NameSpace: wsl_usb_manager.USBDevices
* Author: Chuckie
* copyright: Copyright (c) Chuckie, 2024
* Description:
* Create Date: 2024/10/17 20:22
******************************************************************************/
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using wsl_usb_manager.Domain;
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

    private void AutoAttachCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is USBDevicesViewModel vm)
        {
            vm.UpdateDevices(null);
        }
    }

    private void BoundCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.CheckBox checkBox)
        {
            if (checkBox.DataContext is USBDeviceInfoModel device)
            {
                if (device.IsBound)
                {
                    device.Bind();
                }
                else
                {
                    device.Unbind();
                }
            }
        }
    }

    private void AttachCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.CheckBox checkBox)
        {
            if (checkBox.DataContext is USBDeviceInfoModel device)
            {
                if (device.IsAttached)
                {
                    device.Attach();
                }
                else
                {
                    device.Detach();
                }
            }
        }
    }

    private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is USBDevicesViewModel dm && dm.SelectedDevice is USBDeviceInfoModel device)
        {
            bool isInFilterList = App.GetSysConfig().IsInFilterDeviceList(device.Device);
            bool isInAutoAttachList = App.GetSysConfig().IsInAutoAttachDeviceList(device.Device);
            dm.MenuBindEnabled = !device.IsBound && !isInAutoAttachList && !isInFilterList;
            dm.MenuAttachEnabled = device.IsBound && !device.IsAttached && !isInAutoAttachList;
            dm.MenuDetachEnabled = device.IsAttached && !isInAutoAttachList;
            dm.MenuUnbindEnabled = device.IsBound && !isInAutoAttachList;
            dm.MenuHideEnabled = !isInFilterList && !isInAutoAttachList;
            dm.MenuUnhiddenEnabled = isInFilterList;
            dm.MenuAddToAutoEnabled = !isInAutoAttachList && !isInFilterList;
            dm.MenuRemoveFromAutoEnabled = isInAutoAttachList && !isInFilterList;
        }
    }

    private void MenuItem_Click(object sender, RoutedEventArgs e)
    {

        if (sender is MenuItem item && DataContext is USBDevicesViewModel dm)
        {
            if (dm.SelectedDevice is USBDeviceInfoModel device)
            {
                ObservableCollection<USBDeviceInfoModel>? oldList = dm.USBDevicesItems;
                switch (item.Name)
                {
                    case "MenuItemBind":
                        device.Bind();
                        break;

                    case "MenuItemUnbind":
                        device.Unbind();
                        break;
                    case "MenuItemAttach":
                        device.AutoAttach();
                        break;
                    case "MenuItemDetach":
                        device.Detach();
                        break;
                    case "MenuItemAddToAutoAttach":
                        device.AddToAutoAttach();
                        break;
                    case "MenuItemRemoveFromAutoAttach":
                        device.RemoveFromAutoAttach();
                        dm.UpdateDevices(null);
                        break;
                    case "MenuItemHide":
                        device.AddToFilter();
                        dm.UpdateDevices(null);
                        break;
                    case "MenuItemUnhiden":
                        device.RemoveFromFilter();
                        dm.UpdateDevices(null);
                        break;
                    case "MenuItemShowHide":
                        if (dm.USBDevicesItems == null)
                        {
                            break;
                        }
                        foreach (USBDeviceInfoModel usbDeviceInfo in dm.USBDevicesItems)
                        {
                            usbDeviceInfo.IsVisible = true;
                        }
                        dm.USBDevicesItems = oldList;
                        break;
                    default:
                        break;
                }
            }
            else
                MessageBox.Show("No device is selected.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ListView_PreviewMouseRightButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        DependencyObject? obj = e.OriginalSource as DependencyObject;
        int listCount = 0;
        while (obj != null && obj is not System.Windows.Controls.ListViewItem)
        {
            if (obj is ItemsControl ic)
            {
                listCount = ic.Items.Count;
            }
            obj = VisualTreeHelper.GetParent(obj);
        }

        if (obj is System.Windows.Controls.ListViewItem listViewItem)
        {
            listViewItem.Focus();
            return;
        }
        if (DataContext is USBDevicesViewModel dm && listCount > 0)
        {
            dm.MenuBindEnabled = false;
            dm.MenuAttachEnabled = false;
            dm.MenuDetachEnabled = false;
            dm.MenuUnbindEnabled = false;
            dm.MenuHideEnabled = false;
            dm.MenuUnhiddenEnabled = false;
            dm.MenuAddToAutoEnabled = false;
            dm.MenuRemoveFromAutoEnabled = false;
            return;
        }
        e.Handled = true;
    }
}
