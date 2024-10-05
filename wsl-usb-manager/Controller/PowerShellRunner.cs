/******************************************************************************
* SPDX-License-Identifier: MIT
* Project: wsl-usb-manager
* Class: PowerShellRunner.cs
* NameSpace: wsl_usb_manager.Controller
* Author: Chuckie
* copyright: Copyright (c) Chuckie, 2024
* Description:
* Create Date: 2024/10/5 17:17
******************************************************************************/
using log4net;
using System.Diagnostics;
using System.Text;

namespace wsl_usb_manager.Controller;

public struct CommandResult
{
    public int ExitCode;
    public string StandardOutput;
    public string StandardError;
}

public class PowerShellRunner(string commands)
{
    private readonly string Commands = commands;
    private static readonly ILog log = LogManager.GetLogger(typeof(PowerShellRunner));
    private (string, string) SpliteCommandString(string command)
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

    public CommandResult Run(bool privilege)
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
            Arguments = $"-Command \"{Commands}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        if (privilege)
        {
            //Start-Process <process> -Verb runAs -ArgumentList '<ArgumentList>'
            (string cmd, string args) = SpliteCommandString(Commands);
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

    public static (byte[], int) RemoveZeroBytes(byte[] bytes, int len)
    {
        int new_len = 0;
        int min_len = bytes.Length > len ? len : bytes.Length;
        if (min_len < 1)
        {
            return ([], new_len);
        }
        byte[] new_bytes = new byte[min_len];

        for (int i = 0; i < min_len - 1; i++)
        {
            if (bytes[i] == 0)
                continue;
            new_bytes[new_len++] = bytes[i];
        }
        new_bytes[new_len] = 0;
        return (new_bytes, new_len);
    }

    public CommandResult RunAndDecode(bool privilege)
    {
        string error_string = "";
        byte[] buffer = new byte[512 * 1024];
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
            Arguments = $"-Command \"{Commands}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        if (privilege)
        {
            //Start-Process <process> -Verb runAs -ArgumentList '<ArgumentList>'
            (string cmd, string args) = SpliteCommandString(Commands);
            startInfo.Arguments = $"Start-Process {cmd} -Verb RunAs -ArgumentList '{args}' -WindowStyle Hidden\r\n";
        }
        Process process = new()
        {
            StartInfo = startInfo
        };
        try
        {
            process.Start();
            process.WaitForExit();
            log.Debug($"Try to decode with {Encoding.Default.EncodingName}");
            int bytes_len = process.StandardOutput.BaseStream.Read(buffer, 0, buffer.Length);
            (byte[] new_bytes, bytes_len) = RemoveZeroBytes(buffer, bytes_len);
            if (bytes_len > 0)
            {
                result.StandardOutput = Encoding.Default.GetString(new_bytes, 0, bytes_len);
            }

            bytes_len = process.StandardError.BaseStream.Read(buffer, 0, buffer.Length);
            (new_bytes, bytes_len) = RemoveZeroBytes(buffer, bytes_len);
            if (bytes_len > 0)
            {
                error_string = Encoding.Default.GetString(new_bytes, 0, bytes_len);
            }
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
}
