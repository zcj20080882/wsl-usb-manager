/******************************************************************************
* SPDX-License-Identifier: MIT
* Project: wsl-usb-manager
* Class: UsbDeviceNotification.cs
* NameSpace: wsl_usb_manager.USBMonitor
* Author: Chuckie
* copyright: Copyright (c) Chuckie, 2025
* Description:
* Create Date: 2025/3/5 22:01
******************************************************************************/
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using log4net;
using System.Windows;
using System.Windows.Interop;
using Window = System.Windows.Window;

namespace wsl_usb_manager.USBMonitor;

public class UsbDeviceNotification(Window window)
{
    private static readonly ILog log = LogManager.GetLogger(typeof(UsbDeviceNotification));
    private readonly Guid GUID_DEVINTERFACE_USB_HOST_CONTROLLER = new("3ABF6F2D-71C4-462A-8A92-1E6861E6AF27");
    private readonly Guid GUID_DEVINTERFACE_USB_HUB = new("F18A0E88-C30C-11D0-8815-00A0C906BED8");
    private readonly Guid GUID_DEVINTERFACE_USB_DEVICE = new("A5DCBF10-6530-11D2-901F-00C04FB951ED");
    private IntPtr notificationHandle = IntPtr.Zero;
    private readonly Window window = window;
    private Dictionary<string, USBEventArgs> _deviceInfoCache = new Dictionary<string, USBEventArgs>();
    private USBEventHandler? UsbChangeEvent { set; get; }

    private const int DbtDeviceArrival = 0x8000; // system detected a new device
    private const int DbtDeviceRemoveComplete = 0x8004; // device is gone
    private const int WmDeviceChange = 0x0219; // device change event
    private const int DbchDevicetypeDeviceinterface = 5;
    private const int DeviceNotifyWindowHandle = 0x00000000;
    private const int DigcfPresent = 0x00000002;
    private const int DigcfAllclasses = 0x00000004;
    private const uint SpdrpDevicename = 0x0000000C;
    private const uint SpdrpDevicedesc = 0x00000000;
    private const uint SpdrpMfg = 0x0000000B;
    private const uint SpdrpHardwareId = 0x00000001;

    [StructLayout(LayoutKind.Sequential)]
    private struct DevBroadcastDeviceinterface
    {
        public int Size;
        public int DeviceType;
        public int Reserved;
        public Guid ClassGuid;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string Name;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SpDevinfoData
    {
        public int cbSize;
        public Guid ClassGuid;
        public int DevInst;
        public IntPtr Reserved;
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr RegisterDeviceNotification(IntPtr hRecipient, IntPtr notificationFilter, int flags);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool UnregisterDeviceNotification(IntPtr handle);

    [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetupDiGetClassDevs(IntPtr classGuid, string enumerator, IntPtr hwndParent, int flags);

    [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool SetupDiEnumDeviceInfo(IntPtr deviceInfoSet, int memberIndex, ref SpDevinfoData deviceInfoData);

    [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool SetupDiGetDeviceInstanceId(IntPtr deviceInfoSet, ref SpDevinfoData deviceInfoData, char[] deviceId, int deviceIdSize, out int requiredSize);

    [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool SetupDiGetDeviceRegistryProperty(IntPtr deviceInfoSet, ref SpDevinfoData deviceInfoData, uint property, out uint propertyRegDataType, byte[] propertyBuffer, int propertyBufferSize, out int requiredSize);


    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmDeviceChange)
        {
            if (lParam != IntPtr.Zero)
            {

                var devBroadcastDeviceInterface = (DevBroadcastDeviceinterface)Marshal.PtrToStructure(lParam, typeof(DevBroadcastDeviceinterface))!;
                var devicePath = new string(devBroadcastDeviceInterface.Name).TrimEnd('\0');
                var eventArgs = new USBEventArgs();

                switch ((int)wParam)
                {
                    case DbtDeviceArrival:
                        eventArgs.IsConnected = true;
                        break;
                    case DbtDeviceRemoveComplete:
                        eventArgs.IsConnected = false;
                        break;
                }
                log.Info("connect: ");
                UsbChangeEvent?.Invoke(eventArgs);
            }
            else
            {
                log.Error("WParam from Message is null.");
            }
        }
        return IntPtr.Zero;
    }

    private USBEventArgs? GetUsbDeviceEventArgs(string devicePath)
    {
        var deviceInfoSet = SetupDiGetClassDevs(IntPtr.Zero, "USB", IntPtr.Zero, DigcfPresent | DigcfAllclasses);
        var deviceInfoData = new SpDevinfoData();
        deviceInfoData.cbSize = Marshal.SizeOf(deviceInfoData);

        for (int i = 0; SetupDiEnumDeviceInfo(deviceInfoSet, i, ref deviceInfoData); i++)
        {
            var deviceId = new char[256];
            if (SetupDiGetDeviceInstanceId(deviceInfoSet, ref deviceInfoData, deviceId, deviceId.Length, out _))
            {
                var deviceIdStr = new string(deviceId).TrimEnd('\0');
                if (deviceIdStr.Equals(devicePath, StringComparison.OrdinalIgnoreCase))
                {
                    var hardwareId = GetDeviceProperty(deviceInfoSet, ref deviceInfoData, SpdrpHardwareId);

                    var vid = ExtractVid(hardwareId);
                    var pid = ExtractPid(hardwareId);

                    return new USBEventArgs
                    {
                        Name = GetDeviceProperty(deviceInfoSet, ref deviceInfoData, SpdrpDevicename),
                        Description = GetDeviceProperty(deviceInfoSet, ref deviceInfoData, SpdrpDevicedesc),
                        Manufacturer = GetDeviceProperty(deviceInfoSet, ref deviceInfoData, SpdrpMfg),
                        HardwareID = $"{vid}:{pid}",
                    };
                }
            }
        }

        return null;
    }

    private static string GetDeviceProperty(IntPtr deviceInfoSet, ref SpDevinfoData deviceInfoData, uint property)
    {
        var buffer = new byte[256];
        if (SetupDiGetDeviceRegistryProperty(deviceInfoSet, ref deviceInfoData, property, out _, buffer, buffer.Length, out _))
        {
            return Encoding.Unicode.GetString(buffer).TrimEnd('\0');
        }
        return string.Empty;
    }

    private static string? ExtractVid(string hardwareId)
    {
        var match = Regex.Match(hardwareId, @"VID_([0-9A-F]{4})", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : null;
    }

    private string? ExtractPid(string hardwareId)
    {
        var match = Regex.Match(hardwareId, @"PID_([0-9A-F]{4})", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : null;
    }


    public bool RegisterUSBEvent(USBEventHandler eventHandler)
    {
        var dbi = new DevBroadcastDeviceinterface
        {
            DeviceType = DbchDevicetypeDeviceinterface,
            Reserved = 0,
            ClassGuid = GUID_DEVINTERFACE_USB_HOST_CONTROLLER,
            Name = string.Empty
        };
        dbi.Size = Marshal.SizeOf(dbi);
        var buffer = Marshal.AllocHGlobal(dbi.Size);
        Marshal.StructureToPtr(dbi, buffer, true);

        if (PresentationSource.FromVisual(window) is HwndSource source)
        {
            source.AddHook(WndProc);
            notificationHandle = RegisterDeviceNotification(source.Handle, buffer, DeviceNotifyWindowHandle);
            UsbChangeEvent = eventHandler;
        }
        log.Error("Failed to get window handle.");
        return false;
    }

    public void UnregisterUSBEvent()
    {
        UsbChangeEvent = null;
        if (notificationHandle != IntPtr.Zero)
            UnregisterDeviceNotification(notificationHandle);
    }
}
