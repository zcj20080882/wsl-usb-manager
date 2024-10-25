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
using System.Text.RegularExpressions;

namespace wsl_usb_manager.Controller;

public partial class USBIPD
{
    private static readonly string CmdGetAllDevices = @"Import-Module $env:ProgramW6432'\usbipd-win\PowerShell\Usbipd.Powershell.dll';Get-UsbipdDevice";
    private static readonly string[] separator = [""];
    private static readonly char[] separatorOfDictionary = [':'];
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
    public USBIPD()
    {

    }

    public static async Task<(ExitCode, string)> InitUSBIPD()
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

        (int exitCode, string stdout, string stderr) = await RunPowerShellScripts(script, 5000);

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
        //Run as administrator example:
        //Start-Process <process> -ArgumentList '<ArgumentList>' -Verb runAs -WindowStyle Hidden
        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"Start-Process usbipd -ArgumentList '{arguments.Aggregate((s1, s2) => s1 + " " + s2)}' -WindowStyle Hidden",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        if (privilege)
        {
            startInfo.Arguments += " -Verb RunAs";
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
                    stderr = reader.ReadLine()?.Replace("Start-Process","").Trim(':',' ');
                }
            }
        });

        if (process == null)
        {
            return new((int)ExitCode.Failure, "", $"Failed to start \"usbipd\" with arguments {string.Join(" ", arguments.Select(arg => $"\"{arg}\""))}.");
        }
        
        return new(process.ExitCode, stdout, stderr);
    }

    /**
     * Bind a device with a hardware ID.
     */
    public static async Task<(ExitCode, string)> 
        BindDevice(string hardwareid, bool force)
    {
        (int exitCode, string stdout, string stderr) = await RunUSBIPD(true, ["bind", "--hardware-id", hardwareid, (force ? "--force" : "")]);
        if(exitCode == 0)
        {
            return (ExitCode.Success,"");
        }
        await Task.Run(() => Thread.Sleep(200));

        if(await IsDeviceBound(hardwareid))
        {
            return (ExitCode.Success, "");
        }
        return (ExitCode.Failure, stderr);
    }


    public static async Task<(ExitCode, string)> BindDevice(string hardwareid) => await BindDevice(hardwareid,false);


    public static async Task<(ExitCode, string)> 
        UnbindDevice(string? hardwareid)
    {
        int exitCode;
        string stderr;
        if (string.IsNullOrEmpty(hardwareid))
        {
            (exitCode, _, stderr) = await RunUSBIPD(true, ["unbind", "--all"]);
        }
        else
        {
            (exitCode, _, stderr) = await RunUSBIPD(true, ["unbind", "--hardware-id", hardwareid]);
        }

        if (exitCode == 0)
        {
            return (ExitCode.Success, "");
        }
        
        return (ExitCode.Failure, stderr);
    }

    public static async Task<(ExitCode, string)> UnbindAllDevice() => await UnbindDevice(null);

    public static async Task<(ExitCode, string)>
        DetachDevice(string? hardwareid)
    {
        int exitCode;
        string stderr;
        if (string.IsNullOrEmpty(hardwareid))
        {
            (exitCode, _, stderr) = await RunUSBIPD(false, ["detach", "--all"]);
        }
        else
        {
            (exitCode, _, stderr) = await RunUSBIPD(false, ["detach", "--hardware-id", hardwareid]);
        }

        if (exitCode == 0)
        {
            return (ExitCode.Success, "");
        }

        return (ExitCode.Failure, stderr);
    }

    public static async Task<(ExitCode, string)> DetachAllDevice() => await DetachDevice(null);


    public static async Task<(ExitCode,string, List<USBDevice>?)> GetAllUSBDevices()
    {
        List<USBDevice> deviceslist = [];
        (int exitCode,string stdout,string stderr) = await RunPowerShellScripts(CmdGetAllDevices,2000);

        if (exitCode != 0 || stdout.Length == 0)
        {
            return (ExitCode.Failure, stderr, deviceslist);
        }

        string pattern = @"\r?\n\s*\r?\n";
        string[] blocks = Regex.Split(stdout.Trim(['\r', '\n', ' ', '\t']), pattern);

        foreach (string block in blocks)
        {
            if (DeviceInfoRegex().Match(block.Trim(['\r', '\n', ' ', '\t'])) is Match match)
            {
                string busId = match.Groups["BusId"].Value.Trim();
                if (!bool.TryParse(match.Groups["IsForced"].Value.Trim(), out bool isForced))
                {
                    string t = match.Groups["IsForced"].Value.Trim();
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

                deviceslist.Add(deviceInfo);
            }
        }
        
        return (ExitCode.Success, stdout, deviceslist);
    }

    public static async Task<(ExitCode, string, List<USBDevice>?)> GetAllConnectedDevices()
    {
        List<USBDevice> deviceslist = [];
        string cmd = $"{CmdGetAllDevices} | Where-Object {{$_.IsConnected}}";
        (int exitCode, string stdout, string stderr) = await RunPowerShellScripts(cmd, 2000);

        if (exitCode != 0 || stdout.Length == 0)
        {
            return (ExitCode.Failure, stderr, deviceslist);
        }

        string pattern = @"\r?\n\s*\r?\n";
        string[] blocks = Regex.Split(stdout.Trim(['\r', '\n', ' ', '\t']), pattern);

        foreach (string block in blocks)
        {
            if (DeviceInfoRegex().Match(block.Trim(['\r', '\n', ' ', '\t'])) is Match match)
            {

                if (!bool.TryParse(match.Groups["IsForced"].Value.Trim(), out bool isForced))
                {
                    string t = match.Groups["IsForced"].Value.Trim();
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
                    BusId = match.Groups["BusId"].Value.Trim(),
                    PersistedGuid = match.Groups["PersistedGuid"].Value.Trim(),
                    StubInstanceId = match.Groups["StubInstanceId"].Value.Trim(),
                    ClientIPAddress = match.Groups["ClientIPAddress"].Value.Trim(),
                    IsBound = isBound,
                    IsConnected = true,
                    IsAttached = isAttached
                };

                deviceslist.Add(deviceInfo);
            }
        }

        return (ExitCode.Success, stdout, deviceslist);
    }

    public static async Task<bool> IsDeviceConnected(string? hardwareId)
    {
        (ExitCode ret, _, List<USBDevice>? infolist) = await GetAllConnectedDevices();
        if(ret == 0 && infolist != null)
        {
            return infolist.Any(x => string.Equals(x.HardwareId, hardwareId, StringComparison.OrdinalIgnoreCase));
        }
        return false;
    }

    public static async Task<bool> IsDeviceBound(string? hardwareId)
    {
        (ExitCode _, _, List<USBDevice>? infolist) = await GetAllConnectedDevices();
        USBDevice? dev = infolist?.Find(x => string.Equals(x.HardwareId, hardwareId, StringComparison.OrdinalIgnoreCase));
        if (dev != null)
        {
            return dev.IsBound;
        }
        return false;
    }
}
