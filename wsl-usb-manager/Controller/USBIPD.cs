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

using wsl_usb_manager.Settings;

namespace wsl_usb_manager.Controller;

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

    public override bool Equals(object? obj)
    {
        if (obj is USBDevicesInfo other)
        {
            return (
                    this.InstanceId == other.InstanceId &&
                    this.HardwareId == other.HardwareId &&
                    this.Description == other.Description &&
                    this.IsForced == other.IsForced &&
                    this.BusId == other.BusId &&
                    this.PersistedGuid == other.PersistedGuid &&
                    this.StubInstanceId == other.StubInstanceId &&
                    this.ClientIPAddress == other.ClientIPAddress &&
                    this.IsBound == other.IsBound &&
                    this.IsConnected == other.IsConnected &&
                    this.IsAttached == other.IsAttached
                );
        }
        return false;
    }

    public override int GetHashCode()
    {
        HashCode hash = new();
        hash.Add(this.InstanceId);
        hash.Add(this.HardwareId);
        hash.Add(this.Description);
        hash.Add(this.IsForced);
        hash.Add(this.BusId);
        hash.Add(this.PersistedGuid);
        hash.Add(this.StubInstanceId);
        hash.Add(this.ClientIPAddress);
        hash.Add(this.IsBound);
        hash.Add(this.IsConnected);
        hash.Add(this.IsAttached);
        return hash.ToHashCode();
    }
}

public class USBIPD
{
    private static readonly string USBIPD_CMD = "usbipd";
    private static readonly string CmdGetAllDevices = @"Import-Module $env:ProgramW6432'\usbipd-win\PowerShell\Usbipd.Powershell.dll';Get-UsbipdDevice";
    private static readonly string[] separator = [""];
    private static readonly char[] separatorOfDictionary = [':'];

    public USBIPD()
    {

    }

    public static CommandResult BindDevice(string hardwareid)
    {
        return new PowerShellRunner($"{USBIPD_CMD} bind --hardware-id {hardwareid}").Run(true);
    }

    public static CommandResult BindDevice(string hardwareid, bool force)
    {
        return new PowerShellRunner($"{USBIPD_CMD} bind --hardware-id {hardwareid} {(force ? "--force" : "")}").Run(true);
    }

    public static CommandResult UnbindDevice(string harwareid)
    {
        return new PowerShellRunner($"{USBIPD_CMD} unbind --hardware-id {harwareid}").Run(true);
    }

    public static CommandResult UnbindAAllDevice()
    {
        return new PowerShellRunner($"{USBIPD_CMD} unbind --all").Run(true);
    }

    public static CommandResult AttachDevice(string harwareid)
    {
        return new PowerShellRunner($"{USBIPD_CMD} attach --wsl --hardware-id {harwareid}").Run(false);
    }

    public static CommandResult AttachDevice(string harwareid, string distribution)
    {
        return new PowerShellRunner($"{USBIPD_CMD} attach --wsl {distribution} --hardware-id {harwareid}").Run(false);
    }

    public static CommandResult DetachDevice(string harwareid)
    {
        return new PowerShellRunner($"{USBIPD_CMD} detach --hardware-id {harwareid}").Run(false);
    }

    public static CommandResult DetachAllDevice()
    {
        return new PowerShellRunner($"{USBIPD_CMD} detach --all").Run(false);
    }

    public static ValueTuple<int, string, List<USBDevicesInfo>> GetAllUSBDevices()
    {
        List<USBDevicesInfo> deviceslist = [];
        CommandResult result = new PowerShellRunner(CmdGetAllDevices).Run(false);

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
