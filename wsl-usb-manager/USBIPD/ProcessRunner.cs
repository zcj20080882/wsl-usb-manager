/******************************************************************************
* SPDX-License-Identifier: MIT
* Project: wsl-usb-manager
* Class: ProcessRunner.cs
* NameSpace: wsl_usb_manager.USBIPD
* Author: Chuckie
* copyright: Copyright (c) Chuckie, 2025
* Description:
* Create Date: 2025/3/5 21:06
******************************************************************************/
using log4net;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Text;
using wsl_usb_manager.Domain;

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

public class ProcessRunner
{
    private string name = "";
    private Process? process = null;
    static readonly string _wslPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "wsl.exe");
    static readonly char[] CtrlC = ['\x03'];
    private CancellationTokenSource cancellationTokenSource = new();

    private static readonly ILog log = LogManager.GetLogger(typeof(ProcessRunner));


    private static async Task<(string StandardOutput, string StandardError, MemoryStream BinaryOutput)>
        CaptureProcessOutput(Process process, Action<string, bool>? outputCallback, bool binaryOutput, CancellationToken cancellationToken)
    {
        string stdout = "";
        string stderr = "";
        var memoryStream = new MemoryStream();
        var callbackLock = new object();

        async Task CaptureBinary(Stream stream)
        {
            await process.StandardOutput.BaseStream.CopyToAsync(memoryStream, cancellationToken);
            memoryStream.Position = 0;
        }

        async Task CaptureText(StreamReader streamReader, bool isStandardError)
        {
            while (await streamReader.ReadLineAsync(cancellationToken) is string line)
            {
                if (outputCallback is not null)
                {
                    // prevent stderr/stdout collisions
                    lock (callbackLock)
                    {
                        outputCallback(line, isStandardError);
                    }
                }
                // Note that this normalizes the line endings.
                if (isStandardError)
                {
                    stderr += line + '\n';
                }
                else
                {
                    stdout += line + '\n';
                }
            }
        }

        var captureTasks = new[]
        {
            binaryOutput ? CaptureBinary(process.StandardOutput.BaseStream) : CaptureText(process.StandardOutput, false),
            CaptureText(process.StandardError, true),
        };

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        finally
        {
            process.Kill(true);
        }
        // Since the process either completed or was killed, these should complete or cancel promptly.
        await Task.WhenAll(captureTasks);
        return (stdout, stderr, memoryStream);
    }

    public string Name => name;
    public static string WslPath => _wslPath;

    public ProcessRunner(string name)
    {
        this.name = name;
    }

    public ProcessRunner()
    {

    }

    public static bool IsRunningAsAdministrator()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        WindowsPrincipal principal = new(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public async Task<(int ExitCode, string StandardOutput, string StandardError)>
        RunUSBIPD(int millisecondsTimeout, bool privilege, Action<string, bool>? outputCallback, params string[] arguments)
    {
        ProcessStartInfo startInfo;
        var stdout = string.Empty;
        var stderr = string.Empty;
        CancellationToken cancellationToken = cancellationTokenSource.Token;
        cancellationTokenSource.TryReset();
        var exePath = Path.Combine(USBIPDWin.GetUSBIPDInstallPath(), "usbipd.exe");
        if (privilege && !IsRunningAsAdministrator())
        {
            //Run as administrator example:
            //Start-Process <process> -ArgumentList '<ArgumentList>' -Verb runAs -WindowStyle Hidden
            startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"Start-Process '{exePath}' -ArgumentList '{arguments.Aggregate((s1, s2) => s1 + " " + s2)}' -WindowStyle Hidden -Verb RunAs",
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
                FileName = $"{exePath}",
                Arguments = $"{arguments.Aggregate((s1, s2) => s1 + " " + s2)}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
        }

        process = Process.Start(startInfo);
        if (process == null)
        {
            stderr = $"Failed to start \"{startInfo.FileName}\" with arguments {string.Join(" ", arguments.Select(arg => $"\"{arg}\""))}.";
            log.Error(stderr);
            return new(-1, "", stderr);
        }

        if (millisecondsTimeout > 0)
            cancellationToken = new CancellationTokenSource(millisecondsTimeout).Token;
        (stdout, stderr, _) = await CaptureProcessOutput(process, outputCallback, false, cancellationToken);
        if (process.ExitCode != 0)
        {
            log.Error($"Failed to run '{startInfo.FileName} {string.Join(" ", arguments.Select(arg => $"\"{arg}\""))}', error: {stderr}");
            return new(process.ExitCode, stdout, stderr);
        }

        return new(process.ExitCode, stdout, stderr);
    }
    public async Task<(int ExitCode, string StandardOutput, string StandardError)>
        RunUSBIPD(bool privilege, Action<string, bool>? outputCallback, params string[] arguments) => 
        await RunUSBIPD(-1, privilege, outputCallback, arguments);

    public async Task<(int ExitCode, string StandardOutput, string StandardError)>
        RunUSBIPD(bool privilege, params string[] arguments) => 
        await RunUSBIPD(-1, privilege, null, arguments);

    public async Task<(int ExitCode, string StandardOutput, string StandardError)>
        RunPowerShellScripts(int millisecondsTimeout, string scripts)
    {
        var stdout = string.Empty;
        var stderr = string.Empty;
        CancellationToken cancellationToken = cancellationTokenSource.Token;
        cancellationTokenSource.TryReset();

        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command {scripts}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        process = Process.Start(startInfo);
        if (process is null)
        {
            stderr = $"Cannot run powershell.exe with args \"{startInfo.ArgumentList}\"";
            log.Error(stderr);
            return new(-1, "", stderr);
        }

        if (millisecondsTimeout > 0)
            cancellationToken = new CancellationTokenSource(millisecondsTimeout).Token;
        (stdout, stderr, _) = await CaptureProcessOutput(process, null, false, cancellationToken);
        if (process.ExitCode != 0)
        {
            log.Error($"Failed to run '{startInfo.FileName} {scripts}', error: {stderr}");
            return new(process.ExitCode, stdout, stderr);
        }

        return new(process.ExitCode, stdout, stderr);
    }
    public async Task<(int ExitCode, string StandardOutput, string StandardError)>
       RunPowerShellScripts(string scripts) => await RunPowerShellScripts(-1, scripts);

    public async Task<(int ExitCode, string StandardOutput, string StandardError, MemoryStream BinaryOutput)>
        RunWslAsync(int millisecondsTimeout, (string distribution, string directory)? linux, Action<string, 
            bool>? outputCallback, bool binaryOutput, params string[] arguments)
    {
        CancellationToken cancellationToken = cancellationTokenSource.Token;
        cancellationTokenSource.TryReset();

        var stdout = string.Empty;
        var stderr = string.Empty;
        var memoryStream = new MemoryStream();

        if (!File.Exists(WslPath))
        {
            stderr = "WSL is not installed.";
            log.Error(stderr);
            return ((int)ErrorCode.WslNotInstalled, "", stderr, memoryStream);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = WslPath,
            UseShellExecute = false,
            StandardOutputEncoding = linux is null ? Encoding.Unicode : Encoding.UTF8,
            StandardErrorEncoding = linux is null ? Encoding.Unicode : Encoding.UTF8,
            // None of our commands require user input from the real console.
            StandardInputEncoding = Encoding.ASCII,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            CreateNoWindow = true
        };
        if (linux is not null)
        {
            startInfo.ArgumentList.Add("--distribution");
            startInfo.ArgumentList.Add(linux.Value.distribution);
            startInfo.ArgumentList.Add("--user");
            startInfo.ArgumentList.Add("root");
            startInfo.ArgumentList.Add("--cd");
            startInfo.ArgumentList.Add(linux.Value.directory);
            startInfo.ArgumentList.Add("--exec");
        }
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        process = Process.Start(startInfo);
        if (process is null)
        {
            stderr = $"Failed to start \"{WslPath}\" with arguments {string.Join(" ", arguments.Select(arg => $"\"{arg}\""))}.";
            log.Error(stderr);
            return new(-1, stdout, stderr, memoryStream);
        }

        if (millisecondsTimeout > 0)
            cancellationToken = new CancellationTokenSource(millisecondsTimeout).Token;
        (stdout, stderr, memoryStream) = await CaptureProcessOutput(process, outputCallback, binaryOutput, cancellationToken);
        if (process.ExitCode != 0)
        {
            log.Error($"Failed to run '{startInfo.FileName} {string.Join(" ", arguments.Select(arg => $"\"{arg}\""))}', error: {stderr}");
            return new(process.ExitCode, stdout, stderr, memoryStream);
        }

        return new(process.ExitCode, stdout, stderr, memoryStream);
    }

    public async Task<(int ExitCode, string StandardOutput, string StandardError, MemoryStream BinaryOutput)>
        RunWslAsync((string distribution, string directory)? linux, Action<string, bool>? outputCallback, bool binaryOutput, 
        params string[] arguments) => await RunWslAsync(-1, linux, outputCallback, binaryOutput, arguments);

    public async Task<(int ExitCode, string StandardOutput, string StandardError, MemoryStream BinaryOutput)>
        RunWslAsync(Action<string, bool>? outputCallback, bool binaryOutput, params string[] arguments) => 
        await RunWslAsync(-1, null, outputCallback, binaryOutput, arguments);

    public async Task<(int ExitCode, string StandardOutput, string StandardError, MemoryStream BinaryOutput)>
        RunWslAsync(bool binaryOutput, params string[] arguments) => 
        await RunWslAsync(-1, null, null, binaryOutput, arguments);

    public async Task<(int ExitCode, string StandardOutput, string StandardError, MemoryStream BinaryOutput)>
        RunWslAsync(params string[] arguments) => await RunWslAsync(-1, null, null, false, arguments);

    public bool HasExited() => process == null || process.HasExited;

    public void Destroy()
    {
        if (process != null)
        {
            try
            {
                if (process.StartInfo.RedirectStandardInput)
                {
                    using var remoteTimeoutTokenSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
                    // Fire-and-forget Ctrl+C, this *should* terminate any process.
                    process.StandardInput.WriteAsync(CtrlC, remoteTimeoutTokenSource.Token);
                    process.StandardInput.Close();
                    process.WaitForExitAsync(remoteTimeoutTokenSource.Token);
                }
                else
                {
                    log.Debug("Send CTRL+C with CtrlCUtil");
                    CtrlCUtil.SendCtrlC(process);
                }
                process.WaitForExit(200);
            }
            finally
            {
                process.Kill();
                process.Dispose();
                process = null;
            }
        }
    }
}
