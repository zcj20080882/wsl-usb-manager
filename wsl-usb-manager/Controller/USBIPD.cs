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
using System.Diagnostics;

namespace wsl_usb_manager.Controller;

public class USBIPD
{
    public static readonly string CmdGetAllDevices = @"Import-Module $env:ProgramW6432'\usbipd-win\PowerShell\Usbipd.Powershell.dll';Get-UsbipdDevice";
    private static readonly string[] separator = [""];
    private static readonly char[] separatorOfDictionary = [':'];

    public USBIPD()
    {

    }

    public static string RunPowerShellCommand(string command)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = "powershell.exe",
            Arguments = command,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        Process process = new()
        {
            StartInfo = startInfo
        };
        process.Start();

        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return output;
    }

    public static List<Dictionary<string, string>>? GetAllUSBDevices()
    {
        string deviceslist = RunPowerShellCommand(CmdGetAllDevices);
        string[] blocks = deviceslist.Split(separator, StringSplitOptions.RemoveEmptyEntries);

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

        return devices;
    }
}
