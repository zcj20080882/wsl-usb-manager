/******************************************************************************
* SPDX-License-Identifier: MIT
* Project: wsl-usb-manager
* Class: MainWindow.cs
* NameSpace: wsl_usb_manager
* Author: Chuckie
* copyright: Copyright (c) Chuckie, 2025
* Description:
* Create Date: 2024/10/17 20:22
******************************************************************************/
using log4net;
using MaterialDesignThemes.Wpf;
using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using wsl_usb_manager.Domain;
using wsl_usb_manager.MessageBox;
using wsl_usb_manager.USBIPD;
using wsl_usb_manager.USBMonitor;

namespace wsl_usb_manager;

public partial class MainWindow : Window, INotifyService
{
    private static readonly NotifyIcon notifyIcon = new();
    private static readonly ILog log = LogManager.GetLogger(typeof(MainWindow));
    private static UsbMonitor? usbMonitor;
    //private UsbDeviceNotification? usbDeviceNotification;
    private bool USBEventProcessing { get; set; } = false;

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

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        usbMonitor = new UsbMonitor(OnUSBEvent);
        usbMonitor.Start();
        //usbDeviceNotification = new UsbDeviceNotification(this);
        //usbDeviceNotification.RegisterUSBEvent(OnUSBEvent);
    }

    
    private async void MenuExitButton_OnClick(object sender, RoutedEventArgs e)
    {
        await USBIPDWin.StopAllProcesses();
        usbMonitor?.Stop();
        Environment.Exit(0);
    }


    private void MenuDarkModeButton_Click(object sender, RoutedEventArgs e)
        => ModifyTheme(DarkModeToggleButton.IsChecked == true);


    private void Exit_Click(object? sender, EventArgs e)
    {
        Environment.Exit(0);
    }

    private void Show_Click(object? Sender, EventArgs e)
    {
        Show();
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;        
        Topmost = true;  // Ensure the window is on top
        Activate();
        Topmost = false; // Reset the Topmost property
    }

    private void OnSelectedItemChanged(object sender, DependencyPropertyChangedEventArgs e)
        => MainScrollViewer.ScrollToHome();

    private static void ModifyTheme(bool isDarkTheme)
    {
        var paletteHelper = new PaletteHelper();
        var theme = paletteHelper.GetTheme();

        theme.SetBaseTheme(isDarkTheme ? BaseTheme.Dark : BaseTheme.Light);
        paletteHelper.SetTheme(theme);
    }

    private void InitNotifition()
    {
        notifyIcon.Visible = true;
        notifyIcon.Icon = Properties.Resources.NotifyIcon;
        notifyIcon.Text = this.Title;

        notifyIcon.MouseDoubleClick += new MouseEventHandler(Show_Click);
        notifyIcon.BalloonTipClicked += Show_Click;
        notifyIcon.ContextMenuStrip = new ContextMenuStrip();

        var showItem = new ToolStripMenuItem("Show");
        showItem.Click += new EventHandler(Show_Click);
        notifyIcon.ContextMenuStrip.Items.Add(showItem);

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += new EventHandler(Exit_Click);
        notifyIcon.ContextMenuStrip.Items.Add(exitItem);
        NotifyService.RegisterNotifyService(this);
    }
    
    private void OnUSBEvent(USBEventArgs e)
    {
        if (USBEventProcessing)
        {
            log.Warn("USB event is processing, ignore this event.");
            return;
        }
        if (string.IsNullOrEmpty(e.HardwareID))
        {
            log.Debug("hardware id is empty.");
            return;
        }

        USBEventProcessing = true;
        _ = Dispatcher.Invoke(async () =>
        {
            if (DataContext is not MainWindowViewModel vm)
            {
                log.Error("Cannot get MainWindowViewModel");
                return;
            }
            await vm.USBEventProcess(e);
        });
        USBEventProcessing = false;
    }

    public void ShowNotification(string message)
    {
        log.Info(message);
        if (!IsVisible)
        {
            notifyIcon.Visible = true;
            notifyIcon.ShowBalloonTip(5, Title, message, ToolTipIcon.Info);
        }
        else
        {
            MainSnackbar.MessageQueue?.Clear();
            MainSnackbar.MessageQueue?.Enqueue(message);
        }
    }

    public void ShowErrorMessage(string message)
    {
        log.Error(message);
        if (!IsVisible)
        {
            notifyIcon.Visible = true;
            notifyIcon.ShowBalloonTip(5, Title, message, ToolTipIcon.Error);
        }
        else
        {
            var view = new MessageBoxView(MessageType.Info, "Error", message);
            if (view != null)
            {
                DialogHost.Show(view, "RootDialog");
            }
        }
    }

    public void DisableWindow()
    {
        if(DataContext is MainWindowViewModel vm)
        {
            vm.DisableWindow();
        }
    }
    
    public void EnableWindow()
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.EnableWindow();
        }
    }
}
