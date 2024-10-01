using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Management;
using static System.Formats.Asn1.AsnWriter;
using System.Collections;
using System.Windows.Threading;
using System.Text.RegularExpressions;

namespace wsl_usb_manager.Controller;

public class USBEventArgs : EventArgs
{
    public string Name { get; set; } = string.Empty;
    public string VID { get; set; } = string.Empty;
    public string PID { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty; 
    public string ClassGuid { get; set; } = string.Empty;
    public string PNPDeviceID { get; set; } = string.Empty;
    public string Service { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsConnected { get; set; }
}

public delegate void USBEventHandler(object sender, USBEventArgs e);

public class USBMonitor
{
    private ManagementEventWatcher usbInsertWatcher;
    private ManagementEventWatcher usbRemoveWatcher;
    private USBEventHandler? usbChangeEvent { set; get; }
    public USBMonitor(USBEventHandler eventHandler)
    {
        // Bind to local machine
        var options = new ConnectionOptions { EnablePrivileges = true };
        var scope = new ManagementScope(@"root\CIMV2", options);

        var insertQuery = new WqlEventQuery
        {
            EventClassName = "__InstanceCreationEvent",
            WithinInterval = TimeSpan.FromMilliseconds(500),
            Condition = @"TargetInstance ISA 'Win32_USBControllerDevice'"
        };

        var removeQuery = new WqlEventQuery
        {
            EventClassName = "__InstanceDeletionEvent",
            WithinInterval = TimeSpan.FromMilliseconds(500),
            Condition = @"TargetInstance ISA 'Win32_USBControllerDevice'"
        };


        this.usbInsertWatcher = new ManagementEventWatcher(scope, insertQuery);
        this.usbRemoveWatcher = new ManagementEventWatcher(scope, removeQuery);

        this.usbInsertWatcher.EventArrived += (sender, e) => {
            this.usbInsertWatcher.Stop();
            this.usbChangeEvent?.Invoke(this, convertToUSBEventArgs(e));
            this.usbInsertWatcher.Start();
        };
        this.usbRemoveWatcher.EventArrived += (sender, e) => {
            this.usbRemoveWatcher.Stop();
            this.usbChangeEvent?.Invoke(this, convertToUSBEventArgs(e));
            this.usbRemoveWatcher.Start();
        };
        this.usbChangeEvent += eventHandler;
    }

    private USBEventArgs convertToUSBEventArgs(EventArrivedEventArgs e)
    {
        USBEventArgs usbEventArgs = new();
        if(e.NewEvent.ClassPath.ClassName == "__InstanceCreationEvent")
        {
            usbEventArgs.IsConnected = true;
        }
        else if (e.NewEvent.ClassPath.ClassName == "__InstanceDeletionEvent")
        {
            usbEventArgs.IsConnected = false;
        }
        if (e.NewEvent["TargetInstance"] is ManagementBaseObject mbo && mbo.ClassPath.ClassName == "Win32_USBControllerDevice")
        {
            string Dependent = (mbo["Dependent"] as string).Split(new char[] { '=' })[1];
            Match match = Regex.Match(Dependent, @"VID_([0-9a-fA-F]{4})(.*?)PID_([0-9a-fA-F]{4})");
            if (match.Success)
            {
                usbEventArgs.VID = match.Groups[1].Value;
                usbEventArgs.PID = match.Groups[3].Value;

                ManagementObjectCollection PnPEntityCollection = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity WHERE DeviceID=" + Dependent).Get();
                if (PnPEntityCollection != null)
                {
                    foreach (ManagementObject Entity in PnPEntityCollection)
                    {
                        usbEventArgs.ClassGuid = Entity["ClassGuid"] as string;
                        usbEventArgs.Description = Entity["Description"] as string;
                        usbEventArgs.Name = Entity["Name"] as string;

                        usbEventArgs.PNPDeviceID = Entity["PNPDeviceID"] as string;  // 设备ID
                        usbEventArgs.Service = Entity["Service"] as string;          // 服务
                        usbEventArgs.Status = Entity["Status"] as string;            // 设备状态
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
