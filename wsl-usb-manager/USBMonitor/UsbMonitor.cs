/******************************************************************************
* SPDX-License-Identifier: MIT
* Project: wsl-usb-manager
* Class: UsbMonitor.cs
* NameSpace: wsl_usb_manager.USBMonitor
* Author: Chuckie
* copyright: Copyright (c) Chuckie, 2025
* Description:
* Create Date: 2025/3/6 20:40
******************************************************************************/
using log4net;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using wsl_usb_manager.USBIPD;

namespace wsl_usb_manager.USBMonitor;

public partial class UsbMonitor
{
    private static readonly ILog log = LogManager.GetLogger(typeof(UsbMonitor));
    private readonly ManagementEventWatcher usbInsertWatcher;
    private readonly ManagementEventWatcher usbRemoveWatcher;
    public const string VBOX_USB_HARDWARE_ID = "80EE:CAFE";
    private const string ClassName = "Win32_USBControllerDevice";
    private const string WqlEventQueryCondition = $"TargetInstance ISA '{ClassName}'";

    private USBEventHandler? UsbChangeEvent { set; get; }
    private Dictionary<string, USBEventArgs> usbDeviceCache = [];
    [GeneratedRegex(@"VID_([0-9a-fA-F]{4})(.*?)PID_([0-9a-fA-F]{4})")]
    private static partial Regex USBVIDPIDRegex();

    public UsbMonitor(USBEventHandler eventHandler)
    {
        // Bind to local machine
        var options = new ConnectionOptions { EnablePrivileges = true };
        var scope = new ManagementScope(@"root\CIMV2", options);

        var insertQuery = new WqlEventQuery
        {
            EventClassName = "__InstanceCreationEvent",
            WithinInterval = TimeSpan.FromMilliseconds(50),
            Condition = WqlEventQueryCondition
        };

        var removeQuery = new WqlEventQuery
        {
            EventClassName = "__InstanceDeletionEvent",
            WithinInterval = TimeSpan.FromMilliseconds(50),
            Condition = WqlEventQueryCondition
        };


        this.usbInsertWatcher = new ManagementEventWatcher(scope, insertQuery);
        this.usbRemoveWatcher = new ManagementEventWatcher(scope, removeQuery);

        this.usbInsertWatcher.EventArrived += (sender, e) =>
        {
            this.usbInsertWatcher.Stop();
            if (e.NewEvent["TargetInstance"] is ManagementBaseObject mbo)
            {
                this.UsbChangeEvent?.Invoke(ConvertToUSBEventArgs(mbo, true));
            }
                
            this.usbInsertWatcher.Start();
        };
        this.usbRemoveWatcher.EventArrived += (sender, e) =>
        {
            this.usbRemoveWatcher.Stop();
            if (e.NewEvent["TargetInstance"] is ManagementBaseObject mbo)
            {
                this.UsbChangeEvent?.Invoke(ConvertToUSBEventArgs(mbo, false));
            }
            this.usbRemoveWatcher.Start();
        };
        this.UsbChangeEvent += eventHandler;
    }

    private USBEventArgs ConvertToUSBEventArgs(ManagementBaseObject mbo, bool isConnected)
    {
        USBEventArgs usbEventArgs = new()
        {
            IsConnected = isConnected
        };

        // Get the Dependent property, which is a reference to the Win32_PnPEntity instance
        string dependent = mbo["Dependent"]?.ToString() ?? string.Empty;

        if (string.IsNullOrEmpty(dependent))
        {
            log.Error("Failed to get dependent property");
            return usbEventArgs;
        }
        string deviceId = dependent.Split('=')[1].Trim('"');

        Match match = USBVIDPIDRegex().Match(deviceId);
        if (match.Success)
        {
            usbEventArgs.HardwareID = $"{match.Groups[1].Value}:{match.Groups[3].Value}";
        }
        else
        {
            //log.Error($"Failed to get device id from {deviceId}");
            return usbEventArgs;
        }
        log.Debug($"{usbEventArgs.HardwareID} is {(usbEventArgs.IsConnected ? "connected" : "disconnected")}");

        return usbEventArgs;
    }

   

    public void Stop()
    {
        this.usbInsertWatcher.Stop();
        this.usbRemoveWatcher.Stop();
    }

    public void Start()
    {
        //Task.Run(() => {
        //    CreateUsbEventArgsCache();
        //});
        this.usbInsertWatcher.Start();
        this.usbRemoveWatcher.Start();
    }
}
