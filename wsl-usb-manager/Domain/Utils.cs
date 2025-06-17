/******************************************************************************
* SPDX-License-Identifier: MIT
* Project: wsl-usb-manager
* Class: BodyItem.cs
* NameSpace: wsl_usb_manager.Domain
* Author: Chuckie
* copyright: Copyright (c) Chuckie, 2025
* Description:
* Create Date: 2025/06/17 20:25
******************************************************************************/

using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
namespace wsl_usb_manager.Domain;

public class ApplicationInfo
{
    public required string DisplayName { get; set; }
    public required string InstallLocation { get; set; }
    public required string DisplayVersion { get; set; }
    public required string Publisher { get; set; }
    public required string InstallDate { get; set; }
    public required string UninstallString { get; set; }
    public bool Is64Bit { get; set; }
}

public static class ApplicationChecker
{
    public static ApplicationInfo? GetApplicationInfo(string applicationName)
    {
        // 检查64位应用的注册表路径
        ApplicationInfo ? appInfo = SearchRegistry(
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            applicationName,
            false);

        if (appInfo != null)
            return appInfo;

        // 检查32位应用的注册表路径（在64位系统上）
        appInfo = SearchRegistry(
            @"SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
            applicationName,
            true);

        if (appInfo != null)
            return appInfo;

        // 检查当前用户安装的应用
        appInfo = SearchRegistry(
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            applicationName,
            false,
            Registry.CurrentUser);

        return appInfo;
    }

    public static List<ApplicationInfo> GetAllInstalledApplications()
    {
        List<ApplicationInfo> applications = new List<ApplicationInfo>();

        // 收集HKLM中的64位应用
        CollectApplications(
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            applications,
            false);

        // 收集HKLM中的32位应用
        CollectApplications(
            @"SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
            applications,
            true);

        // 收集HKCU中的应用
        CollectApplications(
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            applications,
            false,
            Registry.CurrentUser);

        return applications;
    }

    private static ApplicationInfo? SearchRegistry(string registryKey, string applicationName, bool is32Bit, RegistryKey? baseKey = null)
    {
        baseKey ??= Registry.LocalMachine;

        using RegistryKey? key = baseKey.OpenSubKey(registryKey);
        if (key != null)
        {
            foreach (string subkeyName in key.GetSubKeyNames())
            {
                using RegistryKey? subkey = key.OpenSubKey(subkeyName);
                if (subkey != null)
                {
                    string? displayName = subkey.GetValue("DisplayName") as string;

                    if (!string.IsNullOrEmpty(displayName) &&
                        displayName.IndexOf(applicationName, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return GetInfoFromRegistryKey(subkey, is32Bit);
                    }
                }
            }
        }

        return null;
    }

    private static void CollectApplications(string registryKey, List<ApplicationInfo> applications, bool is32Bit, RegistryKey? baseKey = null)
    {
        baseKey ??= Registry.LocalMachine;

        using RegistryKey? key = baseKey.OpenSubKey(registryKey);
        if (key != null)
        {
            foreach (string subkeyName in key.GetSubKeyNames())
            {
                using RegistryKey? subkey = key.OpenSubKey(subkeyName);
                if (subkey != null)
                {
                    string? displayName = subkey.GetValue("DisplayName") as string;

                    if (!string.IsNullOrEmpty(displayName))
                    {
                        ApplicationInfo? info = GetInfoFromRegistryKey(subkey, is32Bit);
                        if (info != null)
                        {
                            applications.Add(info);
                        }
                    }
                }
            }
        }
    }

    private static ApplicationInfo GetInfoFromRegistryKey(RegistryKey key, bool is32Bit)
    {
        return new ApplicationInfo
        {
            DisplayName = key.GetValue("DisplayName") as string ?? string.Empty,
            InstallLocation = key.GetValue("InstallLocation") as string ?? string.Empty,
            DisplayVersion = key.GetValue("DisplayVersion") as string ?? string.Empty,
            Publisher = key.GetValue("Publisher") as string ?? string.Empty,
            InstallDate = key.GetValue("InstallDate") as string ?? string.Empty,
            UninstallString = key.GetValue("UninstallString") as string ?? string.Empty,
            Is64Bit = !is32Bit
        };
    }
}

public static class CtrlCUtil
{
    [DllImport("kernel32.dll")]
    static extern bool GenerateConsoleCtrlEvent(uint dwCtrlEvent, uint dwProcessGroupId);

    [DllImport("kernel32.dll")]
    static extern bool AttachConsole(uint dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool FreeConsole();

    private const uint CTRL_C_EVENT = 0;

    public static void SendCtrlC(Process process)
    {
        AttachConsole((uint)process.Id);
        GenerateConsoleCtrlEvent(CTRL_C_EVENT, (uint)process.Id);
        FreeConsole();
    }
}