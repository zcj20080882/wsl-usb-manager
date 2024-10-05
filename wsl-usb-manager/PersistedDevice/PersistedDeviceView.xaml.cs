/******************************************************************************
* SPDX-License-Identifier: MIT
* Project: wsl-usb-manager
* Class: PersistedDeviceView.xaml.cs
* NameSpace: wsl_usb_manager.PersistedDevice
* Author: Chuckie
* copyright: Copyright (c) Chuckie, 2024
* Description:
* Create Date: 2024/10/3 20:21
******************************************************************************/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using wsl_usb_manager.USBDevices;
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

    private void MenuItem_Click(object sender, RoutedEventArgs e)
    {
        List<USBDeviceInfoModel> devicelist = [];
        if (sender is System.Windows.Controls.MenuItem item && DataContext is PersistedDeviceViewModel dm)
        {
            switch (item.Name)
            {
                case "MenuItemDeleteOne":
                    if (dm.SelectedDevice is USBDeviceInfoModel device)
                    {
                        devicelist.Add(device);
                    }
                    else
                        MessageBox.Show("No device is selected.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    break;
                case "MenuItemDeleteAll":
                    if (dm.DevicesItems != null && dm.DevicesItems.Count > 0)
                    {
                        foreach (var d in dm.DevicesItems)
                        {
                            devicelist.Add(d);
                        }
                    }
                    break;
                default:
                    break;
            }
            if (devicelist.Count > 0)
            {
                dm?.DeletePersistedDevices(devicelist);
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
