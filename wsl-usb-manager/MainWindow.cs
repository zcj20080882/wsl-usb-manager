/******************************************************************************
* SPDX-License-Identifier: MIT
* Project: wsl-usb-manager
* Class: MainWindow.cs
* NameSpace: wsl_usb_manager
* Author: Chuckie
* copyright: Copyright (c) Chuckie, 2024
* Description:
* Create Date: 2024/10/17 20:22
******************************************************************************/
using log4net;
using MaterialDesignThemes.Wpf;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using wsl_usb_manager.Controller;
using wsl_usb_manager.USBDevices;
using System.Windows.Forms;

namespace wsl_usb_manager;

public interface INotifyIconService
{
    void ShowNotification(string message);
    void ShowErrorMessage(string message);
}

public partial class MainWindow : Window, INotifyIconService
{
    private static readonly NotifyIcon notifyIcon = new();
    private static readonly ILog log = LogManager.GetLogger(typeof(MainWindow));

    private void MenuExitButton_OnClick(object sender, RoutedEventArgs e)
    {
        Environment.Exit(0);
    }


    private void MenuDarkModeButton_Click(object sender, RoutedEventArgs e)
        => ModifyTheme(DarkModeToggleButton.IsChecked == true);


    private static void ModifyTheme(bool isDarkTheme)
    {
        var paletteHelper = new PaletteHelper();
        var theme = paletteHelper.GetTheme();

        theme.SetBaseTheme(isDarkTheme ? BaseTheme.Dark : BaseTheme.Light);
        paletteHelper.SetTheme(theme);
    }

    private void InitNotifyIcon()
    {
        notifyIcon.Visible = true;
        notifyIcon.Icon = Properties.Resources.NotifyIcon;
        notifyIcon.Text = this.Title;

        notifyIcon.MouseDoubleClick += new MouseEventHandler(Show_Click);
        notifyIcon.ContextMenuStrip = new ContextMenuStrip();

        var showItem = new ToolStripMenuItem("Show");
        showItem.Click += new EventHandler(Show_Click);
        notifyIcon.ContextMenuStrip.Items.Add(showItem);

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += new EventHandler(Exit_Click);
        notifyIcon.ContextMenuStrip.Items.Add(exitItem);
    }

    private void Exit_Click(object? sender, EventArgs e)
    {
        Environment.Exit(0);
    }

    private void Show_Click(object? Sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;

        Show();
        Activate();
    }

    protected override void OnStateChanged(EventArgs e)
    {
        if (WindowState == WindowState.Minimized) Hide();

        base.OnStateChanged(e);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (App.GetAppConfig().CloseToTray)
        {
            e.Cancel = true;
            Hide();
            base.OnClosing(e);
        }
    }

    private void OnSelectedItemChanged(object sender, DependencyPropertyChangedEventArgs e)
        => MainScrollViewer.ScrollToHome();

    private void OnUSBEvent(object sender, USBEventArgs e)
    {
        if (string.IsNullOrEmpty(e.HardwareID))
        {
            log.Error("Invalid hardware id.");
            return;
        }

        if (string.Equals(e.HardwareID, USBMonitor.VBOX_USB_HARDWARE_ID, StringComparison.OrdinalIgnoreCase))
        {
            /**
             * This event is triggered by VBOX USB, ignore it.
             */
            log.Debug("Received VBOX USB connection event, ignore it.");
            return;
        }

        Dispatcher.InvokeAsync(async () =>
        {
            if (DataContext is not MainWindowViewModel vm)
            {
                log.Error("Cannot get MainWindowViewModel");
                return;
            }
            await vm.USBEventProcess(e);
        });
    }

    public void ShowNotification(string message)
    {
        if (!IsVisible)
        {
            notifyIcon.Visible = true;
            notifyIcon.ShowBalloonTip(5, Title, message, ToolTipIcon.Info);
        }
        else {
            MainSnackbar.MessageQueue?.Clear();
            MainSnackbar.MessageQueue?.Enqueue(message);
        }
    }

    public void ShowErrorMessage(string message)
    {
        if (!IsVisible)
        {
            notifyIcon.Visible = true;
            notifyIcon.ShowBalloonTip(5, Title, message, ToolTipIcon.Error);
        }
        else
        {
            System.Windows.MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
