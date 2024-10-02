/******************************************************************************
* SPDX-License-Identifier: MIT
* Project: wsl-usb-manager
* Class: MainWindow.cs
* NameSpace: wsl_usb_manager
* Author: Chuckie
* copyright: Copyright (c) Chuckie, 2024
* Description:
* Create Date: 2024/10/2 19:00
******************************************************************************/
using MaterialDesignThemes.Wpf;
using System.ComponentModel;
using System.Windows;
using wsl_usb_manager.Controller;

namespace wsl_usb_manager;

public partial class MainWindow : Window
{
    private NotifyIcon notifyIcon = new();

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

    private void initNotifyIcon()
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
        e.Cancel = true;
        Hide();
        base.OnClosing(e);
    }

    private void OnSelectedItemChanged(object sender, DependencyPropertyChangedEventArgs e)
        => MainScrollViewer.ScrollToHome();

    private void OnUSBEvent(object sender, USBEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            string msg = "Device ";
            if (e.Name != null)
            {
                msg += e.Name + "(" + e.VID + ":" + e.PID + ")";
            }
            else
            {
                msg += e.VID + ":" + e.PID;
            }
            msg += " ";
            if (e.IsConnected)
            {
                msg += "connected";
            }
            else
            {
                msg += "disconnected";
            }
            msg += ".";
            //if (NotificationView.MessageQueue is { } messageQueue)
            //{
            //    //the message queue can be called from any thread
            //    Task.Factory.StartNew(() => messageQueue.Enqueue(msg));
            //}
        });
    }
}
