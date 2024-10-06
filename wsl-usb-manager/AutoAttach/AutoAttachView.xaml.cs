/******************************************************************************
* SPDX-License-Identifier: MIT
* Project: wsl-usb-manager
* Class: AutoAttachView.xaml.cs
* NameSpace: wsl_usb_manager.AutoAttach
* Author: Chuckie
* copyright: Copyright (c) Chuckie, 2024
* Description:
* Create Date: 2024/10/17 20:31
******************************************************************************/
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using wsl_usb_manager.Domain;
using wsl_usb_manager.PersistedDevice;
using MessageBox = System.Windows.MessageBox;
using UserControl = System.Windows.Controls.UserControl;

namespace wsl_usb_manager.AutoAttach
{
    /// <summary>
    /// AutoAttachView.xaml 的交互逻辑
    /// </summary>
    public partial class AutoAttachView : UserControl
    {
        public AutoAttachView()
        {
            InitializeComponent();
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            List<USBDeviceInfoModel> devicelist = [];
            if (sender is System.Windows.Controls.MenuItem item && DataContext is AutoAttachViewModel dm)
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
                    dm?.RemoveDeviceFromAutoAttachProfile(devicelist);
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
}
