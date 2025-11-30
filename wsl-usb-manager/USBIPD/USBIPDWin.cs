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
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography.Xml;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Automation.Text;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Xml.Linq;
using wsl_usb_manager.Domain;
using wsl_usb_manager.Resources;

namespace wsl_usb_manager.USBIPD;

public enum ErrorCode
{
    DeviceDetachFailed = -26,
    DeviceUnbindFailed = -25,
    DeviceAttachFailed = -24,
    DeviceBindFailed = -23,
    DeviceNotAttached = -22,
    DeviceNotBound = -21,
    DeviceNotConnected = -20,
    USBIPDLowVersion = -11,
    USBIPDNotFound = -10,
    WslDistribNotFound = -4,
    WslLowVersion = -3,
    WslNotRunning = -2,
    WslNotInstalled = -1,
    Success = 0,
    Failure = 1,
    ParseError = 2,
    AccessDenied = 3,
    Timeout = 4,
    UnknownError = 255,
};

public static class CtrlCUtil
{
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GenerateConsoleCtrlEvent(uint dwCtrlEvent, uint dwProcessGroupId);

    public static void SendCtrlC(Process process)
    {
        if (process == null || process.HasExited)
            return;

        // Attach to the console of the target process
        if (AttachConsole((uint)process.Id))
        {
            try
            {
                // Send CTRL+C to the process group
                GenerateConsoleCtrlEvent(0, 0);
            }
            finally
            {
                FreeConsole();
            }
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(uint dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
    private static extern bool FreeConsole();
}

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

    [GeneratedRegex(@"^[a-zA-Z]:\\")]
    private static partial Regex LocalDriveRegex();

    private const ushort USBIP_PORT = 3240;
    private static ApplicationInfo? USBIPDAppInfo = null;
    private static bool IsChinese() => Lang.IsChinese();

    private static readonly List<Process> ProcessList = [];
    private static readonly object ProcessListLock = new();

    private static bool IsSuccess(ErrorCode errCode) => errCode == ErrorCode.Success;

    private static bool IsFailed(ErrorCode errCode) => errCode != ErrorCode.Success;

    private static ErrorCode ExitCodeToErroCode(int ExitCode)
    {
        return ExitCode switch
        {
            0 => ErrorCode.Success,
            1 => ErrorCode.Failure,
            2 or 3 => ErrorCode.USBIPDNotFound,
            5 or 126 => ErrorCode.AccessDenied,
            _ => ErrorCode.UnknownError,
        };
    }

    private static void ForceStopProcess(Process process)
    {
        try
        {
            if(process.HasExited)
            {
                return;
            }
            //CtrlCUtil.SendCtrlC(process);
            process.Kill(true);
            //if (!process.WaitForExit(5000))
            //{
            //    process.Kill(true);
            //    log.Info($"Process '{process.StartInfo.FileName} {process.StartInfo.Arguments}'({process.Id}) has been forcibly stopped.");
            //}
            //else
            //{
            //    log.Info($"Process '{process.StartInfo.FileName} {process.StartInfo.Arguments}'({process.Id}) has been stopped by CTRL+C.");
            //}
            process.CancelOutputRead();
            process.CancelErrorRead();
        }
        catch (Exception ex)
        {
            log.Error($"Failed to forcibly stop the process '{process.StartInfo.FileName} {process.StartInfo.Arguments}'({process.Id}): {ex.Message}");
        }
        finally
        {
            process.Dispose();
        }
    }

    private static void AddtoProcessList(Process process)
    {
        lock (ProcessListLock)
        {
            ProcessList.Add(process);
        }
    }

    private static void RemoveFromProcessList(Process process)
    {
        lock (ProcessListLock)
        {
            foreach (Process p in ProcessList)
            {
                if (p.StartInfo.Arguments.Equals(process.StartInfo.Arguments, StringComparison.CurrentCultureIgnoreCase))
                {
                    if (!p.HasExited)
                    {
                        log.Info($"Stopping 'usbipd.exe {process.StartInfo.Arguments}'");
                        ForceStopProcess(process);
                    }
                    log.Warn($"'usbipd.exe {process.StartInfo.Arguments}' has been removed from list.");
                    ProcessList.Remove(p);
                    break;
                }
            }
        }
    }

    private static bool IsInProcessList(Process process)
    {
        lock (ProcessListLock)
        {
            foreach (Process p in ProcessList)
            {
                if (p.StartInfo.Arguments.Equals(process.StartInfo.Arguments, StringComparison.CurrentCultureIgnoreCase))
                {
                    if (!p.HasExited)
                    {
                        log.Info($"'usbipd.exe {process.StartInfo.Arguments}' is in running!");
                        return true;
                    }
                    log.Warn($"'usbipd.exe {process.StartInfo.Arguments}' has exited, removing it ...");
                    ProcessList.Remove(p);
                    break;
                }
            }
        }
        return false;
    }

    private static async Task<(ErrorCode ErrCode, string StandardOutput, string StandardError)>
    RunProcessWithTimeout(Process process, int timeoutMilliseconds)
    {
        var stdoutBuilder = new StringBuilder();
        var stderrBuilder = new StringBuilder();
        string StdOut = "", StdErr = "";

        if (process.StartInfo.RedirectStandardOutput)
        {
            process.OutputDataReceived += (s, e) => { if (e.Data != null) stdoutBuilder.AppendLine(e.Data); };
        }
        if (process.StartInfo.RedirectStandardError)
        {
            process.ErrorDataReceived += (s, e) => { if (e.Data != null) stderrBuilder.AppendLine(e.Data); };
        }
        try
        {
            //if (string.IsNullOrWhiteSpace(process.StartInfo.Verb))
            //{
            //    process.StartInfo.EnvironmentVariables["CREATE_NEW_CONSOLE"] = "1";
            //}
            process.Start();
            if (process.StartInfo.RedirectStandardOutput)
            {
                process.BeginOutputReadLine();
            }
            if (process.StartInfo.RedirectStandardError)
            {
                process.BeginErrorReadLine();
            }
            if (timeoutMilliseconds > 0)
            {
                var processTask = process.WaitForExitAsync();
                var timeoutTask = Task.Delay(timeoutMilliseconds);

                var completedTask = await Task.WhenAny(processTask, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    ForceStopProcess(process);
                    //await process.WaitForExitAsync();
                    return (ErrorCode.Timeout, stdoutBuilder.ToString(), stderrBuilder.ToString());
                }
            }
            else
            {
                await process.WaitForExitAsync();
            }

            if (process.StartInfo.RedirectStandardOutput)
            {
                StdOut = stdoutBuilder.ToString();
            }
            if (process.StartInfo.RedirectStandardError)
            {
                StdErr = stderrBuilder.ToString();
            }

            return (ExitCodeToErroCode(process.ExitCode), StdOut, StdErr);
        }
        catch (Exception ex)
        {
            StdErr = ex.ToString();
            return (ErrorCode.UnknownError, StdOut, StdErr);
        }
        finally
        {
            process.Dispose();
        }
    }

    private static async Task<(ErrorCode ErrCode, string StandardOutput, string StandardError)> 
        RunUSBIPDWin(bool privilege, bool daemon, params string[] arguments)
      {
        var ErrCode = ErrorCode.Failure;
        string StdOutput = "", StdError = "";

        var startInfo = new ProcessStartInfo
        {
            FileName = Path.Combine(GetUSBIPDInstallPath(), "usbipd.exe"),
            Arguments = $"{arguments.Aggregate((s1, s2) => s1 + " " + s2)}",
            UseShellExecute = privilege,
            RedirectStandardOutput = !privilege,
            RedirectStandardError = !privilege,
            Verb = privilege ? "runas" : "",
            CreateNoWindow = true,
        };

        var process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };

        if (daemon)
        {
            if (IsInProcessList(process))
            {
                return (ErrorCode.Success, "", "");
            }
            AddtoProcessList(process);
        }

        try
        {
            (ErrCode, StdOutput, StdError) = await RunProcessWithTimeout(process, daemon ? -1 : 5000);
        }
        finally
        {
            if (daemon)
            {
                RemoveFromProcessList(process);
            }
            if (ErrCode != ErrorCode.Success)
            {
                log.Error($"Failed to run '{process.StartInfo.FileName} {process.StartInfo.Arguments}.\r\n Stdout: {StdOutput}\r\n; Stderr: {StdError}");
            }
            string[] newLineSeparators = ["\r\n", "\r", "\n"];
            var Outlines = StdOutput.Split(newLineSeparators, StringSplitOptions.RemoveEmptyEntries);
            var ErrLines = StdError.Split(newLineSeparators, StringSplitOptions.RemoveEmptyEntries);
            StdOutput = "";
            StdError = "";
            var index = 0;
            foreach (var l in Outlines)
            {
                if ((index = l.IndexOf("info:")) >= 0)
                {
                    index += 5;
                    StdOutput += $"{l[index..].Trim()}\r\n";
                }
                else if ((index = l.IndexOf("error:")) >= 0)
                {
                    index += 6;
                    StdError += $"{l[index..].Trim()}\r\n";
                }
                else
                { StdOutput += l; }
            }
            foreach (var l in ErrLines)
            {
                if ((index = l.IndexOf("info:")) >= 0)
                {
                    index += 5;
                    StdOutput += $"{l[index..].Trim()}\r\n";
                }
                else if ((index = l.IndexOf("error:")) >= 0)
                {
                    index += 6;
                    StdError += $"{l[index..].Trim()}\r\n";
                }
                else { StdError += l; }
            }
        }
        return (ErrCode, StdOutput, StdError);
    }

    private static async Task<(ErrorCode ErrCode, string StandardOutput, string StandardError)> 
        RunUSBIPDWin(bool privilege, params string[] arguments) =>  await RunUSBIPDWin(privilege, false, arguments);

    private static async Task<(ErrorCode ErrCode, string StandardOutput, string StandardError)>
        RunUSBIPDWin(params string[] arguments) => await RunUSBIPDWin(false, false, arguments);

    private static async Task<(ErrorCode ErrCode, string StandardOutput, string StandardError)>
        RunUSBIPDWinDaemon(bool privilege, params string[] arguments) => await RunUSBIPDWin(privilege, true, arguments);

    private static async Task<(ErrorCode ErrCode, string StandardOutput, string StandardError)>
        RunPowerShellScripts(string scripts)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command {scripts}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        var process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };

        return await RunProcessWithTimeout(process, 5000);
    }

    private static async Task<(ErrorCode ErrCode, string ErrMsg)> CheckUSBIPDWin()
    {
        var stderr = string.Empty;

        if (USBIPDAppInfo is not null)
        {
            return new(ErrorCode.Success, stderr);
        }

        USBIPDAppInfo = ApplicationChecker.GetApplicationInfo("usbipd-win");

        if (USBIPDAppInfo == null || string.IsNullOrWhiteSpace(USBIPDAppInfo.InstallLocation) || string.IsNullOrWhiteSpace(USBIPDAppInfo.DisplayName))
        {
            stderr = $"{(IsChinese() ? ErrUsbipNotInstalledZH : ErrUsbipNotInstalledEN)}";
            log.Error(stderr);
            USBIPDAppInfo = null;
            return (ErrorCode.USBIPDNotFound, stderr);
        }
        USBIPDAppInfo.InstallLocation = USBIPDAppInfo.InstallLocation.Trim();
        if (!Directory.Exists(USBIPDAppInfo.InstallLocation))
        {
            stderr = $"{(IsChinese() ? ErrUsbipNotInstalledZH : ErrUsbipNotInstalledEN)}";
            log.Error(stderr);
            USBIPDAppInfo = null;
            return (ErrorCode.USBIPDNotFound, stderr);
        }

        if (string.IsNullOrWhiteSpace(USBIPDAppInfo.DisplayVersion))
        {
            var (_, StandardOutput, StandardError) = await RunUSBIPDWin(["--version"]);

            USBIPDAppInfo.DisplayVersion = StandardOutput.Trim();
            stderr = StandardError;
        }

        if (string.IsNullOrWhiteSpace(USBIPDAppInfo.DisplayVersion))
        {
            if (IsChinese())
                stderr = $"无法检查\"usbipd\"版本： {stderr}。";
            else
                stderr = $"Failed to check \"usbipd\" version: {stderr}";
            log.Error(stderr);
            USBIPDAppInfo = null;
            return new(ErrorCode.Failure, stderr);
        }
        log.Info($"USBIPD version: {USBIPDAppInfo.DisplayVersion}");
        Match match = VersionRegex().Match(USBIPDAppInfo.DisplayVersion);
        if (match.Success)
        {
            int major = int.Parse(match.Groups[1].Value);
            int minor = int.Parse(match.Groups[2].Value);
            int patch = int.Parse(match.Groups[3].Value);
            USBIPDAppInfo.DisplayVersion = $"{major}.{minor}.{patch}";
            Match requestVersion = VersionRegex().Match(UsbipdMinVersion);
            if (requestVersion.Success)
            {
                int minimajor = int.Parse(requestVersion.Groups[1].Value);
                int miniminer = int.Parse(requestVersion.Groups[2].Value);

                if (major < minimajor || (major == minimajor && minor < miniminer))
                {
                    stderr = $"{(IsChinese() ? ErrUsbipLowVersionZH : ErrUsbipLowVersionEN)}";

                    log.Error(stderr);
                    USBIPDAppInfo = null;
                    return (ErrorCode.USBIPDLowVersion, stderr);
                }
                else if (major == minimajor)
                {
                    UsbipdPowershellModulePath = Path.Combine(USBIPDAppInfo.InstallLocation, "PowerShell", "Usbipd.Powershell.dll");
                }
                else
                {
                    UsbipdPowershellModulePath = Path.Combine(USBIPDAppInfo.InstallLocation, "Usbipd.Powershell.dll");
                }
            }
            else
            {
                if (IsChinese())
                    stderr = $"无法解析请求的\"usbipd\"版本： {UsbipdMinVersion}。";
                else
                    stderr = $"Failed to parse requested USBIPD version: {UsbipdMinVersion}";
                log.Error(stderr);
                USBIPDAppInfo = null;
                return (ErrorCode.ParseError, stderr);
            }
        }
        else
        {
            if (IsChinese())
                stderr = $"无法解析\"usbipd\"版本： {USBIPDAppInfo.DisplayVersion}。";
            else
                stderr = $"Failed to parse USBIPD version: {USBIPDAppInfo.DisplayVersion}";
            log.Error(stderr);

        }


        if ((Path.GetPathRoot(USBIPDAppInfo.InstallLocation) is not string wslWindowsPathRoot) || (!LocalDriveRegex().IsMatch(wslWindowsPathRoot)))
        {
            stderr = (IsChinese() ? ErrUsbipLocationZH : ErrUsbipLocationEN);
            log.Warn(stderr);
        }

        log.Info($"USBIPD version is {USBIPDAppInfo.DisplayVersion}.");
        log.Info($"USBIPD powershell module path: {UsbipdPowershellModulePath}");

        return (ErrorCode.Success, stderr);
    }

    public static string GetUSBIPDInstallPath() => USBIPDAppInfo?.InstallLocation ?? string.Empty;

    public static string GetUSBIPDVersion() => USBIPDAppInfo?.DisplayVersion ?? string.Empty;

    public static async Task StopAllProcesses()
    {
        foreach (var p in ProcessList)
        {
            log.Debug($"Process: {p.StartInfo.FileName} {p.StartInfo.Arguments}");
            if (!p.HasExited)
            {
                log.Info($"Stopping '{p.StartInfo.FileName} {p.StartInfo.Arguments}' ...");
                 ForceStopProcess(p);
            }
        }
        ProcessList.Clear();
        var (_, distribution, _) = await GetRunningDistribution();
        if (!string.IsNullOrWhiteSpace(distribution))
        {
            await RunWslLinuxCmdAsync(distribution, "pkill", "-f", "auto-attach.sh");
            await RunWslLinuxCmdAsync(distribution, "pkill", "-f", "usbip-auto-attach");
        }
    }

}
