// SPDX-FileCopyrightText: Microsoft Corporation
// SPDX-FileCopyrightText: 2021 Frans van Dorsselaer
// SPDX-FileCopyrightText: 2022 Ye Jun Huang
//
// SPDX-License-Identifier: GPL-3.0-only

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Win32;

namespace wsl_usb_manager.USBIPD;

public static partial class USBIPDWin
{
    enum FirewallCheckResult
    {
        Unknown,
        Pass,
        Fail,
    }

    private const string FwRuleReasonDoNotAllowExceptionsEN = @"A group policy blocks all incoming connections for the public network profile, which includes WSL.";
    private const string FwRuleReasonDoNotAllowExceptionsZH = @"群组策略阻止了公共网络配置文件上的所有传入连接，其中包括WSL。";
    private const string FwRuleReasonAllowLocalPolicyMergeEN = @"A group policy blocks the 'usbipd' firewall rule for the public network profile, which includes WSL.";
    private const string FwRuleReasonAllowLocalPolicyMergeZH = @"群组策略阻止了'usbipd'防火墙规则，该规则包括WSL。";
    private const string FwRuleReasonWinFwNotAllowedEN = @"Windows Firewall is blocking the 'usbipd' firewall rule for the public network profile, which includes WSL.";
    private const string FwRuleReasonWinFwNotAllowedZH = @"Windows防火墙阻止了'usbipd'防火墙规则，该规则包括WSL。";
    private const string ErrWslNotAvalibleEN = $"Windows Subsystem for Linux version 2 is not available. See {InstallWslUrl}.";
    private const string ErrWslNotAvalibleZH = $"Windows Subsystem for Linux version 2 不可用. 参考 {InstallWslUrl} 。";
    private const string ErrNoWslDistributionEN = $"There are no WSL distributions installed. Learn how to install one at {InstallDistributionUrl}.";
    private const string ErrNoWslDistributionZH = $"未检测到已安装的WSL发行版。参考 {InstallDistributionUrl} 了解如何安装。";
    private const string ErrWslVersionEn = $"This program only works with WSL 2 distributions. Learn how to upgrade at {SetWslVersionUrl}.";
    private const string ErrWslVersionZh = $"本程序只支持 WSL2 发行版。参考 {SetWslVersionUrl} 了解如何升级。";
    private const string ErrNoWslDistributionRunningEn = $"There is no WSL 2 distribution running; keep a command prompt to a WSL 2 distribution open to leave it running.";
    private const string ErrNoWslDistributionRunningZh = $"未检测到正在运行的WSL2发行版；请打开一个WSL2发行版的命令提示符，以保持其运行。";
    private const string ErrUSBIPDWslSupportedEN = $"WSL support for usbipd-win is not detected. It may not be installed, or the version of usbipd-win is too low. Please install usbipd-win version 4.0 or higher.";
    private const string ErrUSBIPDWslSupportedZH = $"未检测到usbipd-win的WSL支持，可能没有安装USBIPD-WIN，或者usbipd-win版本过低，请安装版本大于等4.0的usbipd-win。";
    private const string ErrWslGetKernelConfigurationEN = $"Unable to retrieve WSL kernel configuration. WSL may not be running properly. Please restart WSL and try again.";
    private const string ErrWslGetKernelConfigurationZH = $"无法获取WSL内核配置，可能WSL运行不正常，请重启WSL后再试。";
    private const string ErrWslDoNotSupportUSBIPEN = $"WSL kernel is not USBIP capable; update with 'wsl --update'.";
    private const string ErrWslDoNotSupportUSBIPZH = $"WSL 内核不支持 USBIP;使用“wsl --update”进行更新。";

    public const string AttachWslUrl = "https://learn.microsoft.com/windows/wsl/connect-usb#attach-a-usb-device";
    const string InstallWslUrl = "https://learn.microsoft.com/windows/wsl/install";
    const string ListDistributionsUrl = "https://learn.microsoft.com/windows/wsl/basic-commands#install";
    const string InstallDistributionUrl = "https://learn.microsoft.com/windows/wsl/basic-commands#install";
    const string SetWslVersionUrl = "https://learn.microsoft.com/windows/wsl/basic-commands#set-wsl-version-to-1-or-2";


    const string WslMountPoint = "/var/run/usbipd-win";

    [GeneratedRegex(@"^  NAME +STATE +VERSION *$")]
    private static partial Regex WslListHeaderRegex();

    [GeneratedRegex(@"^( |\*) (.+) +([a-zA-Z]+) +([0-9])+ *$")]
    private static partial Regex WslListDistributionRegex();

    [GeneratedRegex(@"\s*(\S+)\s+(\S+)\s+(\S+)\s+(\S+)\s+(\S+)\s+(\S+)\s+(\S+)\s+(\S+)\s+(\S+)\s+(\S+)\s+(\S+)\s*")]
    private static partial Regex RouteRegex();

    [GeneratedRegex(@"\|--\s+(\S+)\s+/32 host LOCAL")]
    private static partial Regex LocalAddressRegex();

    [GeneratedRegex(@"^[a-zA-Z]:\\")]
    private static partial Regex LocalDriveRegex();

    private static List<ProcessRunner> AttachProcessList = [];
    private static readonly object AttachProcessListLock = new();

    public sealed record Distribution(string Name, bool IsDefault, uint Version, bool IsRunning);

    private static string? GetPossibleBlockReason()
    {
        using var policy = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\WindowsFirewall\PublicProfile");
        if (policy is not null)
        {
            if (policy.GetValue("DoNotAllowExceptions") is int doNotAllowExceptions && doNotAllowExceptions != 0)
            {
                return (IsChinese() ? FwRuleReasonDoNotAllowExceptionsZH : FwRuleReasonDoNotAllowExceptionsEN);
            }
            if (policy.GetValue("AllowLocalPolicyMerge") is int allowLocalPolicyMerge && allowLocalPolicyMerge == 0)
            {
                return (IsChinese() ? FwRuleReasonAllowLocalPolicyMergeZH : FwRuleReasonAllowLocalPolicyMergeEN);
            }
        }

        using var settings = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\PublicProfile");
        if (settings is not null)
        {
            if (settings.GetValue("DoNotAllowExceptions") is int doNotAllowExceptions && doNotAllowExceptions != 0)
            {
                return (IsChinese() ? FwRuleReasonWinFwNotAllowedZH : FwRuleReasonWinFwNotAllowedEN);
            }
        }

        return null;
    }

    internal static bool IsOnSameIPv4Network(IPAddress hostAddress, IPAddress hostMask, IPAddress clientAddress)
    {
        if (hostAddress.AddressFamily != AddressFamily.InterNetwork
            || hostMask.AddressFamily != AddressFamily.InterNetwork
            || clientAddress.AddressFamily != AddressFamily.InterNetwork)
        {
            return false;
        }

        // NOTE: we don't care about byte order here
        var rawHost = BitConverter.ToUInt32(hostAddress.GetAddressBytes());
        var rawInstance = BitConverter.ToUInt32(clientAddress.GetAddressBytes());
        var rawMask = BitConverter.ToUInt32(hostMask.GetAddressBytes());
        return (rawHost & rawMask) == (rawInstance & rawMask);
    }

    /// <summary>
    /// Returns null if WSL 2 is not even installed.
    /// </summary>
    public static async Task<IEnumerable<Distribution>?> GetWSLDistributions()
    {
        var distributions = new List<Distribution>();
        ProcessRunner runner = new();
        // Get a list of details of available distributions (in any state: Stopped, Running, Installing, etc.)
        // This contains all we need (default, name, state, version).
        // NOTE: WslGetDistributionConfiguration() is unreliable getting the version.
        //
        // Sample output:
        //   NAME               STATE           VERSION
        // * Ubuntu             Running         1
        //   Debian             Stopped         2
        //   Custom-MyDistro    Running         2
        var detailsResult = await runner.RunWslAsync("--list", "--all", "--verbose");
        switch (detailsResult.ExitCode)
        {
            case 0:
                var details = detailsResult.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);

                // Sanity check
                if (!WslListHeaderRegex().IsMatch(details.FirstOrDefault() ?? string.Empty))
                {
                    log.Error($"WSL failed to parse distributions: {detailsResult.StandardOutput}");
                    runner.Destroy();
                    return null;
                }

                foreach (var line in details.Skip(1))
                {
                    var match = WslListDistributionRegex().Match(line);
                    if (!match.Success)
                    {
                        log.Error($"WSL failed to parse distributions: {detailsResult.StandardOutput}");
                        return null;
                    }
                    var isDefault = match.Groups[1].Value == "*";
                    var name = match.Groups[2].Value.TrimEnd();
                    var isRunning = match.Groups[3].Value == "Running";
                    var version = uint.Parse(match.Groups[4].Value, CultureInfo.InvariantCulture);
                    if (name == "docker-desktop-data")
                    {
                        // NOTE: docker-desktop-data is unusable
                        continue;
                    }
                    distributions.Add(new(name, isDefault, version, isRunning));
                }
                break;

            case 1:
                // This is returned by the default wsl.exe placeholder that is available on newer versions of Windows 10 even if WSL is not installed.
                // At least, that seems to be the case; it turns out that the wsl.exe command line interface isn't stable.

                // Newer versions of wsl.exe support the --status command.
                if ((await runner.RunWslAsync("--status")).ExitCode != 0)
                {
                    // We conclude that WSL is indeed not installed at all.
                    runner.Destroy();
                    return null;
                }

                // We conclude that WSL is installed after all.
                break;

            case -1:
                // This is returned by wsl.exe when WSL is installed, but there are no distributions installed.
                // At least, that seems to be the case; it turns out that the wsl.exe command line interface isn't stable.
                break;

            default:
                // An unknown response. Just assume WSL is installed and report no distributions.
                break;
        }

        return distributions.AsEnumerable();
    }

    /// <summary>
    /// BusId has already been checked, and the server is running.
    /// </summary>
    public static async Task<(ErrorCode ErrCode,string ErrMsg)> 
        Attach(string busID, bool autoAttach, string? hostIP)
    {
        string errMsg = string.Empty;
        string distribution = string.Empty;
        IPAddress? hostAddress = null;
        ProcessRunner runner = new(busID);

        var check = await CheckUSBIPDWin();
        if (check.ErrCode != ErrorCode.Success)
        {
            log.Error($"Failed to fetch USB device list: {check.ErrMsg}");
            runner.Destroy();
            return (check.ErrCode, check.ErrMsg);
        }

        var wslWindowsPath = Path.Combine(Path.GetDirectoryName(GetUSBIPDInstallPath())!, "WSL");
        if (!Path.Exists(wslWindowsPath))
        {
            errMsg = IsChinese() ? ErrUSBIPDWslSupportedZH : ErrUSBIPDWslSupportedEN;
            log.Error(errMsg);
            runner.Destroy();
            return new(ErrorCode.USBIPDLowVersion,errMsg);
        }

        if (!File.Exists(ProcessRunner.WslPath))
        {
            errMsg = IsChinese() ? ErrWslNotAvalibleZH : ErrWslNotAvalibleEN;
            runner.Destroy();
            return new(ErrorCode.WslNotInstalled, errMsg); ;
        }

        // Check: Is the device has been auto-attached.
        lock (AttachProcessListLock)
        {
            foreach (var p in AttachProcessList)
            {
                if (p.Name.ToLower() == busID.ToLower())
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
                }
            }
        }
            
        if (!string.IsNullOrEmpty(hostIP))
        {
            if (!IPAddress.TryParse(hostIP, out hostAddress))
            {
                errMsg = $"{hostIP} is not a valid IP.";
                log.Error(errMsg);
                runner.Destroy();
                return new(ErrorCode.Failure, errMsg);
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
            return new(ErrorCode.WslDistribNotFound,errMsg);;
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

        // We now have determined which running version 2 distribution to use.

        // Check: WSL kernel must be USBIP capable.
        {
            var wslResult = await runner.RunWslAsync((distribution, "/"), null, true, "/bin/cat", "/proc/config.gz");
            if (wslResult.ExitCode != 0)
            {
                errMsg = IsChinese() ? ErrWslGetKernelConfigurationZH : ErrWslGetKernelConfigurationEN;
                log.Error(errMsg);
                runner.Destroy();
                return new(ErrorCode.Failure, errMsg);
            }

            using var gunzipStream = new GZipStream(wslResult.BinaryOutput, CompressionMode.Decompress);
            using var reader = new StreamReader(gunzipStream, Encoding.UTF8);
            var config = await reader.ReadToEndAsync();
            if (config.Contains("CONFIG_USBIP_VHCI_HCD=y"))
            {
                // USBIP client built-in, we're done
            }
            else if (config.Contains("CONFIG_USBIP_VHCI_HCD=m"))
            {
                // USBIP client built as a module

                // Expected output:
                //
                //    ...
                //    vhci_hcd 61440 0 - Live 0x0000000000000000
                //    ...
                wslResult = await runner.RunWslAsync((distribution, "/"), null, false, "/bin/cat", "/proc/modules");
                if (wslResult.ExitCode != 0)
                {
                    errMsg = $"Unable to get WSL kernel modules.";
                    log.Error(errMsg);
                    runner.Destroy();
                    return new(ErrorCode.Failure, errMsg);
                }

                if (!wslResult.StandardOutput.Contains("vhci_hcd"))
                {
                    log.Info($"Loading vhci_hcd module.");
                    wslResult = await runner.RunWslAsync((distribution, "/"), null, false, "/sbin/modprobe", "vhci_hcd");
                    if (wslResult.ExitCode != 0)
                    {
                        errMsg = $"Loading vhci_hcd failed.";
                        log.Error(errMsg);
                        runner.Destroy();
                        return (ErrorCode.Failure, errMsg);
                    }
                    // Expected output:
                    //
                    //    ...
                    //    vhci_hcd 61440 0 - Live 0x0000000000000000
                    //    ...
                    wslResult = await runner.RunWslAsync((distribution, "/"), null, false, "/bin/cat", "/proc/modules");
                    if (wslResult.ExitCode != 0)
                    {
                        errMsg = $"Unable to get WSL kernel modules.";
                        log.Error(errMsg);
                        runner.Destroy();
                        return new(ErrorCode.Failure, errMsg);
                    }
                    if (!wslResult.StandardOutput.Contains("vhci_hcd"))
                    {
                        errMsg = $"Module vhci_hcd not loaded.";
                        log.Error(errMsg);
                        runner.Destroy();
                        return new(ErrorCode.Failure, errMsg);
                    }
                }
            }
            else
            {
                errMsg = IsChinese() ? ErrWslDoNotSupportUSBIPZH : ErrWslDoNotSupportUSBIPEN;
                log.Error(errMsg);
                runner.Destroy();
                return new(ErrorCode.Failure, errMsg);
            }
        }

        // Ensure our wsl directory is mounted.
        // NOTE: This should resolve all issues for users that modified [automount], such as:
        //       disabled automount, mounting at weird locations, mounting non-executable, etc.
        // NOTE: We don't know the shell type (for example, docker-desktop does not even have bash),
        //       so be as portable as possible: single line, use 'test', quote all paths, etc.
        {
            var wslResult = await runner.RunWslAsync((distribution, "/"), null, false, "/bin/sh", "-c", $$"""
                if ! test -d "{{WslMountPoint}}"; then
                    mkdir -m 0000 "{{WslMountPoint}}";
                fi;
                if ! test -f "{{WslMountPoint}}/README.md"; then
                    mount -t drvfs -o "ro,umask=222" "{{wslWindowsPath}}" "{{WslMountPoint}}";
                fi;
                """.ReplaceLineEndings(" "));
            if (wslResult.ExitCode != 0)
            {
                errMsg = $"Mounting '{wslWindowsPath}' within WSL failed.";
                log.Error(errMsg);
                runner.Destroy();
                return new(ErrorCode.Failure, errMsg);
            }
        }

        // Check: our distribution-independent usbip client must be runnable.
        {
            var wslResult = await runner.RunWslAsync((distribution, WslMountPoint), null, false, "./usbip", "version");
            if (wslResult.ExitCode != 0 || wslResult.StandardOutput.Trim() != "usbip (usbip-utils 2.0)")
            {
                errMsg = $"Unable to run 'usbip' client tool. Please report this at https://github.com/dorssel/usbipd-win/issues.";
                runner.Destroy();
                return (ErrorCode.Failure, errMsg);
            }
        }

        // Now find out the IP address of the host (if not explicitly provided by the user).
        if (hostAddress is null)
        {
            var wslResult = await runner.RunWslAsync((distribution, "/"), null, false, "/bin/wslinfo", "--networking-mode");
            string networkingMode;
            if (wslResult.ExitCode == 0)
            {
                networkingMode = wslResult.StandardOutput.Trim();
                log.Info($"Detected networking mode '{networkingMode}'.");
            }
            else
            {
                networkingMode = "nat";
                log.Warn($"Unable to determine networking mode, assuming 'nat'.");
            }
            switch (networkingMode)
            {
                case "none":
                case "virtioproxy":
                default:
                    errMsg = $"Networking mode '{networkingMode}' is not supported.";
                    log.Error(errMsg);
                    return new(ErrorCode.Failure, errMsg);
                case "mirrored":
                    hostAddress = IPAddress.Loopback;
                    break;
                case "nat":
                    // See https://learn.microsoft.com/en-us/windows/wsl/networking
                    // We need to get the default gateway address.
                    // We use 'cat /proc/net/route', where we assume 'cat' is available on all distributions
                    //      and /proc/net/route is supported by the WSL kernel.
                    var ipResult = await runner.RunWslAsync((distribution, "/"), null, false, "/bin/cat", "/proc/net/route");
                    if (ipResult.ExitCode == 0)
                    {
                        // Example output:
                        //
                        // Iface   Destination     Gateway         Flags   RefCnt  Use     Metric  Mask            MTU     Window  IRTT
                        //
                        // eth0    00000000        01E01AAC        0003    0       0       0       00000000        0       0       0
                        //
                        // eth0    00E01AAC        00000000        0001    0       0       0       00F0FFFF        0       0       0

                        for (var match = RouteRegex().Match(ipResult.StandardOutput); match.Success; match = match.NextMatch())
                        {
                            if (uint.TryParse(match.Groups[2].Value, NumberStyles.HexNumber, null, out var destination) && destination == 0
                                && uint.TryParse(match.Groups[3].Value, NumberStyles.HexNumber, null, out var gateway))
                            {
                                hostAddress = new IPAddress(gateway);
                                break;
                            }
                        }
                    }
                    break;
            }
        }
        if (hostAddress is null)
        {
            errMsg = "Unable to determine host address.";
            log.Error(errMsg);
            runner.Destroy();
            return new(ErrorCode.Failure, errMsg);
        }

        log.Info($"Using IP address {hostAddress} to reach the host.");

        // Heuristic firewall check
        {
            // The current timeout is two seconds.
            // This used to be one second, but some users got false results due to WSL being slow to start the command.
            FirewallCheckResult result;
            try
            {
                // With minimal requirements (bash only) try to connect from WSL to our server.
                var pingResult = await runner.RunWslAsync(2000,(distribution, "/"), null, false, "/bin/bash", "-c",
                    $"echo < /dev/tcp/{hostAddress}/{USBIP_PORT}");
                if (pingResult.StandardError.Contains("refused"))
                {
                    // If the output contains "refused", then the test was executed and failed, irrespective of the exit code.
                    result = FirewallCheckResult.Fail;
                }
                else if (pingResult.ExitCode == 0)
                {
                    // The test was executed, and returned within the timeout, and the connection was not actively refused (see above).
                    result = FirewallCheckResult.Pass;
                }
                else
                {
                    // The test was not executed properly (bash unavailable, /dev/tcp not supported, etc.).
                    result = FirewallCheckResult.Unknown;
                }
            }
            catch (OperationCanceledException)
            {
                // Timeout, probably a firewall dropping the connection request (i.e., not actively refused (DENY), but DROP).
                result = FirewallCheckResult.Fail;
            }
            switch (result)
            {
                case FirewallCheckResult.Unknown:
                default:
                    {
                        log.Info($"Firewall check not possible with this distribution (no bash, or wrong version of bash).");
                        // Try to detect any (domain) policy blockers.
                        if (GetPossibleBlockReason() is string blockReason)
                        {
                            // We found a possible blocker.
                            log.Warn(blockReason);
                        }
                    }
                    break;

                case FirewallCheckResult.Fail:
                    {
                        if (GetPossibleBlockReason() is string blockReason)
                        {
                            // We found a possible reason.
                            log.Warn(blockReason);
                        }
                        // In any case, it isn't working...
                        log.Warn($"A firewall appears to be blocking the connection; ensure TCP port {USBIP_PORT} is allowed.");
                    }
                    break;

                case FirewallCheckResult.Pass:
                    // All is well.
                    break;
            }
        }

        // This allows us to augment the errors produced by the Linux usbip client tool.
        // Since we are using our own build of usbip, the "interface" is stable.
        void FilterUsbip(string line, bool isStandardError)
        {
            if (string.IsNullOrEmpty(line))
            {
                // usbip throws in an extraneous final empty line
                return;
            }
            // Prepend "WSL", so the user does not confused over "usbip: ... " vs our own "usbipd: ...".
            // We output as "normal text" (although we could filter on "error:").
            log.Info($"WSL {busID}:{line}");
            if (line.Contains("Device busy"))
            {
                // We have already checked that the device is not attached to some other client.
                log.Warn(
                    "The device appears to be used by Windows; stop the software using the device, or bind the device using the '--force' option.");
            }
        }

        // Finally, call 'usbip attach', or run the auto-attach.sh script.
        if (!autoAttach)
        {
            var wslResult = await runner.RunWslAsync((distribution, WslMountPoint), FilterUsbip, false, "./usbip", "attach",
                $"--remote={hostAddress}", $"--busid={busID}");
            for (int i = 0; i< 3; i++)
            {
                if (wslResult.ExitCode != 0)
                {
                    errMsg = $"Failed to attach device with busid '{busID}'.";
                    wslResult = await runner.RunWslAsync((distribution, WslMountPoint), FilterUsbip, false, "./usbip", "attach",
                        $"--remote={hostAddress}", $"--busid={busID}");
                    log.Error( errMsg );
                    Thread.Sleep(1000);
                }
            }

            runner.Destroy();

            if (wslResult.ExitCode != 0)
            {
                return new(ErrorCode.DeviceAttachFailed, errMsg);
            }
        }
        else
        {
            log.Info($"Auto-attach process started for device {busID}.");
            lock (AttachProcessListLock)
            {
                AttachProcessList.Add(runner);
            }

            var (_, _, ErrMsg,_) = await runner.RunWslAsync(0, (distribution, WslMountPoint), FilterUsbip, false, "./auto-attach.sh", hostAddress.ToString(),
                busID);
            return new(ErrorCode.Failure, IsChinese() ? "运行于WSL中的自动附加程序已退出。" : "The automatic attachment program running in WSL has exited.");
        }

        return new(ErrorCode.Success, "");
    }

    public static void StopAutoAttachProcesses()
    {
        foreach (var p in AttachProcessList)
        {
            if (!p.HasExited())
            {
                log.Info($"Stopping autoattach for {p.Name}");
                p.Destroy();
            }
        }
        AttachProcessList.Clear();
    }
}
