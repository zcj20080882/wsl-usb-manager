/******************************************************************************
* SPDX-License-Identifier: MIT
* Project: wsl-usb-manager
* Class: Wsl.cs
* NameSpace: wsl_usb_manager.Controller
* Author: Chuckie
* copyright: Copyright (c) Chuckie, 2024
* Description:
* Create Date: 2024/10/17 20:28
******************************************************************************/

// Ignore Spelling: Wsl

using log4net;
using Microsoft.Win32;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace wsl_usb_manager.Controller;

static partial class Wsl
{
    public const string AttachWslUrl = "https://learn.microsoft.com/windows/wsl/connect-usb#attach-a-usb-device";
    const string InstallWslUrl = "https://learn.microsoft.com/windows/wsl/install";
    const string InstallDistributionUrl = "https://learn.microsoft.com/windows/wsl/basic-commands#install";
    const string SetWslVersionUrl = "https://learn.microsoft.com/windows/wsl/basic-commands#set-wsl-version-to-1-or-2";

    static readonly string WslPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "wsl.exe");

    const string WslMountPoint = "/var/run/usbipd-win";

    private static readonly ILog log = LogManager.GetLogger(typeof(Wsl));

    public sealed record Distribution(string Name, bool IsDefault, uint Version, bool IsRunning);

    private const ushort USBIP_PORT = 3240;


    static string? GetPossibleBlockReason()
    {
        using var policy = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\WindowsFirewall\PublicProfile");
        if (policy is not null)
        {
            if (policy.GetValue("DoNotAllowExceptions") is int doNotAllowExceptions && doNotAllowExceptions != 0)
            {
                return "A group policy blocks all incoming connections for the public network profile, which includes WSL.";
            }
            if (policy.GetValue("AllowLocalPolicyMerge") is int allowLocalPolicyMerge && allowLocalPolicyMerge == 0)
            {
                return "A group policy blocks the 'usbipd' firewall rule for the public network profile, which includes WSL.";
            }
        }

        using var settings = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\PublicProfile");
        if (settings is not null)
        {
            if (settings.GetValue("DoNotAllowExceptions") is int doNotAllowExceptions && doNotAllowExceptions != 0)
            {
                return "Windows Firewall is configured to block all incoming connections for the public network profile, which includes WSL.";
            }
        }

        return null;
    }

    static async Task<(int ExitCode, string StandardOutput, string StandardError)>
        RunWslAsync((string distribution, string directory)? linux, int timeout_ms, params string[] arguments)
    {
        var stdout = string.Empty;
        var stderr = string.Empty;
        var startInfo = new ProcessStartInfo
        {
            FileName = WslPath,
            UseShellExecute = false,
            StandardOutputEncoding = linux is null ? Encoding.Unicode : Encoding.Default,
            StandardErrorEncoding = linux is null ? Encoding.Unicode : Encoding.Default,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
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

        Process? process = null;
        await Task.Run(() =>
        {
            process = Process.Start(startInfo);
            if (process != null)
            {
                try
                {
                    process.WaitForExit(timeout_ms);
                }
                finally
                {
                    // Kill the entire Windows process tree, just in case it hasn't exited already.
                    process.Kill(true);
                    stdout = process.StandardOutput.ReadToEnd();
                    stderr = process.StandardError.ReadToEnd();
                }
            }
        });

        if (process == null)
        {
            return new((int)ExitCode.Failure, "", $"Failed to start \"{WslPath}\" with arguments {string.Join(" ", arguments.Select(arg => $"\"{arg}\""))}.");
        }

        return new(process.ExitCode, stdout, stderr);
    }

    enum FirewallCheckResult
    {
        Unknown,
        Pass,
        Fail,
    }

    /// <summary>
    /// BusId has already been checked, and the server is running.
    /// </summary>
    public static async Task<(ExitCode, string error_msg)>
        Attach(string busId, string? hostIP)
    {
        var distribution = "";
        var err_msg = "";
        var wslWindowsPath = Path.Combine(USBIPD.GetUSBIPDInstallPath(), "WSL");
        if (!Path.Exists(wslWindowsPath))
        {
            err_msg = $"usbipd-win may be not installed; reinstall usbipd-win(version >= 4.3.0) then restart this program.";
            log.Error(err_msg);
            return (ExitCode.Failure, err_msg);
        }
        if ((Path.GetPathRoot(wslWindowsPath) is not string wslWindowsPathRoot) || (!LocalDriveRegex().IsMatch(wslWindowsPathRoot)))
        {
            err_msg = $"This software should be installed on a local drive.";
            log.Error(err_msg);
            return (ExitCode.Failure, err_msg);
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
            err_msg = $"Windows Subsystem for Linux version 2 is not available. See {InstallWslUrl}.";
            log.Error(err_msg);
            return (ExitCode.Failure, err_msg);
        }

        // check (c1)
        if (!distributions.Any())
        {
            err_msg = $"There are no WSL distributions installed. Learn how to install one at {InstallDistributionUrl}.";
            log.Error(err_msg);
            return (ExitCode.Failure, err_msg);
        }

        // check (c2)
        if (!distributions.Any(d => d.Version == 2))
        {
            err_msg = $"This program only works with WSL 2 distributions. Learn how to upgrade at {SetWslVersionUrl}.";
            log.Error(err_msg);
            return (ExitCode.Failure, err_msg);
        }

        // check (c3)
        if (!distributions.Any(d => d.Version == 2 && d.IsRunning))
        {
            err_msg = $"There is no WSL 2 distribution running; keep a command prompt to a WSL 2 distribution open to leave it running.";
            log.Error(err_msg);
            return (ExitCode.Failure, err_msg);
        }

        if (distributions.FirstOrDefault(d => d.IsDefault && d.Version == 2 && d.IsRunning) is Distribution defaultDistribution)
        {
            // case (c4i)
            distribution = defaultDistribution.Name;
        }
        else
        {
            // case (c4ii)
            distribution = distributions.First(d => d.Version == 2 && d.IsRunning).Name;
        }


        log.Info($"Using WSL distribution '{distribution}' to attach; the device will be available in all WSL 2 distributions.");

        // We now have determined which running version 2 distribution to use.

        // Check: WSL kernel must be USBIP capable.
        {
            var wslResult = await RunWslAsync((distribution, "/"), 1000, "zgrep", "CONFIG_USBIP_VHCI_HCD", "/proc/config.gz");
            if (wslResult.ExitCode != 0)
            {
                log.Error($"Unable to get WSL kernel configuration.");
                return (ExitCode.Failure, $"Unable to get WSL kernel configuration: {wslResult.StandardError}");
            }

            var config = wslResult.StandardOutput;
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
                wslResult = await RunWslAsync((distribution, "/"), 1000, "cat", "/proc/modules");
                if (wslResult.ExitCode != 0)
                {
                    log.Error($"Unable to get WSL kernel modules: {wslResult.StandardError}");
                    return (ExitCode.Failure, $"Unable to get WSL kernel modules: {wslResult.StandardError}");
                }

                if (!wslResult.StandardOutput.Contains("vhci_hcd"))
                {
                    log.Info($"Loading vhci_hcd module.");
                    wslResult = await RunWslAsync((distribution, "/"), 1000, "modprobe", "vhci_hcd");
                    if (wslResult.ExitCode != 0)
                    {
                        log.Error($"Loading vhci_hcd failed: {wslResult.StandardError}");
                        return (ExitCode.Failure, $"Loading vhci_hcd failed.");
                    }
                    // Expected output:
                    //
                    //    ...
                    //    vhci_hcd 61440 0 - Live 0x0000000000000000
                    //    ...
                    wslResult = await RunWslAsync((distribution, "/"), 1000, "cat", "/proc/modules");
                    if (wslResult.ExitCode != 0)
                    {
                        log.Error($"Unable to get WSL kernel modules: {wslResult.StandardError}");
                        return (ExitCode.Failure, $"Unable to get WSL kernel modules.");
                    }
                    if (!wslResult.StandardOutput.Contains("vhci_hcd"))
                    {
                        log.Error($"Module vhci_hcd not loaded.");
                        return (ExitCode.Failure, $"Module vhci_hcd not loaded.");
                    }
                }
            }
            else
            {
                log.Error($"WSL kernel is not USBIP capable; update with 'wsl --update'.");
                return (ExitCode.Failure, $"WSL kernel is not USBIP capable; update with 'wsl --update'.");
            }
        }

        // Ensure our wsl directory is mounted.
        // NOTE: This should resolve all issues for users that modified [automount], such as:
        //       disabled automount, mounting at weird locations, mounting non-executable, etc.
        // NOTE: We don't know the shell type (for example, docker-desktop does not even have bash),
        //       so be as portable as possible: single line, use 'test', quote all paths, etc.
        {
            var wslResult = await RunWslAsync((distribution, "/"), 1000, "/bin/sh", "-c", $$"""
                if ! test -d "{{WslMountPoint}}"; then
                    mkdir -m 0000 "{{WslMountPoint}}";
                fi;
                if ! test -f "{{WslMountPoint}}/README.md"; then
                    mount -t drvfs -o "ro,umask=222" "{{wslWindowsPath}}" "{{WslMountPoint}}";
                fi;
                """.ReplaceLineEndings(" "));
            if (wslResult.ExitCode != 0)
            {
                log.Error($"Mounting '{wslWindowsPath}' within WSL failed: {wslResult.StandardError}");
                return (ExitCode.Failure, $"Mounting '{wslWindowsPath}' within WSL failed.");
            }
        }

        // Check: our distribution-independent usbip client must be runnable.
        {
            var wslResult = await RunWslAsync((distribution, WslMountPoint), 1000, "./usbip", "version");
            if (wslResult.ExitCode != 0 || wslResult.StandardOutput.Trim() != "usbip (usbip-utils 2.0)")
            {
                log.Error($"Unable to run 'usbip' client tool. Please report this at https://github.com/dorssel/usbipd-win/issues.");
                return (ExitCode.Failure, $"Unable to run 'usbip' client tool. Please report this at https://github.com/dorssel/usbipd-win/issues.");
            }
        }

        // Now find out the IP address of the host.
        IPAddress hostAddress;
        if (hostIP is null || hostIP.Length == 0)
        {
            var wslResult = await RunWslAsync((distribution, "/"), 1000, "/bin/wslinfo", "--networking-mode");
            if (wslResult.ExitCode == 0 && wslResult.StandardOutput.Trim() == "mirrored")
            {
                // mirrored networking mode ... we're done
                hostAddress = IPAddress.Loopback;
            }
            else
            {
                // Get all non-loopback unicast IPv4 addresses for WSL.
                var clientAddresses = new List<IPAddress>();
                {
                    // We use 'cat /proc/net/fib_trie', where we assume 'cat' is available on all distributions and /proc/net/fib_trie is supported by the WSL kernel.
                    var ipResult = await RunWslAsync((distribution, "/"), 500, "cat", "/proc/net/fib_trie");
                    if (ipResult.ExitCode == 0)
                    {
                        // Example output:
                        //
                        // Main:
                        //   +-- 0.0.0.0/0 3 0 5
                        //      |-- 0.0.0.0
                        //         /0 universe UNICAST
                        //      +-- 127.0.0.0/8 2 0 2
                        //         +-- 127.0.0.0/31 1 0 0
                        //            |-- 127.0.0.0
                        //               /32 link BROADCAST
                        //               /8 host LOCAL
                        //            |-- 127.0.0.1
                        //               /32 host LOCAL
                        //         |-- 127.255.255.255
                        //            /32 link BROADCAST
                        // ...
                        //
                        // We are interested in all entries like:
                        //
                        //            |-- 127.0.0.1
                        //               /32 host LOCAL
                        //
                        // These are the interface addresses.

                        for (var match = LocalAddressRegex().Match(ipResult.StandardOutput); match.Success; match = match.NextMatch())
                        {
                            if (!IPAddress.TryParse(match.Groups[1].Value, out var clientAddress))
                            {
                                continue;
                            }
                            if (clientAddress.AddressFamily != AddressFamily.InterNetwork)
                            {
                                // For simplicity, we only use IPv4.
                                continue;
                            }
                            if (IsOnSameIPv4Network(IPAddress.Loopback, IPAddress.Parse("255.0.0.0"), clientAddress))
                            {
                                // We are *not* in mirrored network mode, so ignore loopback addresses.
                                continue;
                            }
                            // Only add unique entries. List is not going to be long, so a linear search is fine.
                            if (!clientAddresses.Contains(clientAddress))
                            {
                                clientAddresses.Add(clientAddress);
                            }
                        }
                    }
                }
                if (clientAddresses.Count == 0)
                {
                    err_msg = $"WSL does not appear to have network connectivity; try `wsl --shutdown` and then restart WSL.";
                    log.Error(err_msg);
                    return (ExitCode.Failure, err_msg);
                }

                // Get all non-loopback unicast IPv4 addresses (with their mask) for the host.
                var hostAddresses = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(ni => ni.OperationalStatus == OperationalStatus.Up)
                    .Select(ni => ni.GetIPProperties().UnicastAddresses)
                    .SelectMany(uac => uac)
                    .Where(ua => ua.Address.AddressFamily == AddressFamily.InterNetwork)
                    .Where(ua => !IsOnSameIPv4Network(IPAddress.Loopback, IPAddress.Parse("255.0.0.0"), ua.Address));

                // Find any match; we'll just take the first.
                if (hostAddresses.FirstOrDefault(ha => clientAddresses.Any(ca => IsOnSameIPv4Network(ha.Address, ha.IPv4Mask, ca))) is not UnicastIPAddressInformation matchHost)
                {
                    err_msg = "The host IP address for the WSL virtual switch could not be found.";
                    log.Error(err_msg);
                    return (ExitCode.Failure, err_msg);
                }

                hostAddress = matchHost.Address;
            }
        }
        else
        {
            try
            {
                hostAddress = IPAddress.Parse(hostIP);
            }
            catch
            {
                err_msg = $"{hostIP} is an invalid IP address.";
                log.Error(err_msg);
                return (ExitCode.Failure, err_msg);
            }
        }

        log.Info($"Using IP address {hostAddress} to reach the host.");

        // Heuristic firewall check
        {
            // The current timeout is two seconds.
            // This used to be one second, but some users got false results due to WSL being slow to start the command.
            FirewallCheckResult result;
            // With minimal requirements (bash only) try to connect from WSL to our server.
            var pingResult = await RunWslAsync((distribution, "/"), 1000, "bash", "-c", $"echo < /dev/tcp/{hostAddress}/{USBIP_PORT}");
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
                            log.Error(blockReason);
                            return (ExitCode.Failure, blockReason);
                        }
                    }
                    break;

                case FirewallCheckResult.Fail:
                    {
                        if (GetPossibleBlockReason() is string blockReason)
                        {
                            // We found a possible reason.
                            log.Error(blockReason);
                        }
                        // In any case, it isn't working...
                        err_msg = $"A firewall appears to be blocking the connection; ensure TCP port {USBIP_PORT} is allowed.";
                        log.Error(err_msg);
                        return (ExitCode.Failure, err_msg);
                    }
                case FirewallCheckResult.Pass:
                    // All is well.
                    break;
            }
        }

        // Finally, call 'usbip attach', or run the auto-attach.sh script.
        {
            var wslResult = await RunWslAsync((distribution, WslMountPoint), 5000, "./usbip", "attach", $"--remote={hostAddress}", $"--busid={busId}");
            if (wslResult.ExitCode != 0)
            {
                err_msg = "";
                if (wslResult.StandardError.Contains("Device busy"))
                {
                    err_msg = "The device appears to be used by Windows; stop the software using the device, or bind the device with force enabled.";
                    err_msg += "\r\n";

                }
                err_msg += $"Failed to attach device with busid '{busId}'.";
                log.Error(err_msg);
                return (ExitCode.Failure, err_msg);
            }
        }

        return (ExitCode.Success, "");
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

    [GeneratedRegex(@"^\s+NAME\s+STATE\s+VERSION\s+$")]
    private static partial Regex WslListHeaderRegex();

    [GeneratedRegex(@"^( |\*) (.+) +([a-zA-Z]+) +([0-9])\s*$")]
    private static partial Regex WslListDistributionRegex();

    [GeneratedRegex(@"\|--\s+(\S+)\s+/32 host LOCAL")]
    private static partial Regex LocalAddressRegex();

    [GeneratedRegex(@"^[a-zA-Z]:\\")]
    private static partial Regex LocalDriveRegex();

    /// <summary>
    /// Returns null if WSL 2 is not even installed.
    /// </summary>
    public static async Task<IEnumerable<Distribution>?> GetWSLDistributions()
    {
        if (!File.Exists(WslPath))
        {
            // Since WSL 2, the wsl.exe command is used to manage WSL.
            // And since USBIP requires a real kernel (i.e. WSL 2), we may safely assume that wsl.exe is available.
            // Users with older (< 1903) Windows will simply get a report that WSL 2 is not available,
            //    even if they have WSL (version 1) installed. It won't work for them anyway.
            // We won't bother checking for the older wslconfig.exe that was used to manage WSL 1.
            return null;
        }

        var distributions = new List<Distribution>();

        // Get a list of details of available distributions (in any state: Stopped, Running, Installing, etc.)
        // This contains all we need (default, name, state, version).
        // NOTE: WslGetDistributionConfiguration() is unreliable getting the version.
        //
        // Sample output:
        //   NAME               STATE           VERSION
        // * Ubuntu             Running         1
        //   Debian             Stopped         2
        //   Custom-MyDistro    Running         2
        var detailsResult = await RunWslAsync(null, 500, "--list", "--all", "--verbose");
        switch (detailsResult.ExitCode)
        {
            case 0:
                var details = detailsResult.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);

                // Sanity check
                if (!WslListHeaderRegex().IsMatch(details[0]))
                {
                    log.Error($"WSL failed to parse distributions: {detailsResult.StandardOutput}");
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
                if ((await RunWslAsync(null, 500, "--status")).ExitCode != 0)
                {
                    // We conclude that WSL is indeed not installed at all.
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
}
