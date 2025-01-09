/******************************************************************************
* SPDX-License-Identifier: MIT
* Project: wsl-usb-manager
* Class: USBIPD.cs
* NameSpace: wsl_usb_manager.Controller
* Author: Chuckie
* copyright: Copyright (c) Chuckie, 2024
* Description:
* Create Date: 2024/10/17 20:22
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

namespace wsl_usb_manager.Controller;

public partial class USBIPD
{
    private static readonly string CmdGetAllDevices = @"Import-Module $env:ProgramW6432'\usbipd-win\PowerShell\Usbipd.Powershell.dll';Get-UsbipdDevice";
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
    private static readonly ILog log = LogManager.GetLogger(typeof(USBIPD));
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
    public const string USBIPSharedDeviceID = "80EE:CAFE";
    private static bool IsChinese() => Lang.IsChinese();

    public USBIPD()
    {

    }

    public static bool IsRunningAsAdministrator()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        WindowsPrincipal principal = new(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static string GetUSBIPDInstallPath() => USBIPD_INSTALL_PATH;

    private static async Task<(int ExitCode, string StandardOutput, string StandardError)> CheckUSBIPDWin()
    {
        var stdout = string.Empty;
        var stderr = string.Empty;

        if(!string.IsNullOrWhiteSpace(USBIPD_INSTALL_PATH) && !string.IsNullOrWhiteSpace(USBIPD_VERSION))
        {
            return (0, USBIPD_INSTALL_PATH, USBIPD_VERSION);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command {ScriptsCheckUSBIPD}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        Process? process = null;
        await Task.Run(async () =>
        {
            process = Process.Start(startInfo);
            if (process != null)
            {
                try
                {
                    // Asynchronously read the standard output
                    var outputTask = Task.Run(async () =>
                    {
                        while (!process.StandardOutput.EndOfStream)
                        {
                            var line = await process.StandardOutput.ReadLineAsync();
                            stdout += line + Environment.NewLine;
                        }
                    });

                    // Asynchronously read the standard error
                    var errorTask = Task.Run(async () =>
                    {
                        while (!process.StandardError.EndOfStream)
                        {
                            var line = await process.StandardError.ReadLineAsync();
                            stderr += line + Environment.NewLine;
                        }
                    });
                    process.WaitForExit(1000);
                    await Task.WhenAll(outputTask, errorTask);
                }
                finally
                {
                    // Kill the entire Windows process tree, just in case it hasn't exited already.
                    process.Kill(true);

                }
            }
        });

        if (process == null)
        {
            if (IsChinese())
                stderr = $"无法检查\"usbipd\"安装情况: 无法打开子进程。";
            else
                stderr = $"Failed to check \"usbipd\" installation: Cannot start process.";

            log.Error(stderr);
            return new((int)ExitCode.Failure, "", stderr);
        }

        if (string.IsNullOrWhiteSpace(stdout))
        { 
            if (IsChinese())
                stderr = $"无法检查\"usbipd\"安装情况: {stderr}。";
            else
                stderr = $"Failed to check \"usbipd\" installation: {stderr}";
            log.Error(stderr);
            return new((int)ExitCode.NotFound, "", stderr);
        }
        string[] output = stdout.Trim().Split(Environment.NewLine);
        USBIPD_INSTALL_PATH = output[0].Trim();
        if (!Path.Exists(USBIPD_INSTALL_PATH))
        {
            stderr = $"{(IsChinese() ? ErrUsbipNotInstalledZH : ErrUsbipNotInstalledEN)}";
            log.Error(stderr);
            USBIPD_INSTALL_PATH = string.Empty;
            return ((int)ExitCode.NotFound, "", stderr);
        }

        if (output.Length < 2)
        {
            if (IsChinese())
                stderr = $"无法检查\"usbipd\"安装情况: {stderr}。";
            else
                stderr = $"Failed to check \"usbipd\" installation: {stderr}";
            log.Error(stderr);
            USBIPD_INSTALL_PATH = string.Empty;
            return new((int)ExitCode.Failure, stdout, stderr);
        }

        USBIPD_VERSION = output[1].Trim();
        if(string.IsNullOrWhiteSpace(USBIPD_VERSION))
        {
            if (IsChinese())
                stderr = $"无法检查\"usbipd\"版本： {stdout}。";
            else
                stderr = $"Failed to check \"usbipd\" version: {stdout}";
            log.Error(stderr);
            USBIPD_INSTALL_PATH = string.Empty;
            return new((int)ExitCode.Failure, stdout, stderr);
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
                return ((int)ExitCode.LowVersion, stdout, stderr);
            }
        }
        else
        {
            if (IsChinese())
                stderr = $"无法解析\"usbipd\"版本： {stdout}。";
            else
                stderr = $"Failed to parse USBIPD version: {stdout}";
            log.Error(stderr);
            USBIPD_INSTALL_PATH = string.Empty;
            return ((int)ExitCode.ParseError, stdout, stderr);
        }
        USBIPD_WSL_PATH = Path.Combine(USBIPD.GetUSBIPDInstallPath(), "WSL");

        if (!Path.Exists(USBIPD_WSL_PATH))
        {
            USBIPD_INSTALL_PATH = string.Empty;
            stderr = $"{(IsChinese() ? ErrUsbipNotInstalledZH : ErrUsbipNotInstalledEN)}";
            log.Error(stderr);
            return ((int)ExitCode.LowVersion, stdout,stderr);
        }
        if ((Path.GetPathRoot(USBIPD_WSL_PATH) is not string wslWindowsPathRoot) || (!LocalDriveRegex().IsMatch(wslWindowsPathRoot)))
        {
            stderr = (IsChinese() ? ErrUsbipLocationZH : ErrUsbipLocationEN);
            log.Error(stderr);
            return ((int)ExitCode.Failure, stdout, stderr);
        }
        return ((int)ExitCode.Success, stdout,stderr);
    }


    private static async Task<(int ExitCode, string StandardOutput, string StandardError)>
        RunPowerShellScripts(string script, int timeout_ms)
    {
        int exitCode = (int)ExitCode.Failure;
        var stdout = string.Empty;
        var stderr = string.Empty;
        (exitCode,stdout,stderr) = await CheckUSBIPDWin();
        if (exitCode != (int)ExitCode.Success)
        {
            USBIPD_INSTALL_PATH = string.Empty;
            return (exitCode, stdout, stderr);
        }
        stdout = "";
        stderr = "";
        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command {script}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        Process? process = null;
        await Task.Run(async () =>
        {
            process = Process.Start(startInfo);
            if (process != null)
            {
                try
                {
                    // Asynchronously read the standard output
                    var outputTask = Task.Run(async () =>
                    {
                        while (!process.StandardOutput.EndOfStream)
                        {
                            var line = await process.StandardOutput.ReadLineAsync();
                            stdout += line + Environment.NewLine;
                        }
                    });

                    // Asynchronously read the standard error
                    var errorTask = Task.Run(async () =>
                    {
                        while (!process.StandardError.EndOfStream)
                        {
                            var line = await process.StandardError.ReadLineAsync();
                            stderr += line + Environment.NewLine;
                        }
                    });
                    process.WaitForExit(timeout_ms);
                    await Task.WhenAll(outputTask, errorTask);
                }
                finally
                {
                    // Kill the entire Windows process tree, just in case it hasn't exited already.
                    process.Kill(true);

                }
            }
        });

        if (process == null)
        {
            return new((int)ExitCode.Failure, "", $"Failed to start \"usbipd\" with arguments {script}.");
        }
        if(process.ExitCode != 0)
        {
            log.Error($"Failed to run powershell scripts, error: {stderr}");
            return new(process.ExitCode, stdout, stderr);
        }
        return new((int)ExitCode.Success, stdout, stderr);
    }

    private static async Task<(int ExitCode, string StandardOutput, string StandardError)>
        RunUSBIPD(bool privilege, string[] arguments)
    {
        ProcessStartInfo startInfo;
        int exitCode = (int)ExitCode.Failure;
        var stdout = string.Empty;
        var stderr = string.Empty;
        (exitCode, stdout, stderr) = await CheckUSBIPDWin();
        if (exitCode != (int)ExitCode.Success)
        {
            USBIPD_INSTALL_PATH = string.Empty;
            return (exitCode, stdout, stderr);
        }

        if (privilege && !IsRunningAsAdministrator())
        {
            //Run as administrator example:
            //Start-Process <process> -ArgumentList '<ArgumentList>' -Verb runAs -WindowStyle Hidden
            startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"Start-Process usbipd -ArgumentList '{arguments.Aggregate((s1, s2) => s1 + " " + s2)}' -WindowStyle Hidden -Verb RunAs",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
        }
        else
        {
            startInfo = new ProcessStartInfo
            {
                FileName = "usbipd",
                Arguments = $"{arguments.Aggregate((s1, s2) => s1 + " " + s2)}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
        }

        Process? process = null;
        await Task.Run(() =>
        {
            process = Process.Start(startInfo);
            if (process != null)
            {
                try
                {
                    process.WaitForExit();
                }
                finally
                {
                    // Kill the entire Windows process tree, just in case it hasn't exited already.
                    process.Kill(true);
                    stdout = process.StandardOutput.ReadToEnd();
                    stderr = process.StandardError.ReadToEnd();
                    using StringReader reader = new(stderr);
                    stderr = reader.ReadLine()?.Replace("Start-Process", "").Trim(':', ' ');
                }
            }
        });

        if (process == null)
        {
            return new((int)ExitCode.Failure, "", $"Failed to start \"usbipd\" with arguments {string.Join(" ", arguments.Select(arg => $"\"{arg}\""))}.");
        }
        if(process.ExitCode != 0)
        {
            log.Error($"Failed to run '{startInfo.FileName} {string.Join(" ", arguments.Select(arg => $"\"{arg}\""))}', error: {stderr}");
            return new((int)ExitCode.Failure, stdout, stderr);
        }
        return new((int)ExitCode.Success, stdout, stderr);
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
    public static async Task<(ExitCode exitCode, string errMsg, USBDevice? newDev)>
        BindDevice(string hardwareid, bool force)
    {
        string stderr = "";
        USBDevice? dev = null;
        int ret = (int)ExitCode.BindError;
        for (int i= 0; i < 3; i++){
            (ret, string stdout, stderr) = 
                await RunUSBIPD(true, ["bind", "--hardware-id", hardwareid, (force ? "--force" : "")]);
            if (ret == (int)ExitCode.Success)
            {
                (_, _, dev) = await GetUSBDeviceByHardwareID(hardwareid, true);
                return (ExitCode.Success, "", dev);
            }
            else
            {
                log.Warn($"Failed to bind device '{hardwareid}': {stderr}");
            }
            await Task.Run(() => Thread.Sleep(200));
            (_, _, dev) = await GetUSBDeviceByHardwareID(hardwareid, true);
            if (dev != null && dev.IsBound)
            {
                return (ExitCode.Success, "", dev);
            }
        }
        
        return (ret >= (int)ExitCode.NotFound ? (ExitCode)ret : ExitCode.BindError, stderr,dev);
    }


    public static async Task<(ExitCode exitCode, string errMsg, USBDevice? newDev)> 
        BindDevice(string hardwareid) => await BindDevice(hardwareid, false);


    public static async Task<(ExitCode exitCode, string errMsg, USBDevice? newDev)>
        UnbindDevice(string? hardwareid)
    {
        int ret = (int)ExitCode.UnbindError;
        string stderr = "";
        USBDevice? dev = null;
        for (int i= 0; i < 3; i++)
        {
            if (string.IsNullOrEmpty(hardwareid))
            {
                (ret, _, stderr) = await RunUSBIPD(true, ["unbind", "--all"]);
            }
            else
            {
                (ret, _, stderr) = await RunUSBIPD(true, ["unbind", "--hardware-id", hardwareid]);
                (_, _, dev) = await GetUSBDeviceByHardwareID(hardwareid, true);
            }

            if (ret == (int)ExitCode.Success)
            {
                return ((ExitCode)ret, "",dev);
            }
            else
            {
                log.Warn($"Failed to unbind device '{hardwareid}': {stderr}");
            }
        }

        return (ret >= (int)ExitCode.NotFound ? (ExitCode)ret : ExitCode.UnbindError, stderr, dev);
    }

    public static async Task<(ExitCode exitCode, string errMsg)> 
        UnbindAllDevice()
    {
        (ExitCode ret, string err, _) = await UnbindDevice(null);
        return (ret, err);
    }

    public static async Task<(ExitCode exitCode, string errMsg, USBDevice? newDev)>
        DetachDevice(string? hardwareid)
    {
        int ret = (int)ExitCode.DetachError;
        string stderr = "";
        USBDevice? dev = null;
        for (int i= 0; i< 3; i++)
        {
            if (string.IsNullOrEmpty(hardwareid))
            {
                (ret, _, stderr) = await RunUSBIPD(false, ["detach", "--all"]);
            }
            else
            {
                log.Warn($"Failed to detach device '{hardwareid}': {stderr}");
                (ret, _, stderr) = await RunUSBIPD(false, ["detach", "--hardware-id", hardwareid]);
                (_, _, dev) = await GetUSBDeviceByHardwareID(hardwareid, true);
            }

            if (ret == 0)
            {
                return (ExitCode.Success, "", dev);
            }
        }
        
        return  ((int)ret >= (int)ExitCode.NotFound ? (ExitCode)ret : ExitCode.DetachError, stderr, dev);
    }

    public static async Task<(ExitCode exitCode, string errMsg)> 
        DetachAllDevice()
    {
        (ExitCode ret, string err, _) = await DetachDevice(null);
        return (ret, err);
    }


    public static async Task<(ExitCode exitCode, string errMsg, List<USBDevice>? devList)> 
        ListAllUSBDevices()
    {
        var (ret, stdout, stderr) = await RunPowerShellScripts(CmdGetAllDevices, 2000);

        if (ret != (int)ExitCode.Success)
        {
            log.Warn($"Failed to fetch USB device list: {stderr}");
            return ((ExitCode)ret, stderr, []);
        }

        return (ExitCode.Success, "", ParseStringDevInfoToUSBDeviceList(stdout));
    }

    public static async Task<(ExitCode exitCode, string errMsg, List<USBDevice>? devList)> 
        ListConnectedDevices()
    {
        string cmd = $"{CmdGetAllDevices} | Where-Object {{$_.IsConnected}}";
        var(ret, stdout, stderr) = await RunPowerShellScripts(cmd, 2000);

        if (ret != (int)ExitCode.Success)
        {
            log.Warn($"Failed to fetch connected devices: {stderr}");
            return ((ExitCode)ret, stderr, []);
        }

        return (ExitCode.Success, "", ParseStringDevInfoToUSBDeviceList(stdout));
    }

    public static async Task<(ExitCode exitCode, string errMsg, List<USBDevice>? devList)> 
        ListPersistedDevices()
    {
        string cmd = $"{CmdGetAllDevices} | Where-Object {{-not $_.IsConnected -and $_.IsBound}}";
        var (ret, stdout, stderr) = await RunPowerShellScripts(cmd, 2000);

        if (ret != 0 || stdout.Length == 0)
        {
            log.Warn($"Failed to fetch persisted devices: {stderr}");
            return ((ExitCode)ret, stderr, []);
        }

        return (ExitCode.Success, "", ParseStringDevInfoToUSBDeviceList(stdout));
    }

    public static async Task<(ExitCode exitCode, string errMsg, USBDevice? dev)> 
        GetUSBDeviceByHardwareID(string hardwareId)
    {
        USBDevice? dev = null;

        (ExitCode exitCode, string err, List<USBDevice>? devList) = await ListAllUSBDevices();

        if (exitCode == ExitCode.Success)
        {
            dev = devList?.Find(x => string.Equals(x.HardwareId, hardwareId, StringComparison.OrdinalIgnoreCase));
        }

        return (exitCode, err, dev);
    }

    public static async Task<(ExitCode exitCode, string errMsg, USBDevice? dev)> 
        GetUSBDeviceByHardwareID(string hardwareId, bool connectedOnly)
    {
        USBDevice? dev = null;
        ExitCode exitCode = ExitCode.Failure;
        string err = "";
        List<USBDevice>? devList;
        if (connectedOnly)
        {
            (exitCode, err, devList) = await ListConnectedDevices();
        }
        else
        {
            (exitCode, err, devList) = await ListAllUSBDevices();
        }


        if (exitCode == ExitCode.Success)
        {
            dev = devList?.Find(x => string.Equals(x.HardwareId, hardwareId, StringComparison.OrdinalIgnoreCase));
        }

        return (exitCode, err, dev);
    }

    public static async Task<(ExitCode exitCode, string errMsg, USBDevice? dev)> 
        GetUSBDeviceByBusID(string busID)
    {
        USBDevice? dev = null;

        (ExitCode exitCode, string err, List<USBDevice>? devList) = await ListAllUSBDevices();

        if (exitCode == ExitCode.Success)
        {
            dev = devList?.Find(x => string.Equals(x.BusId, busID, StringComparison.OrdinalIgnoreCase));
        }

        return (exitCode, err, dev);
    }

    public static async Task<bool> IsDeviceConnected(string hardwareId)
    {
        (_, _, USBDevice? dev) = await GetUSBDeviceByHardwareID(hardwareId,true);
        if (dev != null)
        {
            
            return dev.IsConnected;
        }
        return false;
    }

    public static async Task<bool> IsDeviceBound(string hardwareId)
    {
        (_, _, USBDevice? dev) = await GetUSBDeviceByHardwareID(hardwareId, true);
        if (dev != null)
        {
            return dev.IsBound;
        }
        return false;
    }

    public static string GetUSBIPDVersion() => USBIPD_VERSION;
}
