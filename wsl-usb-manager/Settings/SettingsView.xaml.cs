/******************************************************************************
* SPDX-License-Identifier: MIT
* Project: wsl-usb-manager
* Class: SettingsView.xaml.cs
* NameSpace: wsl_usb_manager.Settings
* Author: Chuckie
* copyright: Copyright (c) Chuckie, 2024
* Description:
* Create Date: 2024/10/17 20:22
******************************************************************************/
using System.Windows;

namespace wsl_usb_manager.Settings
{
    /// <summary>
    /// SettingsView.xaml 的交互逻辑
    /// </summary>
    public partial class SettingsView : System.Windows.Controls.UserControl
    {
        public SettingsView()
        {
            InitializeComponent();
        }

        public SettingsView(SettingViewModel dataContext)
        {
            InitializeComponent();
            DataContext = dataContext;
        }

        private void ButtonOK_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is SettingViewModel viewModel)
            {
                viewModel.SaveConfig();
            }
        }

        private void CheckboxCheckboxSpecifyNetCard_Unchecked(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.CheckBox && DataContext is SettingViewModel viewModel)
            {
                viewModel.SelectedForwardNetCard = null;
            }
        }
    }
}
