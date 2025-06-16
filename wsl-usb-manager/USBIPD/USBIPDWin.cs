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
using System.Security.Cryptography.Xml;
using System.Security.Principal;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using wsl_usb_manager.Resources;

namespace wsl_usb_manager.USBIPD;

public static partial class USBIPDWin
{
    private const string ErrUsbipNotInstalledZH = "未检测到usbipd-win。可能usbipd-win的版本低于4.0.0，请访问 https://github.com/dorssel/usbipd-win/releases 下载版本大于4.0.0的usbipd-win并安装，然后重启本程序。";
    private const string ErrUsbipNotInstalledEN = "No usbipd-win was found. Please download usbipd-win version greater than 4.0.0 from https://github.com/dorssel/usbipd-win/releases and install it, then restart this program.";
    private const string ErrUsbipLocationZH = "检测到usbipd-win可能安装在远程磁盘中，请将usbipd-win安装到本地磁盘，然后重启本程序。";
    private const string ErrUsbipLocationEN = "Detected that usbipd-win may be installed on a remote disk, please install usbipd-win to a local disk and restart this program.";

    private static string CmdGetAllDevices = @"Import-Module $env:ProgramW6432'\usbipd-win\PowerShell\Usbipd.Powershell.dll';Get-UsbipdDevice";
    private static string AutoAttachProcessName = "auto-attach.sh";
    private static readonly string ScriptsCheckUSBIPD = @"
            $usbipdPath = (Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*' | Where-Object { $_.DisplayName -eq 'usbipd-win' }).InstallLocation
            if ($usbipdPath) {
                $usbipdVersion = & usbipd --version
                write-output $usbipdPath
                write-output $usbipdVersion
            } else {
                Write-Output ''
            }
        ";
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

    private static string USBIPD_INSTALL_PATH = string.Empty;
    private static string USBIPD_VERSION = string.Empty;
    private static string USBIPD_WSL_PATH = string.Empty;
    private const ushort USBIP_PORT = 3240;
    private static bool IsChinese() => Lang.IsChinese();

    private static async Task<(ErrorCode ErrCode, string ErrMsg)> CheckUSBIPDWin()
    {
        var stderr = string.Empty;
        ProcessRunner runner = new();
        if (!string.IsNullOrWhiteSpace(USBIPD_INSTALL_PATH) && !string.IsNullOrWhiteSpace(USBIPD_VERSION))
        {
            runner.Destroy();
            return new(ErrorCode.Success, stderr);
        }

        var result = await runner.RunPowerShellScripts(ScriptsCheckUSBIPD);

        if (result.ExitCode != (int)ErrorCode.Success || string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            if (IsChinese())
                stderr = $"无法检查\"usbipd\"安装情况: {result.StandardError}。";
            else
                stderr = $"Failed to check \"usbipd\" installation: {result.StandardError}.";

            log.Error(stderr);
            runner.Destroy();
            return new(ErrorCode.Failure, stderr);
        }


        string[] output = result.StandardOutput.Trim().Split("\n");
        USBIPD_INSTALL_PATH = output[0].Trim().Trim('\r').Trim('\n');
        if (!Path.Exists(USBIPD_INSTALL_PATH))
        {
            stderr = $"{(IsChinese() ? ErrUsbipNotInstalledZH : ErrUsbipNotInstalledEN)}";
            log.Error(stderr);
            USBIPD_INSTALL_PATH = string.Empty;
            runner.Destroy();
            return (ErrorCode.USBIPDNotFound, stderr);
        }

        if (output.Length < 2)
        {
            if (IsChinese())
                stderr = $"无法检查\"usbipd\"安装情况: {result.StandardError}。";
            else
                stderr = $"Failed to check \"usbipd\" installation: {result.StandardError}";
            log.Error(stderr);
            USBIPD_INSTALL_PATH = string.Empty;
            runner.Destroy();
            return new(ErrorCode.Failure, stderr);
        }

        USBIPD_VERSION = output[1].Trim();
        if (string.IsNullOrWhiteSpace(USBIPD_VERSION))
        {
            if (IsChinese())
                stderr = $"无法检查\"usbipd\"版本： {result.StandardOutput}。";
            else
                stderr = $"Failed to check \"usbipd\" version: {result.StandardOutput}";
            log.Error(stderr);
            USBIPD_INSTALL_PATH = string.Empty;
            runner.Destroy();
            return new(ErrorCode.Failure, stderr);
        }
        log.Info($"USBIPD version: {USBIPD_VERSION}");
        Match match = VersionRegex().Match(USBIPD_VERSION);
        if (match.Success)
        {
            int major = int.Parse(match.Groups[1].Value);
            int minor = int.Parse(match.Groups[2].Value);
            int patch = int.Parse(match.Groups[3].Value);
            USBIPD_VERSION = $"{major}.{minor}.{patch}";
            if (major < 4 || ( major == 4 && minor < 4))
            {
                if (IsChinese())
                    stderr = $"\"usbipd\"版本 \"{USBIPD_VERSION}\" 太低。请安装usbipd-win v4.4.0及其以上版本。";
                else
                    stderr = $"USBIPD version is too low: {USBIPD_VERSION}. Please install usbipd-win version 4.4.0 or above.";
                log.Error(stderr);
                USBIPD_INSTALL_PATH = string.Empty;
                runner.Destroy();
                return (ErrorCode.USBIPDLowVersion, stderr);
            }
            else if (major > 4)
            {
                CmdGetAllDevices = @"Import-Module $env:ProgramW6432'\usbipd-win\Usbipd.Powershell.dll';Get-UsbipdDevice";
                AutoAttachProcessName= "usbip-auto-attach";
                log.Info($"USBIPD version is {USBIPD_VERSION}, using new command to get USB devices info.");
                log.Info($"CmdGetAllDevices: {CmdGetAllDevices}");
            }
        }
        else
        {
            if (IsChinese())
                stderr = $"无法解析\"usbipd\"版本： {result.StandardOutput}。";
            else
                stderr = $"Failed to parse USBIPD version: {result.StandardOutput}";
            log.Error(stderr);
            USBIPD_INSTALL_PATH = string.Empty;
            runner.Destroy();
            return (ErrorCode.ParseError, stderr);
        }
        USBIPD_WSL_PATH = Path.Combine(GetUSBIPDInstallPath(), "WSL");

        if (!Path.Exists(USBIPD_WSL_PATH))
        {
            USBIPD_INSTALL_PATH = string.Empty;
            stderr = $"{(IsChinese() ? ErrUsbipNotInstalledZH : ErrUsbipNotInstalledEN)}";
            log.Error(stderr);
            runner.Destroy();
            return (ErrorCode.USBIPDLowVersion, stderr);
        }
        if ((Path.GetPathRoot(USBIPD_WSL_PATH) is not string wslWindowsPathRoot) || (!LocalDriveRegex().IsMatch(wslWindowsPathRoot)))
        {
            stderr = (IsChinese() ? ErrUsbipLocationZH : ErrUsbipLocationEN);
            log.Error(stderr);
            runner.Destroy();
            return (ErrorCode.Failure, stderr);
        }
        runner.Destroy();
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

    public static string GetUSBIPDInstallPath() => USBIPD_INSTALL_PATH;

    public static string GetUSBIPDVersion() => USBIPD_VERSION;

    public static string GetAutoAttachProcessName() => AutoAttachProcessName;
    /**
     * Bind a device with a hardware ID.
     */
    public static async Task<(ErrorCode ErrCode, string ErrMsg)>
        BindDevice(string hardwareid, bool force)
    {
        string stderr = "";
        ProcessRunner runner = new();
        var result = await runner.RunUSBIPD(true, ["bind", "--hardware-id", hardwareid, (force ? "--force" : "")]);
        if (result.ExitCode != 0)
        {
            log.Error($"Failed to bind device '{hardwareid}': {result.StandardError}");
            stderr = result.StandardError;
        }
        runner.Destroy();
        return new(result.ExitCode == 0 ? ErrorCode.Success : ErrorCode.DeviceBindFailed, stderr);
    }


    public static async Task<(ErrorCode ErrCode, string ErrMsg)> 
        BindDevice(string hardwareid) => await BindDevice(hardwareid, false);


    public static async Task<(ErrorCode ErrCode, string ErrMsg)>
        UnbindDevice(string? hardwareid)
    {
        ProcessRunner runner = new();
        var ret = await runner.RunUSBIPD(false, string.IsNullOrEmpty(hardwareid) ? ["unbind", "--all"] : ["unbind", "--hardware-id", hardwareid]);
        if (ret.ExitCode != 0)
        {
            log.Warn($"Failed to unbind device '{hardwareid}': {ret.StandardError}");
        }
        runner.Destroy();

        return new(ret.ExitCode == 0 ? ErrorCode.Success : ErrorCode.DeviceUnbindFailed, ret.StandardError);
    }

    public static async Task<(ErrorCode ErrCode, string ErrMsg)> 
        UnbindAllDevice() => await UnbindDevice(null);

    /// <summary>
    /// BusId has already been checked, and the server is running.
    /// </summary>
    public static async Task<(ErrorCode ErrCode, string ErrMsg)>
        Attach(string busID, bool autoAttach, string? hostIP)
    {
        ProcessRunner runner = new(busID);
        string distribution = string.Empty;
        string errMsg = string.Empty;

        // Check: Is the device has been auto-attached.
        lock (AttachProcessListLock)
        {
            foreach (var p in AttachProcessList)
            {
                if (p.Name.Equals(busID, StringComparison.CurrentCultureIgnoreCase))
                {
                    if (!p.HasExited())
                    {

                        errMsg = $"Device {busID} has been auto-attached.";
                        log.Info(errMsg);
                        runner.Destroy();
                        return new(ErrorCode.Success, errMsg);
                    }
                    else
                    {
                        log.Warn($"Device {busID} has been auto-attached, but the process has exited.");
                        AttachProcessList.Remove(p);
                    }
                    break;
                }
            }
        }

        // Figure out which distribution to use. WSL can be in many states:
        // (a) not installed at all
        // (b) if the user specified one:
        //      (1) it must exist
        //      (2) it must be version 2
        //      (3) it must be running
        // (c) if the user did not specify one:
        //      (1) there must exist at least one distribution
        //      (2) there must exist at least one version 2 distribution
        //      (3) there must be at least one version 2 running
        //      (4)
        //          (i) use the default distribution, if and only if it is version 2 and running
        //              (FYI: This is administered by WSL in HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Lxss.)
        //          (ii) use the first one that is version 2 and running
        //
        // We provide enough instructions to the user how to fix whatever
        // error/warning we give. Or else we get flooded with "it doesn't work" issues...

        if (await GetWSLDistributions() is not IEnumerable<Distribution> distributions)
        {
            // check (a) failed
            errMsg = IsChinese() ? ErrNoWslDistributionZH : ErrNoWslDistributionEN;
            runner.Destroy();
            return new(ErrorCode.WslDistribNotFound, errMsg); ;
        }

        // check distribution version
        if (!distributions.Any(d => d.Version == 2))
        {
            errMsg = IsChinese() ? ErrWslVersionZh : ErrWslVersionEn;
            log.Error(errMsg);
            runner.Destroy();
            return (ErrorCode.WslLowVersion, errMsg);
        }

        // check is any distribution running
        if (!distributions.Any(d => d.Version == 2 && d.IsRunning))
        {
            errMsg = IsChinese() ? ErrNoWslDistributionRunningZh : ErrNoWslDistributionRunningEn;
            log.Error(errMsg);
            runner.Destroy();
            return (ErrorCode.WslNotRunning, errMsg);
        }

        if (distributions.FirstOrDefault(d => d.IsDefault && d.Version == 2 && d.IsRunning) is Distribution defaultDistribution)
        {
            distribution = defaultDistribution.Name;
        }
        else
        {
            distribution = distributions.First(d => d.Version == 2 && d.IsRunning).Name;
        }


        log.Info($"Using WSL distribution '{distribution}' to attach; the device will be available in all WSL 2 distributions.");

        var args = new List<string>
        {
            "attach",
            "--busid", busID,
            "--wsl", distribution
        };
        if (!string.IsNullOrEmpty(hostIP))
        {
            args.Add("--host-ip");
            args.Add(hostIP);
        }
        if (autoAttach)
        {
            args.Add("--auto-attach");
        }
        
        var (ExitCode, StandardOutput, StandardError) = await runner.RunUSBIPD(false, [.. args]);

        if (!autoAttach)
        {
            if (ExitCode != 0)
            {
                log.Warn($"Failed to attach device '{busID}': {StandardError}");
            }
            runner.Destroy();
            return new(ExitCode == 0 ? ErrorCode.Success : ErrorCode.DeviceAttachFailed, StandardError);
        }

        log.Info($"Auto-attach process started for device {busID}.");
        lock (AttachProcessListLock)
        {
            AttachProcessList.Add(runner);
        }

        return new(ErrorCode.Failure, IsChinese() ? "运行于WSL中的自动附加程序已退出。" : "The automatic attachment program running in WSL has exited.");
    }

    public static async Task<(ErrorCode ErrCode, string ErrMsg)>
        DetachDevice(string? hardwareid)
    {
        ProcessRunner runner = new();
        var ret = await runner.RunUSBIPD(false, string.IsNullOrEmpty(hardwareid) ? ["detach", "--all"] : ["detach", "--hardware-id", hardwareid]);

        if (ret.ExitCode != 0)
        {
            log.Warn($"Failed to detach device '{hardwareid}': {ret.StandardError}");
        }
        runner.Destroy();

        return new(ret.ExitCode == 0 ? ErrorCode.Success : ErrorCode.DeviceDetachFailed, ret.StandardError);
    }

    public static async Task<(ErrorCode ErrCode, string ErrMsg)> 
        DetachAllDevice() => await DetachDevice(null);


    public static async Task<(ErrorCode ErrCode, string ErrMsg, List<USBDevice>? DevicesList)> 
        ListUSBDevices(string? hardwareID, bool connectedOnly)
    {
        List<USBDevice>? devList = [];
        ProcessRunner runner = new();
        var check = await CheckUSBIPDWin();
        if (check.ErrCode != ErrorCode.Success)
        {
            log.Error($"Failed to fetch USB device list: {check.ErrMsg}");
            runner.Destroy();
            return (check.ErrCode, check.ErrMsg, null);
        }
        string cmd = CmdGetAllDevices;
        if (connectedOnly)
        {
            cmd += " | Where-Object {$_.IsConnected}";
        }

        if (!string.IsNullOrEmpty(hardwareID))
        {
            cmd += $" | Where-Object {{$_.HardwareId -eq '{hardwareID}'}}";
        }
        
        var ret = await runner.RunPowerShellScripts(cmd);

        if (ret.ExitCode != 0)
        {
            log.Warn($"Failed to fetch USB device list: {ret.StandardError}");
            runner.Destroy();
            return (ErrorCode.Failure, ret.StandardError,null);
        }
        runner.Destroy();
        if (string.IsNullOrEmpty(ret.StandardOutput))
        {
            log.Warn("No info is found.");
            return (ErrorCode.Failure, ret.StandardError,null);
        }
        return (ErrorCode.Success, "", ParseStringDevInfoToUSBDeviceList(ret.StandardOutput));
    }

    public static async Task<(ErrorCode ErrCode, string ErrMsg, List<USBDevice>? DevicesList)>
        ListUSBDevices() => await ListUSBDevices(null, false);
    public static async Task<(ErrorCode ErrCode, string ErrMsg, List<USBDevice>? DevicesList)>
        ListUSBDevices(string hardwareID) => await ListUSBDevices(hardwareID, false);

    public static async Task<(ErrorCode ErrCode, string ErrMsg, List<USBDevice>? DevicesList)> 
        ListConnectedDevices(string ? hardwareID) => await ListUSBDevices(hardwareID, true);
    public static async Task<(ErrorCode ErrCode, string ErrMsg, List<USBDevice>? DevicesList)>
        ListConnectedDevices() => await ListUSBDevices(null, true);

    public static async Task<(ErrorCode ErrCode, string ErrMsg, List<USBDevice>? DevicesList)> 
        ListPersistedDevices()
    {
        ProcessRunner runner = new();

        var check = await CheckUSBIPDWin();
        if (check.ErrCode != ErrorCode.Success)
        {
            log.Error($"Failed to fetch USB device list: {check.ErrMsg}");
            runner.Destroy();
            return (check.ErrCode, check.ErrMsg, null);
        }
        string cmd = $"{CmdGetAllDevices} | Where-Object {{-not $_.IsConnected -and $_.IsBound}}";
        var ret = await runner.RunPowerShellScripts(cmd);

        if (ret.ExitCode != 0)
        {
            log.Warn($"Failed to fetch persisted devices: {ret.StandardError}");
            runner.Destroy();
            return (ErrorCode.Failure, ret.StandardError, []);
        }
        runner.Destroy();

        return (ErrorCode.Success, "", ParseStringDevInfoToUSBDeviceList(ret.StandardOutput));
    }

    public static async Task<bool> IsDeviceConnected(string hardwareId)
    {
        var ret = await ListConnectedDevices(hardwareId);

        if (ret.DevicesList == null)
        {
            return false;
        }

        foreach (var dev in ret.DevicesList)
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
        var ret = await ListConnectedDevices(hardwareId);

        if (ret.DevicesList == null)
        {
            return false;
        }

        foreach (var dev in ret.DevicesList)
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
        var ret = await ListConnectedDevices(hardwareId);

        if (ret.DevicesList == null)
        {
            return false;
        }

        foreach (var dev in ret.DevicesList)
        {
            if (dev.HardwareId == hardwareId && dev.IsAttached)
            {
                return true;
            }
        }

        return false;
    }

}
