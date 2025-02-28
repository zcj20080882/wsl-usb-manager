/******************************************************************************
* SPDX-License-Identifier: MIT
* Project: wsl-usb-manager
* Class: PersistedDeviceView.xaml.cs
* NameSpace: wsl_usb_manager.PersistedDevice
* Author: Chuckie
* copyright: Copyright (c) Chuckie, 2025
* Description:
* Create Date: 2024/10/17 20:22
******************************************************************************/
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using wsl_usb_manager.Domain;
using MessageBox = System.Windows.MessageBox;

namespace wsl_usb_manager.PersistedDevice;

/// <summary>
/// PersistedDeviceView.xaml 的交互逻辑
/// </summary>
public partial class PersistedDeviceView : System.Windows.Controls.UserControl
{
    public PersistedDeviceView()
    {
        InitializeComponent();
    }

    private async void MenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem item && DataContext is PersistedDeviceViewModel dm)
        {
            switch (item.Name)
            {
                case "MenuItemDeleteOne":
                    if (dm.SelectedDevice is USBDeviceInfoModel device)
                    {
                        await device.Unbind();
                    }
                    else
                        NotifyService.ShowErrorMessage("No device is selected.");
                    await dm.UpdateDevices();
                    break;
                case "MenuItemDeleteAll":
                    if (dm.DeviceInfoModules != null && dm.DeviceInfoModules.Count > 0)
                    {
                        foreach (var d in dm.DeviceInfoModules)
                        {
                            await d.Unbind();
                        }
                    }
                    await dm.UpdateDevices();
                    break;
                default:
                    break;
            }
        }
    }

    private void ListView_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        DependencyObject? obj = e.OriginalSource as DependencyObject;
        while (obj != null && obj is not System.Windows.Controls.ListViewItem)
        {
            obj = VisualTreeHelper.GetParent(obj);
        }

        if (obj is System.Windows.Controls.ListViewItem listViewItem)
        {
            listViewItem.Focus();
            return;
        }
        e.Handled = true;
    }
}
