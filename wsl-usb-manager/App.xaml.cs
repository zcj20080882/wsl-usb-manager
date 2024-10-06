/******************************************************************************
* SPDX-License-Identifier: MIT
* Project: wsl-usb-manager
* Class: App.xaml.cs
* NameSpace: wsl_usb_manager
* Author: Chuckie
* copyright: Copyright (c) Chuckie, 2024
* Description:
* Create Date: 2024/10/17 20:22
******************************************************************************/

// Ignore Spelling: App

using System.Diagnostics.Metrics;
using System.Threading;
using System.Windows;

//[assembly: XmlConfigurator(ConfigFile = "Log4Net.config", Watch = true)]

namespace wsl_usb_manager
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        private static readonly Mutex mutex = new(true, "WSL-USB-Manager-1ddc73e5-c499-484f-b663-87edaee7bfdc");
        protected override void OnStartup(StartupEventArgs e)
        {
            if (!mutex.WaitOne(TimeSpan.Zero, true))
            {
                System.Windows.MessageBox.Show("Another instance of the app is already running,\r\ncheck system tray icon to restore instance.");
                Shutdown();
                return;
            }

            InitConfiguration();
            log4net.Config.XmlConfigurator.Configure();
            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            mutex.ReleaseMutex();
            base.OnExit(e);
        }
    }

}
