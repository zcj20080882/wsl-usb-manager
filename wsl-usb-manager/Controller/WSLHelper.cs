/******************************************************************************
* SPDX-License-Identifier: MIT
* Project: wsl-usb-manager
* Class: WSLHelper.cs
* NameSpace: wsl_usb_manager.Controller
* Author: Chuckie
* copyright: Copyright (c) Chuckie, 2024
* Description:
* Create Date: 2024/10/5 17:16
******************************************************************************/

// Ignore Spelling: busid harwareid

using log4net;
using System.Text.RegularExpressions;

namespace wsl_usb_manager.Controller;

public class WSLHelper
{
    private static readonly ILog log = LogManager.GetLogger(typeof(PowerShellRunner));
    private static string CreateWSLCommand(string cmd, string distribution, bool privilege)
    {
        // wsl --distribution Ubuntu-22.04 --user root --exec bash -c 'ls -l /'
        return $"wsl --distribution {distribution} {(privilege ? "--user root " : "")} --exec bash -c '{cmd}'";
    }

    private static string CreateWSLCommand(string cmd, bool privilege)
    {
        return $"wsl {(privilege ? "--user root " : "")} --exec bash -c '{cmd}'";
    }

    public static CommandResult AttachDevice(string busid, string ip)
    {
        return new PowerShellRunner(CreateWSLCommand($"usbip attach --remote={ip} --busid={busid}", true)).Run(false);
    }

    public static CommandResult AttachDevice(string busid, string distribution, string ip)
    {
        return new PowerShellRunner(CreateWSLCommand($"usbip attach --remote={ip} --busid={busid}", distribution, true)).Run(false);
    }

    public static CommandResult DetachDevice(string harwareid)
    {
        return USBIPD.DetachDevice(harwareid);
    }

    public static List<string> GetAllWSLDistribution()
    {
        List<string> wslDistributions = [];
        CommandResult result = new PowerShellRunner("wsl -l -v").RunAndDecode(false);
        if (result.ExitCode != 0 || result.StandardOutput.Length == 0)
            return [];

        string[] lines = result.StandardOutput.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

        for(int i=1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (line.StartsWith('*'))
            {
                line = line[1..];
            }
            wslDistributions.Add(Regex.Split(line.Trim(), @"\s|\t")[0].Trim());
        }
        return wslDistributions;
    }
}
