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
            if (major < 4)
            {
                if (IsChinese())
                    stderr = $"\"usbipd\"版本 \"{USBIPD_VERSION}\" 太低。";
                else
                    stderr = $"USBIPD version is too low: {USBIPD_VERSION}";
                log.Error(stderr);
                USBIPD_INSTALL_PATH = string.Empty;
                runner.Destroy();
                return (ErrorCode.USBIPDLowVersion, stderr);
            }
            else if (major > 4)
            {
                CmdGetAllDevices = @"Import-Module $env:ProgramW6432'\usbipd-win\Usbipd.Powershell.dll';Get-UsbipdDevice";
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
        var ret = await runner.RunUSBIPD(true, string.IsNullOrEmpty(hardwareid) ? ["unbind", "--all"] : ["unbind", "--hardware-id", hardwareid]);
        if (ret.ExitCode != 0)
        {
            log.Warn($"Failed to unbind device '{hardwareid}': {ret.StandardError}");
        }
        runner.Destroy();

        return new(ret.ExitCode == 0 ? ErrorCode.Success : ErrorCode.DeviceUnbindFailed, ret.StandardError);
    }

    public static async Task<(ErrorCode ErrCode, string ErrMsg)> 
        UnbindAllDevice() => await UnbindDevice(null);
 

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
