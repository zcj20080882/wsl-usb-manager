/******************************************************************************
* SPDX-License-Identifier: MIT
* Project: wsl-usb-manager
* Class: USBIPD.cs
* NameSpace: wsl_usb_manager.Controller
* Author: Chuckie
* copyright: Copyright (c) Chuckie, 2024
* Description:
* Create Date: 2024/10/2 21:59
******************************************************************************/

// Ignore Spelling: USBIPD hardwareid harwareid busid

using System.Reflection;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System;
using System.Text.RegularExpressions;

namespace wsl_usb_manager.Controller;

public struct CommandResult
{
    public int ExitCode;
    public string StandardOutput;
    public string StandardError;
}

public class USBDevicesInfo
{
    public string? InstanceId;
    public string? HardwareId;
    public string? Description;
    public string? IsForced;
    public string? BusId;
    public string? PersistedGuid;
    public string? StubInstanceId;
    public string? ClientIPAddress;
    public string? IsBound;
    public string? IsConnected;
    public string? IsAttached;
}

public class USBIPD
{
    private static readonly string USBIPD_CMD = "usbipd";
    private static readonly string CmdGetAllDevices = @"Import-Module $env:ProgramW6432'\usbipd-win\PowerShell\Usbipd.Powershell.dll';Get-UsbipdDevice";
    private static readonly string[] separator = [""];
    private static readonly char[] separatorOfDictionary = [':'];

    private static (string,string) SpliteCommandString(string command)
    {
        string[] blocks = command.Split(" ");
        string cmd = blocks[0];
        string args = "";
        for (int i = 1; i < blocks.Length; i++)
        {
            args += " " + blocks[i];
        }   
        return (cmd, args);
    }

    private static string CreateWSLCommand(string cmd, string distribution, bool privilege)
    {
        // wsl --distribution Ubuntu-22.04 --user root --exec bash -c 'ls -l /'
        return $"wsl --distribution {distribution} {(privilege ? "--user root " : "")} --exec bash -c '{cmd}'";
    }

    private static string CreateWSLCommand(string cmd, bool privilege)
    {
        return $"wsl {(privilege ? "--user root " : "")} --exec bash -c '{cmd}'";
    }

    public USBIPD()
    {

    }

    public static CommandResult RunPowerShellCommand(string command, bool privilege)
    {
        string error_string = "";
        CommandResult result = new()
        {
            ExitCode = -1,
            StandardOutput = "",
            StandardError = ""
        };
        ProcessStartInfo startInfo = new()
        {
            FileName = "powershell.exe",
            Verb = privilege ? "runas" : "",
            Arguments = $"-Command \"{command}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        if (privilege) {
            //Start-Process <process> -Verb runAs -ArgumentList '<ArgumentList>'
            (string cmd,string args) = SpliteCommandString(command);
            startInfo.Arguments = $"Start-Process {cmd} -Verb RunAs -ArgumentList '{args}' -WindowStyle Hidden\r\n";
        }
        Process process = new()
        {
            StartInfo = startInfo
        };
        try
        {
            process.Start();

            result.StandardOutput = process.StandardOutput.ReadToEnd();
            error_string = process.StandardError.ReadToEnd();
            process.WaitForExit();
            result.ExitCode = process.ExitCode;
            error_string = error_string.Split(new string[] { Environment.NewLine }, StringSplitOptions.None)[0];
            int index = error_string.IndexOf(":");
            if (index != -1)
                error_string = error_string[(index + 1)..];
            
            result.StandardError = error_string.Trim();
        }
        catch (Exception e)
        {
            result.StandardError = e.Message;
        }
        
        return result;
    }

    public static CommandResult BindDevice(string hardwareid)
    {
        return RunPowerShellCommand($"{USBIPD_CMD} bind --hardware-id {hardwareid}", true);
    }

    public static CommandResult BindDevice(string hardwareid, bool force)
    {
        return RunPowerShellCommand($"{USBIPD_CMD} bind --hardware-id {hardwareid} {(force ? "--force" : "")}", true);
    }

    public static CommandResult UnbindDevice(string harwareid)
    {
        return RunPowerShellCommand($"{USBIPD_CMD} unbind --hardware-id {harwareid}", true);
    }

    public static CommandResult UnbindAAllDevice()
    {
        return RunPowerShellCommand($"{USBIPD_CMD} unbind --all", true);
    }

    public static CommandResult AttachDeviceLocal(string harwareid)
    {
        return RunPowerShellCommand($"{USBIPD_CMD} attach --wsl --hardware-id {harwareid}", false);
    }

    public static CommandResult AttachDeviceLocal(string harwareid, string distribution)
    {
        return RunPowerShellCommand($"{USBIPD_CMD} attach --wsl {distribution} --hardware-id {harwareid}", false);
    }

    public static CommandResult AttachDeviceRemote(string busid, string ip)
    {
        return RunPowerShellCommand(CreateWSLCommand($"usbip attach --remote={ip} --busid={busid}",true), false);
    }

    public static CommandResult AttachDeviceRemote(string busid, string distribution, string ip)
    {
        return RunPowerShellCommand(CreateWSLCommand($"usbip attach --remote={ip} --busid={busid}", distribution, true), false);
    }

    public static CommandResult DetachDevice(string harwareid)
    {
        return RunPowerShellCommand($"{USBIPD_CMD} detach --hardware-id {harwareid}", false);
    }

    public static CommandResult DetachAllDevice()
    {
        return RunPowerShellCommand($"{USBIPD_CMD} detach --all", false);
    }

    public static ValueTuple<int, string, List<USBDevicesInfo>> GetAllUSBDevices()
    {
        List<USBDevicesInfo> deviceslist = [];
        CommandResult result = RunPowerShellCommand(CmdGetAllDevices, false);

        if (result.ExitCode != 0 || result.StandardOutput.Length == 0)
        {
            return (result.ExitCode, result.StandardError, deviceslist);
        }

        string[] blocks = result.StandardOutput.Split(separator, StringSplitOptions.RemoveEmptyEntries);

        List<Dictionary<string, string>> devices = [];

        foreach (string block in blocks)
        {
            Dictionary<string, string> device = [];
            string[] lines = block.Split(Environment.NewLine);
            foreach (string line in lines)
            {
                if (line.Length<= 0) 
                {
                    if (device.Count > 0)
                    {
                        devices.Add(device);
                        device = [];
                    }
                    continue;
                }
                string[] parts = line.Split(separatorOfDictionary, 2);
                if (parts.Length == 2)
                {
                    device[parts[0].Trim()] = parts[1].Trim();
                }
            }
            
        }

        foreach (var device in devices)
        {
            USBDevicesInfo deviceInfo = new()
            {
                InstanceId = device.TryGetValue("InstanceId", out string? instance_id) ? instance_id : "",
                HardwareId = device.TryGetValue("HardwareId", out string? hwid) ? hwid : "",
                Description = device.TryGetValue("Description", out string? desc) ? desc : "",

                BusId = device.TryGetValue("BusId", out string? busid) ? busid : "",
                PersistedGuid = device.TryGetValue("PersistedGuid", out string? persist) ? persist : "",
                StubInstanceId = device.TryGetValue("StubInstanceId", out string? stub) ? stub : "",
                ClientIPAddress = device.TryGetValue("ClientIPAddress", out string? ip) ? ip : "",
                IsForced = device.TryGetValue("IsForced", out string? isforced) ? isforced : "False",
                IsBound = device.TryGetValue("IsBound", out string? isbound) ? isbound : "False",
                IsConnected = device.TryGetValue("IsConnected", out string? isconnected) ? isconnected: "False",
                IsAttached = device.TryGetValue("IsAttached", out string? isattached) ? isattached: "False"
            };

            deviceslist.Add(deviceInfo);
        }

        return (result.ExitCode, result.StandardOutput, deviceslist);
    }
}
