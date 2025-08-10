/******************************************************************************
* SPDX-License-Identifier: MIT
* Project: wsl-usb-manager
* Class: USBIPDWin.cs
* NameSpace: wsl_usb_manager.USBIPD
* Author: Chuckie
* copyright: Copyright (c) Chuckie, 2025
* Description:
* Create Date: 2025/3/5 21:06
******************************************************************************/

// Ignore Spelling: USBIPD hardwareid hardwareid busid

using log4net;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography.Xml;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using wsl_usb_manager.Domain;
using wsl_usb_manager.Resources;

namespace wsl_usb_manager.USBIPD;

public static partial class USBIPDWin
{
    private const string UsbipdMinVersion = "4.4.0";
    private const string UsbipdDownloadUrl = "https://github.com/dorssel/usbipd-win/releases";
    private const string ErrUsbipNotInstalledZH = $"检测到usbipd-win未安装。请访问 {UsbipdDownloadUrl} 下载V{UsbipdMinVersion}及更高版本的usbipd-win并安装，然后重启本程序。";
    private const string ErrUsbipNotInstalledEN = $"Detected that usbipd-win is not installed. Please visit {UsbipdDownloadUrl} to download and install usbipd-win V{UsbipdMinVersion} or higher, then restart this program.";
    private const string ErrUsbipLowVersionZH = $"检测到usbipd-win版本低于V{UsbipdMinVersion}。请访问 {UsbipdDownloadUrl} 下载V{UsbipdMinVersion}及更高版本的usbipd-win并安装，然后重启本程序。";
    private const string ErrUsbipLowVersionEN = $"Detected that usbipd-win version is lower than V{UsbipdMinVersion}. Please visit {UsbipdDownloadUrl} to download and install usbipd-win V{UsbipdMinVersion} or higher, then restart this program.";
    private const string ErrUsbipLocationZH = "检测到usbipd-win可能安装在远程磁盘中，请将usbipd-win安装到本地磁盘，然后重启本程序。";
    private const string ErrUsbipLocationEN = "Detected that usbipd-win may be installed on a remote disk, please install usbipd-win to a local disk and restart this program.";

    private static string UsbipdPowershellModulePath = "";
    private static readonly ILog log = LogManager.GetLogger(typeof(USBIPDWin));
    [GeneratedRegex(@"^(\d+)\.(\d+)\.(\d+)*")]
    private static partial Regex VersionRegex();
    [GeneratedRegex(@"InstanceId\s*:\s*(?<InstanceId>.*?)\s*" +
                    @"HardwareId\s*:\s*(?<HardwareId>.*?)\s*" +
                    @"Description\s*:\s*(?<Description>.*?)\s*" +
                    @"IsForced\s*:\s*(?<IsForced>.*?)\s*" +
                    @"BusId\s*:\s*(?<BusId>.*?)\s*" +
                    @"PersistedGuid\s*:\s*(?<PersistedGuid>.*?)\s*" +
                    @"StubInstanceId\s*:\s*(?<StubInstanceId>.*?)\s*" +
                    @"ClientIPAddress\s*:\s*(?<ClientIPAddress>.*?)\s*" +
                    @"IsBound\s*:\s*(?<IsBound>.*?)\s*" +
                    @"IsConnected\s*:\s*(?<IsConnected>.*?)\s*" +
                    @"IsAttached\s*:\s*(?<IsAttached>.*?)\s*(?=\n\n|\Z)", RegexOptions.Singleline)]
    private static partial Regex DeviceInfoRegex();

    private const ushort USBIP_PORT = 3240;
    private static ApplicationInfo? USBIPDAppInfo = null;
    private static bool IsChinese() => Lang.IsChinese();

    private static async Task<(ErrorCode ErrCode, string ErrMsg)> CheckUSBIPDWin()
    {
        var stderr = string.Empty;

        if (USBIPDAppInfo is not null)
        {
            return new(ErrorCode.Success, stderr);
        }

        var appInfo = ApplicationChecker.GetApplicationInfo("usbipd-win");

        if (appInfo == null || string.IsNullOrWhiteSpace(appInfo.InstallLocation) || string.IsNullOrWhiteSpace(appInfo.DisplayName))
        {
            stderr = $"{(IsChinese() ? ErrUsbipNotInstalledZH : ErrUsbipNotInstalledEN)}";
            log.Error(stderr);
            return (ErrorCode.USBIPDNotFound, stderr);
        }
        appInfo.InstallLocation = appInfo.InstallLocation.Trim();
        if (!Directory.Exists(appInfo.InstallLocation))
        {
            stderr = $"{(IsChinese() ? ErrUsbipNotInstalledZH : ErrUsbipNotInstalledEN)}";
            log.Error(stderr);
            return (ErrorCode.USBIPDNotFound, stderr);
        }
        if (string.IsNullOrWhiteSpace(appInfo.DisplayVersion))
        {
            ProcessRunner runner = new();
            var (ExitCode, StandardOutput, StandardError) = await runner.RunUSBIPD(true, ["--version"]);
            runner.Destroy();
            appInfo.DisplayVersion = StandardOutput.Trim();
            stderr = StandardError;
            if (ExitCode == 0)
            {

                log.Error(StandardError);
                if (IsChinese())
                    stderr = $"无法检查\"usbipd\"版本： {stderr}。";
                else
                    stderr = $"Failed to check \"usbipd\" version: {stderr}";
                log.Error(stderr);
                return new(ErrorCode.Failure, stderr);
            }
        }

        if (string.IsNullOrWhiteSpace(appInfo.DisplayVersion))
        {
            if (IsChinese())
                stderr = $"无法检查\"usbipd\"版本： {stderr}。";
            else
                stderr = $"Failed to check \"usbipd\" version: {stderr}";
            log.Error(stderr);
            return new(ErrorCode.Failure, stderr);
        }
        log.Info($"USBIPD version: {appInfo.DisplayVersion}");
        Match match = VersionRegex().Match(appInfo.DisplayVersion);
        if (match.Success)
        {
            int major = int.Parse(match.Groups[1].Value);
            int minor = int.Parse(match.Groups[2].Value);
            int patch = int.Parse(match.Groups[3].Value);
            appInfo.DisplayVersion = $"{major}.{minor}.{patch}";
            Match requestVersion = VersionRegex().Match(UsbipdMinVersion);
            if (requestVersion.Success)
            {
                int minimajor = int.Parse(requestVersion.Groups[1].Value);
                int miniminer = int.Parse(requestVersion.Groups[2].Value);

                if (major < minimajor || (major == minimajor && minor < miniminer))
                {
                    stderr = $"{(IsChinese() ? ErrUsbipLowVersionZH : ErrUsbipLowVersionEN)}";

                    log.Error(stderr);
                    return (ErrorCode.USBIPDLowVersion, stderr);
                }
                else if (major == minimajor)
                {
                    UsbipdPowershellModulePath = Path.Combine(appInfo.InstallLocation, "PowerShell", "Usbipd.Powershell.dll");
                }
                else
                {
                    UsbipdPowershellModulePath = Path.Combine(appInfo.InstallLocation, "Usbipd.Powershell.dll");
                }
            }
            else
            {
                if (IsChinese())
                    stderr = $"无法解析请求的\"usbipd\"版本： {UsbipdMinVersion}。";
                else
                    stderr = $"Failed to parse requested USBIPD version: {UsbipdMinVersion}";
                log.Error(stderr);
                return (ErrorCode.ParseError, stderr);
            }
        }
        else
        {
            if (IsChinese())
                stderr = $"无法解析\"usbipd\"版本： {appInfo.DisplayVersion}。";
            else
                stderr = $"Failed to parse USBIPD version: {appInfo.DisplayVersion}";
            log.Error(stderr);
            return (ErrorCode.ParseError, stderr);
        }


        if ((Path.GetPathRoot(appInfo.InstallLocation) is not string wslWindowsPathRoot) || (!LocalDriveRegex().IsMatch(wslWindowsPathRoot)))
        {
            stderr = (IsChinese() ? ErrUsbipLocationZH : ErrUsbipLocationEN);
            log.Warn(stderr);
        }

        log.Info($"USBIPD version is {appInfo.DisplayVersion}.");
        log.Info($"USBIPD powershell module path: {UsbipdPowershellModulePath}");
        USBIPDAppInfo = appInfo;
        return (ErrorCode.Success, stderr);
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

    public static string GetUSBIPDInstallPath() => USBIPDAppInfo?.InstallLocation ?? string.Empty;

    public static string GetUSBIPDVersion() => USBIPDAppInfo?.DisplayVersion ?? string.Empty;

    /**
     * Bind a device with a hardware ID.
     */
    public static async Task<(ErrorCode ErrCode, string ErrMsg)>
        BindDevice(string id, bool useBusID, bool force)
    {
        string stderr = "";
        ProcessRunner runner = new();
        var (ExitCode, _, StandardError) = await runner.RunUSBIPD(true, ["bind", (useBusID ? "--busid" : "--hardware-id"), id, (force ? "--force" : "")]);
        if (ExitCode != 0)
        {
            log.Error($"Failed to bind device '{id}': {StandardError}");
            stderr = StandardError;
        }
        runner.Destroy();
        return new(ExitCode == 0 ? ErrorCode.Success : ErrorCode.DeviceBindFailed, stderr);
    }


    public static async Task<(ErrorCode ErrCode, string ErrMsg)>
        BindDevice(string id, bool useBusID) => await BindDevice(id, useBusID, false);


    public static async Task<(ErrorCode ErrCode, string ErrMsg)>
        UnbindDevice(string? id, bool? useBusID)
    {
        ProcessRunner runner = new();
        var (ExitCode, _, StandardError) = await runner.RunUSBIPD(
            true,
            string.IsNullOrEmpty(id)
                ? ["unbind", "--all"]
                : ["unbind", (useBusID.HasValue && useBusID.Value ? "--busid" : "--hardware-id"), id]
        );
        if (ExitCode != 0)
        {
            log.Warn($"Failed to unbind device '{id}': {StandardError}");
        }
        runner.Destroy();

        return new(ExitCode == 0 ? ErrorCode.Success : ErrorCode.DeviceUnbindFailed, StandardError);
    }

    public static async Task<(ErrorCode ErrCode, string ErrMsg)>
        UnbindAllDevice() => await UnbindDevice(null, false);

    /// <summary>
    /// ID(Bus ID or hardware ID) has already been checked, and the server is running.
    /// </summary>
    public static async Task<(ErrorCode ErrCode, string ErrMsg)>
        Attach(string id, bool useBusID, bool autoAttach, string? hostIP)
    {
        ProcessRunner runner = new(id);
        string distribution = string.Empty;
        string errMsg = string.Empty;
        ErrorCode errorCode = ErrorCode.Failure;

        // Check: Is the device has been auto-attached.
        lock (AttachProcessListLock)
        {
            foreach (var p in AttachProcessList)
            {
                if (p.Name.Equals(id, StringComparison.CurrentCultureIgnoreCase))
                {
                    if (!p.HasExited())
                    {

                        errMsg = $"Device {id} has been auto-attached.";
                        log.Info(errMsg);
                        runner.Destroy();
                        return new(ErrorCode.Success, errMsg);
                    }
                    else
                    {
                        log.Warn($"Device {id} has been auto-attached, but the process has exited.");
                        AttachProcessList.Remove(p);
                    }
                    break;
                }
            }
        }

        (errorCode, distribution, errMsg) = await GetRunningDistribution();

        if (errorCode != ErrorCode.Success)
        {
            log.Error($"Failed to get running WSL distribution: {errMsg}");
            runner.Destroy();
            return (errorCode, errMsg);
        }

        log.Info($"Using WSL distribution '{distribution}' to attach; the device will be available in all WSL 2 distributions.");
        (errorCode, errMsg) = await CheckWslCondition(distribution, hostIP);
        if (errorCode != ErrorCode.Success)
        {
            log.Error($"Failed to check WSL distribution: {errMsg}");
            runner.Destroy();
            return (errorCode, errMsg);
        }
        var args = new List<string>
        {
            "attach",
            (useBusID ? "--busid" : "--hardware-id"), id,
            "--wsl", distribution
        };

        if (!string.IsNullOrEmpty(hostIP))
        {
            args.Add("--host-ip");
            args.Add(hostIP);
        }

        if (autoAttach)
        {
            log.Info($"Auto-attach process started for device {id}.");
            args.Add("--auto-attach");
            lock (AttachProcessListLock)
            {
                AttachProcessList.Add(runner);
            }
        }
        var (ExitCode, _, StandardError) = await runner.RunUSBIPD(false, [.. args]);

        if (!autoAttach)
        {
            runner.Destroy();
            return new(ExitCode == 0 ? ErrorCode.Success : ErrorCode.DeviceAttachFailed, StandardError);
        }

        return new(ErrorCode.Failure, IsChinese() ? "运行于WSL中的自动附加程序已退出。" : "The automatic attachment program running in WSL has exited.");
    }

    public static async Task<(ErrorCode ErrCode, string ErrMsg)>
        DetachDevice(string? id, bool? useBusID)
    {
        ProcessRunner runner = new();
        var (ExitCode, _, StandardError) = await runner.RunUSBIPD(false, string.IsNullOrEmpty(id) ? ["detach", "--all"] : ["detach", (useBusID.HasValue && useBusID.Value ? "--busid" : "--hardware-id"), id]);

        if (ExitCode != 0)
        {
            log.Warn($"Failed to detach device '{id}': {StandardError}");
        }
        runner.Destroy();

        return new(ExitCode == 0 ? ErrorCode.Success : ErrorCode.DeviceDetachFailed, StandardError);
    }

    public static async Task<(ErrorCode ErrCode, string ErrMsg)>
        DetachAllDevice() => await DetachDevice(null,false);


    public static async Task<(ErrorCode ErrCode, string ErrMsg, List<USBDevice>? DevicesList)>
        ListUSBDevices(string? hardwareID, bool connectedOnly)
    {
        ProcessRunner runner = new();
        var (ErrCode, ErrMsg) = await CheckUSBIPDWin();
        if (ErrCode != ErrorCode.Success)
        {
            log.Error($"Failed to fetch USB device list: {ErrMsg}");
            runner.Destroy();
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

        var (ExitCode, StandardOutput, StandardError) = await runner.RunPowerShellScripts(cmd);

        if (ExitCode != 0)
        {
            log.Warn($"Failed to fetch USB device list: {StandardError}");
            runner.Destroy();
            return (ErrorCode.Failure, StandardError, null);
        }
        runner.Destroy();
        if (string.IsNullOrEmpty(StandardOutput))
        {
            log.Warn("No info is found.");
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
        ProcessRunner runner = new();

        var (ErrCode, ErrMsg) = await CheckUSBIPDWin();
        if (ErrCode != ErrorCode.Success)
        {
            log.Error($"Failed to fetch USB device list: {ErrMsg}");
            runner.Destroy();
            return (ErrCode, ErrMsg, null);
        }
        string cmd = $"Import-Module '{UsbipdPowershellModulePath}';Get-UsbipdDevice | Where-Object {{-not $_.IsConnected -and $_.IsBound}}";
        var (ExitCode, StandardOutput, StandardError) = await runner.RunPowerShellScripts(cmd);

        if (ExitCode != 0)
        {
            log.Warn($"Failed to fetch persisted devices: {StandardError}");
            runner.Destroy();
            return (ErrorCode.Failure, StandardError, []);
        }
        runner.Destroy();

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
