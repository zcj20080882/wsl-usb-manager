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
using System.Security.Principal;
using System.Text.RegularExpressions;
using System.Windows.Forms;

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

    public const string USBIPSharedDeviceID = "80EE:CAFE";

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
    public static async Task<(ExitCode exitCode, string stdout, string stderr)> CheckUsbipdWinInstallation()
    {
        var stdout = string.Empty;
        var stderr = string.Empty;

        if(!string.IsNullOrWhiteSpace(USBIPD_INSTALL_PATH) && !string.IsNullOrWhiteSpace(USBIPD_VERSION))
        {
            return (ExitCode.Success, USBIPD_INSTALL_PATH, USBIPD_VERSION);
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
            log.Error($"Failed to check \"usbipd\" installation: Cannot start process.");
            return new(ExitCode.Failure, "", $"Failed to check \"usbipd\" installation.");
        }

        if (string.IsNullOrWhiteSpace(stdout))
        {
            log.Error($"Failed to check \"usbipd\" installation: {stderr}");
            return new(ExitCode.NotFound, "", stderr);
        }
        string[] output = stdout.Trim().Split(Environment.NewLine);
        USBIPD_INSTALL_PATH = output[0].Trim();
        if (!Path.Exists(USBIPD_INSTALL_PATH))
        {
            log.Error("USBIPD is not installed.");
            USBIPD_INSTALL_PATH = string.Empty;
            return (ExitCode.NotFound, "", "USBIPD is not installed");
        }

        if (output.Length < 2)
        {
            log.Error($"Failed to check \"usbipd\" installation: {stdout}");
            USBIPD_INSTALL_PATH = string.Empty;
            return new(ExitCode.Failure, stdout, "");
        }

        USBIPD_VERSION = output[1].Trim();
        if(string.IsNullOrWhiteSpace(USBIPD_VERSION))
        {
            log.Error($"Failed to check \"usbipd\" version: {stdout}");
            USBIPD_INSTALL_PATH = string.Empty;
            return new(ExitCode.Failure, stdout, "");
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
                log.Warn($"USBIPD version is too low: {USBIPD_VERSION}");
                USBIPD_INSTALL_PATH = string.Empty;
                return (ExitCode.LowVersion, USBIPD_VERSION,"Low usbipd-win version");
            }
        }
        else
        {
            USBIPD_INSTALL_PATH = string.Empty;
            log.Warn($"Failed to parse USBIPD version: {stdout}");
            return (ExitCode.ParseError, "", "");
        }

        return (ExitCode.Success, USBIPD_INSTALL_PATH,USBIPD_VERSION);
    }


    private static async Task<(ExitCode ExitCode, string StandardOutput, string StandardError)>
        RunPowerShellScripts(string script, int timeout_ms)
    {
        var stdout = string.Empty;
        var stderr = string.Empty;
        ExitCode exitCode = ExitCode.Failure;
        (exitCode, stdout, stderr) = await CheckUsbipdWinInstallation();
        if (exitCode != ExitCode.Success) {
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
            return new(ExitCode.Failure, "", $"Failed to start \"usbipd\" with arguments {script}.");
        }
        if(process.ExitCode != 0)
        {
            log.Error($"Failed to run powershell scripts, error: {stderr}");
            return new(ExitCode.Failure, stdout, stderr);
        }
        return new(ExitCode.Success, stdout, stderr);
    }

    private static async Task<(ExitCode ExitCode, string StandardOutput, string StandardError)>
        RunUSBIPD(bool privilege, string[] arguments)
    {
        var stdout = string.Empty;
        var stderr = string.Empty;
        ProcessStartInfo startInfo;
        ExitCode exitCode = ExitCode.Failure;
        (exitCode, stdout, stderr) = await CheckUsbipdWinInstallation();
        if (exitCode != ExitCode.Success)
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
            return new(ExitCode.Failure, "", $"Failed to start \"usbipd\" with arguments {string.Join(" ", arguments.Select(arg => $"\"{arg}\""))}.");
        }
        if(process.ExitCode != 0)
        {
            log.Error($"Failed to run '{startInfo.FileName} {string.Join(" ", arguments.Select(arg => $"\"{arg}\""))}', error: {stderr}");
            return new(ExitCode.Failure, stdout, stderr);
        }
        return new(ExitCode.Success, stdout, stderr);
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
        ExitCode ret = ExitCode.BindError;
        for (int i= 0; i < 3; i++){
            (ret, string stdout, stderr) = 
                await RunUSBIPD(true, ["bind", "--hardware-id", hardwareid, (force ? "--force" : "")]);
            if (ret == ExitCode.Success)
            {
                (_, _, dev) = await GetUSBDeviceByHardwareID(hardwareid, true);
                return (ret, "", dev);
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
        
        return ((int)ret >= (int)ExitCode.NotFound ? ret : ExitCode.BindError, stderr,dev);
    }


    public static async Task<(ExitCode exitCode, string errMsg, USBDevice? newDev)> 
        BindDevice(string hardwareid) => await BindDevice(hardwareid, false);


    public static async Task<(ExitCode exitCode, string errMsg, USBDevice? newDev)>
        UnbindDevice(string? hardwareid)
    {
        ExitCode exitCode = ExitCode.UnbindError;
        string stderr = "";
        USBDevice? dev = null;
        for (int i= 0; i < 3; i++)
        {
            if (string.IsNullOrEmpty(hardwareid))
            {
                (exitCode, _, stderr) = await RunUSBIPD(true, ["unbind", "--all"]);
            }
            else
            {
                (exitCode, _, stderr) = await RunUSBIPD(true, ["unbind", "--hardware-id", hardwareid]);
                (_, _, dev) = await GetUSBDeviceByHardwareID(hardwareid, true);
            }

            if (exitCode == ExitCode.Success)
            {
                return (exitCode, "",dev);
            }
            else
            {
                log.Warn($"Failed to unbind device '{hardwareid}': {stderr}");
            }
        }

        return ((int)exitCode >= (int)ExitCode.NotFound ? exitCode : ExitCode.UnbindError, stderr, dev);
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
        ExitCode exitCode = ExitCode.DetachError;
        string stderr = "";
        USBDevice? dev = null;
        for (int i= 0; i< 3; i++)
        {
            if (string.IsNullOrEmpty(hardwareid))
            {
                (exitCode, _, stderr) = await RunUSBIPD(false, ["detach", "--all"]);
            }
            else
            {
                log.Warn($"Failed to detach device '{hardwareid}': {stderr}");
                (exitCode, _, stderr) = await RunUSBIPD(false, ["detach", "--hardware-id", hardwareid]);
                (_, _, dev) = await GetUSBDeviceByHardwareID(hardwareid, true);
            }

            if (exitCode == 0)
            {
                return (ExitCode.Success, "", dev);
            }
        }
        
        return  ((int)exitCode >= (int)ExitCode.NotFound ? exitCode : ExitCode.DetachError, stderr, dev);
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
        var (exitCode, stdout, stderr) = await RunPowerShellScripts(CmdGetAllDevices, 2000);

        if (exitCode != ExitCode.Success)
        {
            log.Warn($"Failed to fetch USB device list: {stderr}");
            return (exitCode, stderr, []);
        }

        return (ExitCode.Success, "", ParseStringDevInfoToUSBDeviceList(stdout));
    }

    public static async Task<(ExitCode exitCode, string errMsg, List<USBDevice>? devList)> 
        ListConnectedDevices()
    {
        string cmd = $"{CmdGetAllDevices} | Where-Object {{$_.IsConnected}}";
        var(exitCode, stdout, stderr) = await RunPowerShellScripts(cmd, 2000);

        if (exitCode != ExitCode.Success)
        {
            log.Warn($"Failed to fetch connected devices: {stderr}");
            return (exitCode, stderr, []);
        }

        return (ExitCode.Success, "", ParseStringDevInfoToUSBDeviceList(stdout));
    }

    public static async Task<(ExitCode exitCode, string errMsg, List<USBDevice>? devList)> 
        ListPersistedDevices()
    {
        string cmd = $"{CmdGetAllDevices} | Where-Object {{-not $_.IsConnected -and $_.IsBound}}";
        var (exitCode, stdout, stderr) = await RunPowerShellScripts(cmd, 2000);

        if (exitCode != 0 || stdout.Length == 0)
        {
            log.Warn($"Failed to fetch persisted devices: {stderr}");
            return (exitCode, stderr, []);
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
