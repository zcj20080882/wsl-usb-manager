/******************************************************************************
* SPDX-License-Identifier: MIT
* Project: wsl-usb-manager
* Class: USBMonitor.cs
* NameSpace: wsl_usb_manager.Controller
* Author: Chuckie
* copyright: Copyright (c) Chuckie, 2024
* Description:
* Create Date: 2024/10/17 20:22
******************************************************************************/
using System.Management;
using System.Text.RegularExpressions;

namespace wsl_usb_manager.Controller;

public class USBEventArgs : EventArgs
{
    public string? Name { get; set; }
    public string? HardwareID { get; set; }
    public string? Description { get; set; }
    public string? ClassGuid { get; set; }
    public string? PNPDeviceID { get; set; }
    public string? Service { get; set; }
    public string? Status { get; set; }
    public bool IsConnected { get; set; }
}

public delegate void USBEventHandler(object sender, USBEventArgs e);

public partial class USBMonitor
{
    private readonly ManagementEventWatcher usbInsertWatcher;
    private readonly ManagementEventWatcher usbRemoveWatcher;
    public const string VBOX_USB_HARDWARE_ID = "80EE:CAFE";
    private const string WqlEventQueryCondition = @"TargetInstance ISA 'Win32_USBControllerDevice' AND NOT TargetInstance.Dependent LIKE '%HUB%'";
    private USBEventHandler? UsbChangeEvent { set; get; }

    [GeneratedRegex(@"VID_([0-9a-fA-F]{4})(.*?)PID_([0-9a-fA-F]{4})")]
    private static partial Regex USBVIDPIDRegex();

    public USBMonitor(USBEventHandler eventHandler)
    {
        // Bind to local machine
        var options = new ConnectionOptions { EnablePrivileges = true };
        var scope = new ManagementScope(@"root\CIMV2", options);

        var insertQuery = new WqlEventQuery
        {
            EventClassName = "__InstanceCreationEvent",
            WithinInterval = TimeSpan.FromMilliseconds(200),
            Condition = WqlEventQueryCondition
        };

        var removeQuery = new WqlEventQuery
        {
            EventClassName = "__InstanceDeletionEvent",
            WithinInterval = TimeSpan.FromMilliseconds(200),
            Condition = WqlEventQueryCondition
        };


        this.usbInsertWatcher = new ManagementEventWatcher(scope, insertQuery);
        this.usbRemoveWatcher = new ManagementEventWatcher(scope, removeQuery);

        this.usbInsertWatcher.EventArrived += (sender, e) =>
        {
            this.usbInsertWatcher.Stop();
            this.UsbChangeEvent?.Invoke(this, ConvertToUSBEventArgs(e));
            this.usbInsertWatcher.Start();
        };
        this.usbRemoveWatcher.EventArrived += (sender, e) =>
        {
            this.usbRemoveWatcher.Stop();
            this.UsbChangeEvent?.Invoke(this, ConvertToUSBEventArgs(e));
            this.usbRemoveWatcher.Start();
        };
        this.UsbChangeEvent += eventHandler;
    }

    private static USBEventArgs ConvertToUSBEventArgs(EventArrivedEventArgs e)
    {
        USBEventArgs usbEventArgs = new();
        if (e.NewEvent.ClassPath.ClassName == "__InstanceCreationEvent")
        {
            usbEventArgs.IsConnected = true;
        }
        else if (e.NewEvent.ClassPath.ClassName == "__InstanceDeletionEvent")
        {
            usbEventArgs.IsConnected = false;
        }
        if (e.NewEvent["TargetInstance"] is ManagementBaseObject mbo && mbo.ClassPath.ClassName == "Win32_USBControllerDevice")
        {
            string Dependent = ((string)mbo["Dependent"]).Split(['='])[1];
            Match match = USBVIDPIDRegex().Match(Dependent);
            if (match.Success)
            {
                usbEventArgs.HardwareID = $"{match.Groups[1].Value}:{match.Groups[3].Value}";

                ManagementObjectCollection PnPEntityCollection = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity WHERE DeviceID=" + Dependent).Get();
                if (PnPEntityCollection != null)
                {
                    foreach (ManagementObject Entity in PnPEntityCollection.Cast<ManagementObject>())
                    {
                        usbEventArgs.ClassGuid = (string)Entity["ClassGuid"];
                        usbEventArgs.Description = (string)Entity["Description"];
                        usbEventArgs.Name = (string)Entity["Name"];

                        usbEventArgs.PNPDeviceID = (string)Entity["PNPDeviceID"];
                        usbEventArgs.Service = (string)Entity["Service"];
                        usbEventArgs.Status = (string)Entity["Status"];
                    }
                }
            }
        }

        return usbEventArgs;
    }

    public void Stop()
    {
        this.usbInsertWatcher.Stop();
        this.usbRemoveWatcher.Stop();
    }

    public void Start()
    {
        this.usbInsertWatcher.Start();
        this.usbRemoveWatcher.Start();
    }
}
