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

namespace wsl_usb_manager.Controller;

public partial class USBIPD
{
    private static readonly string CmdGetAllDevices = @"Import-Module $env:ProgramW6432'\usbipd-win\PowerShell\Usbipd.Powershell.dll';Get-UsbipdDevice";
    private static readonly ILog log = LogManager.GetLogger(typeof(USBIPD));

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

    public static async Task<(ExitCode exitCode, string errMsg)> InitUSBIPD()
    {
        (bool IsInstalled, string InstallPath) = await CheckUsbipdWinInstallation();
        if (!IsInstalled)
        {
            return (ExitCode.Failure, "usbipd-win is not installed.");
        }
        USBIPD_INSTALL_PATH = InstallPath;
        return (ExitCode.Success, "");
    }

    public static string GetUSBIPDInstallPath() => USBIPD_INSTALL_PATH;
    public static async Task<(bool IsInstalled, string InstallPath)> CheckUsbipdWinInstallation()
    {
        string script = @"
            $usbipdPath = (Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*' | Where-Object { $_.DisplayName -eq 'usbipd-win' }).InstallLocation
            if ($usbipdPath) {
                Write-Output $usbipdPath
            } else {
                Write-Output ''
            }
        ";

        (int exitCode, string stdout, _) = await RunPowerShellScripts(script, 5000);

        if (exitCode == 0 && !string.IsNullOrWhiteSpace(stdout))
        {
            return (true, stdout.Trim());
        }

        return (false, string.Empty);
    }


    private static async Task<(int ExitCode, string StandardOutput, string StandardError)>
        RunPowerShellScripts(string script, int timeout_ms)
    {
        var stdout = string.Empty;
        var stderr = string.Empty;
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

        return new(process.ExitCode, stdout, stderr);
    }

    private static async Task<(int ExitCode, string StandardOutput, string StandardError)>
        RunUSBIPD(bool privilege, string[] arguments)
    {
        var stdout = string.Empty;
        var stderr = string.Empty;
        ProcessStartInfo startInfo;
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

        return new(process.ExitCode, stdout, stderr);
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
        for (int i= 0; i < 3; i++){
            (int exitCode, string stdout, stderr) = 
                await RunUSBIPD(true, ["bind", "--hardware-id", hardwareid, (force ? "--force" : "")]);
            if (exitCode == 0)
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
        
        return (ExitCode.Failure, stderr,dev);
    }


    public static async Task<(ExitCode exitCode, string errMsg, USBDevice? newDev)> 
        BindDevice(string hardwareid) => await BindDevice(hardwareid, false);


    public static async Task<(ExitCode exitCode, string errMsg, USBDevice? newDev)>
        UnbindDevice(string? hardwareid)
    {
        int exitCode;
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

            if (exitCode == 0)
            {
                return (ExitCode.Success, "",dev);
            }
            else
            {
                log.Warn($"Failed to unbind device '{hardwareid}': {stderr}");
            }
        }

        return (ExitCode.Failure, stderr, dev);
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
        int exitCode;
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
        
        return  (ExitCode.Failure, stderr, dev);
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
        (int exitCode, string stdout, string stderr) = await RunPowerShellScripts(CmdGetAllDevices, 2000);

        if (exitCode != 0 || stdout.Length == 0)
        {
            log.Warn($"Failed to fetch USB device list: {stderr}");
            return (ExitCode.Failure, stderr, []);
        }

        return (ExitCode.Success, "", ParseStringDevInfoToUSBDeviceList(stdout));
    }

    public static async Task<(ExitCode exitCode, string errMsg, List<USBDevice>? devList)> 
        ListConnectedDevices()
    {
        string cmd = $"{CmdGetAllDevices} | Where-Object {{$_.IsConnected}}";
        (int exitCode, string stdout, string stderr) = await RunPowerShellScripts(cmd, 2000);

        if (exitCode != 0 || stdout.Length == 0)
        {
            log.Warn($"Failed to fetch connected devices: {stderr}");
            return (ExitCode.Failure, stderr, []);
        }

        return (ExitCode.Success, "", ParseStringDevInfoToUSBDeviceList(stdout));
    }

    public static async Task<(ExitCode exitCode, string errMsg, List<USBDevice>? devList)> 
        ListPersistedDevices()
    {
        string cmd = $"{CmdGetAllDevices} | Where-Object {{-not $_.IsConnected -and $_.IsBound}}";
        (int exitCode, string stdout, string stderr) = await RunPowerShellScripts(cmd, 2000);

        if (exitCode != 0 || stdout.Length == 0)
        {
            log.Warn($"Failed to fetch persisted devices: {stderr}");
            return (ExitCode.Failure, stderr, []);
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
}
