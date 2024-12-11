using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using log4net;

namespace wsl_usb_manager.Controller;

public class UsbDeviceEventArgs : EventArgs
{
    public string? Caption { get; set; }
    public string? Name { get; set; }
    public string? HardwareID { get; set; }
    public string? Description { get; set; }
    public string? ClassGuid { get; set; }
    public string? PNPDeviceID { get; set; }
    public string? DeviceType { get; set; }
    public string? Status { get; set; }
    public bool IsConnected { get; set; }
    public string? Manufacturer { get; set; }
}

public class UsbDeviceNotification
{
    private static readonly ILog log = LogManager.GetLogger(typeof(UsbDeviceNotification));
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
    private const uint SpdrpClass = 0x00000007;
    private readonly Guid GUID_DEVINTERFACE_USB_HOST_CONTROLLER = new("3ABF6F2D-71C4-462A-8A92-1E6861E6AF27");
    private readonly Guid GUID_DEVINTERFACE_USB_HUB = new("F18A0E88-C30C-11D0-8815-00A0C906BED8");
    private readonly Guid GUID_DEVINTERFACE_USB_DEVICE = new("A5DCBF10-6530-11D2-901F-00C04FB951ED");
    private IntPtr notificationHandle;

    public event EventHandler<UsbDeviceEventArgs>? DeviceChanged = null;

    public void RegisterDeviceNotification(IntPtr windowHandle)
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
        notificationHandle = RegisterDeviceNotification(windowHandle, buffer, DeviceNotifyWindowHandle);
    }

    public void UnregisterDeviceNotification()
    {
        UnregisterDeviceNotification(notificationHandle);
    }

    public void WndProc(ref Message m)
    {
        if (m.Msg == WmDeviceChange)
        {
            if (m.LParam != IntPtr.Zero)
            {
                var devBroadcastDeviceInterface = (DevBroadcastDeviceinterface)Marshal.PtrToStructure(m.LParam, typeof(DevBroadcastDeviceinterface))!;
                var devicePath = new string(devBroadcastDeviceInterface.Name).TrimEnd('\0');
                UsbDeviceEventArgs? eventArgs = GetUsbDeviceEventArgs(devicePath);
                if(eventArgs == null)
                {
                    log.Error($"Failed to get info of '{devBroadcastDeviceInterface.Name}'.");
                    return;
                }
                switch ((int)m.WParam)
                {
                    case DbtDeviceArrival:
                        eventArgs.IsConnected = true;
                        break;
                    case DbtDeviceRemoveComplete:
                        eventArgs.IsConnected = false;
                        break;
                    default:
                        log.Warn($"Unknown WParam: {m.WParam}");
                        return;
                }
                DeviceChanged?.Invoke(this, eventArgs);
            }
            else
            {
                log.Error("WParam from Message is null.");
            }
        }
    }

    private UsbDeviceEventArgs? GetUsbDeviceEventArgs(string devicePath)
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
                    var deviceName = GetDeviceProperty(deviceInfoSet, ref deviceInfoData, SpdrpDevicename);
                    var deviceDescription = GetDeviceProperty(deviceInfoSet, ref deviceInfoData, SpdrpDevicedesc);
                    var deviceManufacturer = GetDeviceProperty(deviceInfoSet, ref deviceInfoData, SpdrpMfg);
                    var hardwareId = GetDeviceProperty(deviceInfoSet, ref deviceInfoData, SpdrpHardwareId);
                    var deviceType = GetDeviceProperty(deviceInfoSet, ref deviceInfoData, SpdrpClass);

                    var vid = ExtractVid(hardwareId);
                    var pid = ExtractPid(hardwareId);

                    return new UsbDeviceEventArgs
                    {
                        
                        PNPDeviceID = deviceIdStr,
                        Name = deviceName,
                        Description = deviceDescription,
                        Manufacturer = deviceManufacturer,
                        HardwareID = hardwareId,
                        DeviceType = deviceType,
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
}
