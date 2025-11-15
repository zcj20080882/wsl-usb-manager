/******************************************************************************
* SPDX-License-Identifier: MIT
* Project: wsl-usb-manager
* Class: USBDevicesView.xaml.cs
* NameSpace: wsl_usb_manager.USBDevices
* Author: Chuckie
* copyright: Copyright (c) Chuckie, 2025
* Description:
* Create Date: 2024/10/17 20:22
******************************************************************************/
using Newtonsoft.Json.Linq;
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

    private async void AutoAttachCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.CheckBox checkBox)
        {
            if (checkBox.DataContext is USBDeviceInfoModel device)
            {
                if (device.IsAutoAttach)
                {
                    if (!device.IsInFilterDeviceList())
                    {
                        NotifyService.DisableWindow();
                        await device.AddToAutoAttach();
                        NotifyService.EnableWindow();
                    }
                }
                else
                {
                    if (device.IsInAutoAttachList())
                    {
                        device.RemoveFromAutoAttach();
                    }
                }
            }
        }
    }

    private async void BoundCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.CheckBox checkBox)
        {
            if(checkBox.DataContext is USBDeviceInfoModel device)
            {
                NotifyService.DisableWindow();
                if (device.IsBound)
                {
                    await device.Bind();
                }
                else
                {
                    await device.Unbind();
                }
                NotifyService.EnableWindow();
            }          
        }
    }

    private async void AttachCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.CheckBox checkBox)
        {
            if (checkBox.DataContext is USBDeviceInfoModel device)
            {
                NotifyService.DisableWindow();
                if (device.IsAttached)
                {
                    await device.Attach(true);
                }
                else
                {
                    await device.Detach();
                }
                NotifyService.EnableWindow();
            }
        }
    }

    private void UpdateContextMenu()
    {
        if (DataContext is USBDevicesViewModel dm)
        {
            if(dm.SelectedDevice is USBDeviceInfoModel device)
            {
                dm.MenuBindEnabled = !device.IsBound && !device.IsInAutoAttachList() && !device.IsInFilterDeviceList();
                dm.MenuAttachEnabled = device.IsBound && !device.IsAttached && !device.IsInAutoAttachList();
                dm.MenuDetachEnabled = device.IsAttached && !device.IsInAutoAttachList();
                dm.MenuUnbindEnabled = device.IsBound && !device.IsInAutoAttachList();
                dm.MenuHideEnabled = !device.IsInFilterDeviceList() && !device.IsInAutoAttachList();
                dm.MenuUnhiddenEnabled = device.IsInFilterDeviceList();
                dm.MenuAddToAutoEnabled = !device.IsInAutoAttachList() && !device.IsInFilterDeviceList();
                dm.MenuRemoveFromAutoEnabled = device.IsInAutoAttachList() && !device.IsInFilterDeviceList();
            }
            else
            {
                dm.MenuBindEnabled = false;
                dm.MenuAttachEnabled = false;
                dm.MenuDetachEnabled = false;
                dm.MenuUnbindEnabled = false;
                dm.MenuHideEnabled = false;
                dm.MenuUnhiddenEnabled = false;
                dm.MenuAddToAutoEnabled = false;
                dm.MenuRemoveFromAutoEnabled = false;
            }
        }
    }

    private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateContextMenu();
    }

    private async void MenuItem_Click(object sender, RoutedEventArgs e)
    {

        if (sender is MenuItem item && DataContext is USBDevicesViewModel dm)
        {
            if (dm.SelectedDevice is USBDeviceInfoModel device)
            {
                NotifyService.DisableWindow();
                ObservableCollection<USBDeviceInfoModel>? oldList = dm.USBDeviceInfoModules;
                switch (item.Name)
                {
                    case "MenuItemBind":
                        await device.Bind();
                        break;
                    case "MenuItemUnbind":
                        await device.Unbind();
                        break;
                    case "MenuItemAttach":
                        await device.Attach(true);
                        break;
                    case "MenuItemDetach":
                        await device.Detach();
                        break;
                    case "MenuItemAddToAutoAttach":
                        if (!device.IsInFilterDeviceList())
                        {
                            await device.AddToAutoAttach();
                        }
                        break;
                    case "MenuItemRemoveFromAutoAttach":
                        if (device.IsInAutoAttachList())
                        {
                            device.RemoveFromAutoAttach();
                        }
                        break;
                    case "MenuItemHide":
                        device.AddToFilter();
                        break;
                    case "MenuItemUnhiden":
                        device.RemoveFromFilter();
                        break;
                    case "MenuItemShowHide":
                        if (dm.USBDeviceInfoModules == null)
                        {
                            break;
                        }
                        foreach (USBDeviceInfoModel usbDeviceInfo in dm.USBDeviceInfoModules)
                        {
                            usbDeviceInfo.IsVisible = true;
                        }
                        dm.USBDeviceInfoModules = oldList;
                        break;
                    default:
                        break;
                }
                NotifyService.EnableWindow();
            }
            else
                NotifyService.ShowErrorMessage("No device is selected.");
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
            UpdateContextMenu();
            listViewItem.Focus();
            return;
        }
        if (listCount > 0)
        {
            UpdateContextMenu();
            return;
        }
        e.Handled = true;
    }

}
