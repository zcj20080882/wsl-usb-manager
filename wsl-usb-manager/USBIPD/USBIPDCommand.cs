using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace wsl_usb_manager.USBIPD;

public static partial class USBIPDWin
{
    private const string USBIPDIsBlockedZh = "防火墙阻止了来自WSL中的连接请求.";
    private const string USBIPDNoWSLRunning = "未检测到正在运行的WSL2分发版，请保持WSL2分发版处于运行状态。";
    private const string USBDeviceInUsingZh = "该设备貌似正在被Windows使用中，请停止使用该设备的服务，或者强制绑定该设备后再附加。";
    private static bool IsDeviceInAutoAttaching(string id)
    {
        foreach (Process p in ProcessList)
        {
            if (p.StartInfo.Arguments.Contains("attach") && p.StartInfo.Arguments.Contains(id))
            {
                if (!p.HasExited)
                {
                    log.Info($"'Device {id}' is in auto-attaching!");
                    return true;
                }
                log.Warn($"'usbipd.exe {p.StartInfo.Arguments}' has exited, removing it ...");
                ProcessList.Remove(p);
                break;
            }
        }
        return false;
    }

    private static void StopAutoAttachDevice(string id)
    {
        foreach (Process p in ProcessList)
        {
            if (p.StartInfo.Arguments.Contains("attach") && p.StartInfo.Arguments.Contains(id))
            {
                if (!p.HasExited)
                {
                    log.Info($"'Stopping auto-attached Device {id}' ...");
                    ForceStopProcess(p);
                }
                else
                {
                    log.Warn($"'usbipd.exe {p.StartInfo.Arguments}' has exited, removing it ...");
                }
                ProcessList.Remove(p);
                break;
            }
        }
    }
    private static List<USBDevice>? ParseStringDevInfoToUSBDeviceList(string stringDeviceInfo)
    {
        List<USBDevice> devList = [];
        string pattern = @"\r?\n\s*\r?\n";
        string[] blocks = Regex.Split(stringDeviceInfo.Trim(['\r', '\n', ' ', '\t']), pattern);

        foreach (string block in blocks)
        {
            if (DeviceInfoRegex().Match(block.Trim(['\r', '\n', ' ', '\t'])) is Match match)
            {
                string busId = match.Groups["BusId"].Value.Trim();
                if (!bool.TryParse(match.Groups["IsForced"].Value.Trim(), out bool isForced))
                {
                    isForced = false;
                    log.Warn("Failed to parse IsForced");
                }

                if (!bool.TryParse(match.Groups["IsBound"].Value.Trim(), out bool isBound))
                {
                    isBound = false;
                    log.Warn("Failed to parse IsBound");
                }

                if (!bool.TryParse(match.Groups["IsAttached"].Value.Trim(), out bool isAttached))
                {
                    isAttached = false;
                    log.Warn("Failed to parse IsAttached");
                }
                USBDevice deviceInfo = new()
                {
                    InstanceId = match.Groups["InstanceId"].Value.Trim(),
                    HardwareId = match.Groups["HardwareId"].Value.Trim(),
                    Description = match.Groups["Description"].Value.Trim(),
                    IsForced = isForced,
                    BusId = busId,
                    PersistedGuid = match.Groups["PersistedGuid"].Value.Trim(),
                    StubInstanceId = match.Groups["StubInstanceId"].Value.Trim(),
                    ClientIPAddress = match.Groups["ClientIPAddress"].Value.Trim(),
                    IsBound = isBound,
                    IsConnected = !string.IsNullOrWhiteSpace(busId),
                    IsAttached = isAttached
                };

                devList.Add(deviceInfo);
            }
        }
        return devList;
    }

    /**
     * Bind a device with a hardware ID.
     */
    public static async Task<(ErrorCode ErrCode, string ErrMsg)>
        BindDevice(string id, bool useBusID, bool force)
    {
        string stderr = "";
        var (ErrCode, _, StandardError) = await RunUSBIPDWin(true, ["bind", (useBusID ? "--busid" : "--hardware-id"), id, (force ? "--force" : "")]);
        if (IsFailed(ErrCode))
        {
            log.Error($"Failed to bind device '{id}': {StandardError}");
            stderr = StandardError;
        }
        return new(ErrCode, stderr);
    }


    public static async Task<(ErrorCode ErrCode, string ErrMsg)>
        BindDevice(string id, bool useBusID) => await BindDevice(id, useBusID, false);


    public static async Task<(ErrorCode ErrCode, string ErrMsg)>
        UnbindDevice(string? id, bool? useBusID)
    {
         var (ErrCode, _, StandardError) = await RunUSBIPDWin(
            true,
            string.IsNullOrEmpty(id)
                ? ["unbind", "--all"]
                : ["unbind", (useBusID.HasValue && useBusID.Value ? "--busid" : "--hardware-id"), id]
        );
        if (IsFailed(ErrCode))
        {
            log.Warn($"Failed to unbind device '{id}': {StandardError}");
        }

        return new(ErrCode, StandardError);
    }

    public static async Task<(ErrorCode ErrCode, string ErrMsg)>
        UnbindAllDevice() => await UnbindDevice(null, false);

    /// <summary>
    /// ID(Bus ID or hardware ID) has already been checked, and the server is running.
    /// </summary>
    public static async Task<(ErrorCode ErrCode, string ErrMsg)>
        Attach(string id, bool useBusID, bool autoAttach, string? hostIP)
    {
        if (IsDeviceInAutoAttaching(id))
        {
            return new(ErrorCode.DeviceInAttaching, IsChinese() ? "设备正在自动附加中。" : "The device is in auto-attaching.");
        }

        //string distribution;
        //string errMsg;
        //ErrorCode errorCode;
        //(errorCode, distribution, errMsg) = await GetRunningDistribution();

        //if (errorCode != ErrorCode.Success)
        //{
        //    log.Error($"Failed to get running WSL distribution: {errMsg}");
        //    return (errorCode, errMsg);
        //}

        //log.Info($"Using WSL distribution '{distribution}' to attach; the device will be available in all WSL 2 distributions.");

        //var args = new List<string>
        //{
        //    "attach",
        //    (useBusID ? "--busid" : "--hardware-id"), id,
        //    "--wsl", distribution
        //};

        var args = new List<string>
        {
            "attach",
            (useBusID ? "--busid" : "--hardware-id"), id,
            "--wsl"
        };

        if (!string.IsNullOrEmpty(hostIP))
        {
            args.Add("--host-ip");
            args.Add(hostIP);
        }

        var (errCode, Stdout, StdErr) = await RunUSBIPDWin(false, [.. args]);

        if (IsFailed(errCode))
        {
            log.Error($"Failed to attach {id}, stdout: {Stdout}; stderr: {StdErr}");
            if (StdErr.Contains("A firewall appears to be blocking the connection"))
            {
                StdErr = IsChinese() ? USBIPDIsBlockedZh : StdErr;
            }
            else if (StdErr.Contains("There is no WSL 2 distribution running"))
            {
                StdErr = IsChinese() ? USBIPDNoWSLRunning : StdErr;
            }
            else if (StdErr.Contains("The device appears to be used by Windows"))
            {
                StdErr = IsChinese() ? USBDeviceInUsingZh : StdErr;
            }
            return new(ErrorCode.DeviceDetachFailed, StdErr);
        }

        if (autoAttach)
        {
            log.Info($"Auto-attach process started for device {id}.");
            args.Add("--auto-attach");
            await RunUSBIPDWinDaemon(false, [.. args]);
            return new(ErrorCode.Failure, IsChinese() ? "运行于WSL中的自动附加程序已退出。" : "The automatic attachment program running in WSL has exited.");
        }
        return new(errCode, StdErr);
    }

    public static async Task<(ErrorCode ErrCode, string ErrMsg)>
        DetachDevice(string? id, bool? useBusID)
    {
        var (ErrCode, _, StandardError) = await RunUSBIPDWin(false, string.IsNullOrEmpty(id) ? ["detach", "--all"] : ["detach", (useBusID.HasValue && useBusID.Value ? "--busid" : "--hardware-id"), id]);

        if (IsFailed(ErrCode))
        {
            log.Warn($"Failed to detach device '{id}': {StandardError}");
        }

        return new(IsSuccess(ErrCode) ? ErrorCode.Success : ErrorCode.DeviceDetachFailed, StandardError);
    }

    public static async Task<(ErrorCode ErrCode, string ErrMsg)>
        DetachAllDevice() => await DetachDevice(null, false);


    public static async Task<(ErrorCode ErrCode, string ErrMsg, List<USBDevice>? DevicesList)>
        ListUSBDevices(string? hardwareID, bool connectedOnly)
    {
        var (ErrCode, ErrMsg) = await CheckUSBIPDWin();
        if (ErrCode != ErrorCode.Success)
        {
            log.Error($"Failed to fetch USB device list: {ErrMsg}");
            return (ErrCode, ErrMsg, null);
        }
        string cmd = $"Import-Module '{UsbipdPowershellModulePath}';Get-UsbipdDevice";
        if (connectedOnly)
        {
            cmd += " | Where-Object {$_.IsConnected}";
        }

        if (!string.IsNullOrEmpty(hardwareID))
        {
            cmd += $" | Where-Object {{$_.HardwareId -eq '{hardwareID}'}}";
        }

        var (ExitCode, StandardOutput, StandardError) = await RunPowerShellScripts(cmd);

        if (IsFailed(ErrCode))
        {
            log.Warn($"Failed to fetch USB device list: {StandardError}");
            return (ErrorCode.Failure, StandardError, null);
        }

        if (string.IsNullOrEmpty(StandardOutput))
        {
            log.Warn("No USB device is found.");
            return (ErrorCode.Failure, StandardError, null);
        }
        return (ErrorCode.Success, "", ParseStringDevInfoToUSBDeviceList(StandardOutput));
    }

    public static async Task<(ErrorCode ErrCode, string ErrMsg, List<USBDevice>? DevicesList)>
        ListUSBDevices() => await ListUSBDevices(null, false);
    public static async Task<(ErrorCode ErrCode, string ErrMsg, List<USBDevice>? DevicesList)>
        ListUSBDevices(string hardwareID) => await ListUSBDevices(hardwareID, false);

    public static async Task<(ErrorCode ErrCode, string ErrMsg, List<USBDevice>? DevicesList)>
        ListConnectedDevices(string? hardwareID) => await ListUSBDevices(hardwareID, true);
    public static async Task<(ErrorCode ErrCode, string ErrMsg, List<USBDevice>? DevicesList)>
        ListConnectedDevices() => await ListUSBDevices(null, true);

    public static async Task<(ErrorCode ErrCode, string ErrMsg, List<USBDevice>? DevicesList)>
        ListPersistedDevices()
    {
        var (ErrCode, ErrMsg) = await CheckUSBIPDWin();
        if (ErrCode != ErrorCode.Success)
        {
            log.Error($"Failed to fetch USB device list: {ErrMsg}");
            return (ErrCode, ErrMsg, null);
        }
        string cmd = $"Import-Module '{UsbipdPowershellModulePath}';Get-UsbipdDevice | Where-Object {{-not $_.IsConnected -and $_.IsBound}}";
        var (ExitCode, StandardOutput, StandardError) = await RunPowerShellScripts(cmd);

        if (IsFailed(ErrCode))
        {
            log.Warn($"Failed to fetch persisted devices: {StandardError}");
            return (ErrorCode.Failure, StandardError, []);
        }

        return (ErrorCode.Success, "", ParseStringDevInfoToUSBDeviceList(StandardOutput));
    }

    public static async Task<bool> IsDeviceConnected(string hardwareId)
    {
        var (_, _, DevicesList) = await ListConnectedDevices(hardwareId);

        if (DevicesList == null)
        {
            return false;
        }

        foreach (var dev in DevicesList)
        {
            if (dev.HardwareId == hardwareId)
            {
                return true;
            }
        }

        return false;
    }

    public static async Task<bool> IsDeviceBound(string hardwareId)
    {
        var (_, _, DevicesList) = await ListConnectedDevices(hardwareId);

        if (DevicesList == null)
        {
            return false;
        }

        foreach (var dev in DevicesList)
        {
            if (dev.HardwareId == hardwareId && dev.IsBound)
            {
                return true;
            }
        }

        return false;
    }

    public static async Task<bool> IsDeviceAttached(string hardwareId)
    {
        var (_, _, DevicesList) = await ListConnectedDevices(hardwareId);

        if (DevicesList == null)
        {
            return false;
        }

        foreach (var dev in DevicesList)
        {
            if (dev.HardwareId == hardwareId && dev.IsAttached)
            {
                return true;
            }
        }

        return false;
    }
}
