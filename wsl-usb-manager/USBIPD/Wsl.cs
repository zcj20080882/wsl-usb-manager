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
