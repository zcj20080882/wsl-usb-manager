/******************************************************************************
* SPDX-License-Identifier: MIT
* Project: wsl-usb-manager
* Class: Wsl.cs
* NameSpace: wsl_usb_manager.USBIPD
* Author: Chuckie
* copyright: Copyright (c) Chuckie, 2025
* Description:
* Create Date: 2025/11/14 21:20
******************************************************************************/

using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace wsl_usb_manager.USBIPD;

public static partial class USBIPDWin
{

    private const string ErrNoWslDistributionEN = $"There are no WSL distributions installed. Learn how to install one at {InstallDistributionUrl}.";
    private const string ErrNoWslDistributionZH = $"未检测到已安装的WSL发行版。参考 {InstallDistributionUrl} 了解如何安装。";
    private const string ErrWslVersionEn = $"This program only works with WSL 2 distributions. Learn how to upgrade at {SetWslVersionUrl}.";
    private const string ErrWslVersionZh = $"本程序只支持 WSL2 发行版。参考 {SetWslVersionUrl} 了解如何升级。";
    private const string ErrNoWslDistributionRunningEn = $"There is no WSL 2 distribution running; keep a command prompt to a WSL 2 distribution open to leave it running.";
    private const string ErrNoWslDistributionRunningZh = $"未检测到正在运行的WSL2发行版；请打开一个WSL2发行版的命令提示符，以保持其运行。";
    private const string ErrNoWSLEn = $"The WSL is not installed. Learn how to install one at {InstallDistributionUrl}.";
    private const string ErrNoWSLZh = $"本机未安装的WSL。参考 {InstallDistributionUrl} 了解如何安装。";

    const string InstallDistributionUrl = "https://learn.microsoft.com/windows/wsl/basic-commands#install";
    const string SetWslVersionUrl = "https://learn.microsoft.com/windows/wsl/basic-commands#set-wsl-version-to-1-or-2";

    private static readonly string WslPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "wsl.exe");

    private sealed record Distribution(string Name, bool IsDefault, bool IsRunning, uint Version);


    [GeneratedRegex(@"^( |\*)\s*(\S+)\s+(\S+)\s+(\d+)\r?$")]
    private static partial Regex WslListDistributionRegex();

    private static async Task<(ErrorCode ErrCode, string StandardOutput, string StandardError)>
        RunWslAsync(string? distribution, params string[] arguments)
    {
        if (!File.Exists(WslPath))
        {
            log.Error(IsChinese() ? ErrNoWSLZh : ErrNoWSLEn);
            return (ErrorCode.WslNotInstalled, "", IsChinese() ? ErrNoWSLZh : ErrNoWSLEn);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = WslPath,
            UseShellExecute = false,
            StandardOutputEncoding = distribution is null ? Encoding.Unicode : Encoding.UTF8,
            StandardErrorEncoding = distribution is null ? Encoding.Unicode : Encoding.UTF8,
            StandardInputEncoding = Encoding.ASCII,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            CreateNoWindow = true
        };
        if (distribution is not null)
        {
            startInfo.ArgumentList.Add("--distribution");
            startInfo.ArgumentList.Add(distribution);
            startInfo.ArgumentList.Add("--user");
            startInfo.ArgumentList.Add("root");
            startInfo.ArgumentList.Add("--exec");
        }
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        var process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };

        return await RunProcessWithTimeout(process, 5000);
    }

    private static async Task<(ErrorCode ErrCode, string StandardOutput, string StandardError)>
        RunWslAsync(params string[] arguments) => await RunWslAsync(null, arguments);

    private static async Task<(ErrorCode ErrCode, string StandardOutput, string StandardError)>
        RunWslLinuxCmdAsync(string distribution, params string[] arguments) => await RunWslAsync(distribution, arguments);

    /// <summary>
    /// Returns null if WSL 2 is not even installed.
    /// </summary>
    private static async Task<IEnumerable<Distribution>?> GetWSLDistributions()
    {
        var distributions = new List<Distribution>();

        //
        // Sample output:
        //   NAME               STATE           VERSION
        // * Ubuntu             Running         1
        //   Debian             Stopped         2
        var (ErrCode, StandardOutput, StandardErr) = await RunWslAsync(null,"--list", "--all", "--verbose");

        if (ErrCode != ErrorCode.Success)
        {
            log.Error($"Failed to get WSL distribution, WSL output: {StandardOutput}; Err: {StandardErr}");
            return null;
        }
        var details = StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in details.Skip(1))
        {
            var match = WslListDistributionRegex().Match(line);
            if (!match.Success)
            {
                log.Error($"WSL failed to parse distributions: {StandardOutput}");
                return null;
            }
            var isDefault = match.Groups[1].Value == "*";
            var name = match.Groups[2].Value.TrimEnd();
            var isRunning = match.Groups[3].Value == "Running";
            var version = uint.Parse(match.Groups[4].Value, CultureInfo.InvariantCulture);
            
            distributions.Add(new(name, isDefault, isRunning, version));
        }

        return distributions.AsEnumerable();
    }

    
    private static async Task<(ErrorCode ErrCode, string Distribution, string ErrMsg)> GetRunningDistribution()
    {
        string distribution = string.Empty;
        string errMsg = string.Empty;

        if (await GetWSLDistributions() is not IEnumerable<Distribution> distributions)
        {
            errMsg = IsChinese() ? ErrNoWslDistributionZH : ErrNoWslDistributionEN;
            return new(ErrorCode.WslDistribNotFound, distribution, errMsg); ;
        }

        if (!distributions.Any(d => d.Version == 2))
        {
            errMsg = IsChinese() ? ErrWslVersionZh : ErrWslVersionEn;
            log.Error(errMsg);
            return (ErrorCode.WslLowVersion, distribution, errMsg);
        }

        if (!distributions.Any(d => d.Version == 2 && d.IsRunning))
        {
            errMsg = IsChinese() ? ErrNoWslDistributionRunningZh : ErrNoWslDistributionRunningEn;
            log.Error(errMsg);
            return (ErrorCode.WslNotRunning, distribution, errMsg);
        }

        if (distributions.FirstOrDefault(d => d.IsDefault && d.Version == 2 && d.IsRunning) is Distribution defaultDistribution)
        {
            distribution = defaultDistribution.Name;
        }
        else
        {
            distribution = distributions.First(d => d.Version == 2 && d.IsRunning).Name;
        }
        return (ErrorCode.Success, distribution, string.Empty);
    }
}
