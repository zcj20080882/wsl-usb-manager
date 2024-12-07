/******************************************************************************
* SPDX-License-Identifier: MIT
* Project: wsl-usb-manager
* Class: USBDevice.cs
* NameSpace: wsl_usb_manager.Controller
* Author: Chuckie
* copyright: Copyright (c) Chuckie, 2024
* Description:
* Create Date: 2024/10/17 20:28
******************************************************************************/
using log4net;
using System.Runtime.Serialization;
using wsl_usb_manager.Domain;

namespace wsl_usb_manager.Controller;

[DataContract]
public sealed class USBDevice
{
    private static readonly ILog log = LogManager.GetLogger(typeof(USBDeviceInfoModel));
    public USBDevice() { }

    [DataMember]
    public string InstanceId { get; set; } = string.Empty;
    [DataMember]
    public string HardwareId { get; set; } = string.Empty;
    [DataMember]
    public string Description { get; set; } = string.Empty;
    [DataMember]
    public bool IsForced { get; set; } = false;
    [DataMember]
    public string BusId { get; set; } = string.Empty;
    [DataMember]
    public string PersistedGuid { get; set; } = string.Empty;
    [DataMember]
    public string StubInstanceId { get; set; } = string.Empty;
    [DataMember]
    public string ClientIPAddress { get; set; } = string.Empty;
    [DataMember]
    public bool IsBound { get; set; } = false;
    [DataMember]
    public bool IsConnected { get; set; } = false;
    [DataMember]
    public bool IsAttached { get; set; } = false;

    private void Updatethis(USBDevice? dev)
    {
        if (dev == null)
        {
            return;
        }
        this.InstanceId = dev.InstanceId;
        this.HardwareId = dev.HardwareId;
        this.Description = dev.Description;
        this.IsForced = dev.IsForced;
        this.BusId = dev.BusId;
        this.PersistedGuid = dev.PersistedGuid;
        this.StubInstanceId = dev.StubInstanceId;
        this.ClientIPAddress = dev.ClientIPAddress;
        this.IsBound = dev.IsBound;
        this.IsConnected = dev.IsConnected;
        this.IsAttached = dev.IsAttached;
    }

    public USBDevice Clone()
    {
        return new USBDevice
        {
            InstanceId = this.InstanceId,
            HardwareId = this.HardwareId,
            Description = this.Description,
            IsForced = this.IsForced,
            BusId = this.BusId,
            PersistedGuid = this.PersistedGuid,
            StubInstanceId = this.StubInstanceId,
            ClientIPAddress = this.ClientIPAddress,
            IsBound = this.IsBound,
            IsConnected = this.IsConnected,
            IsAttached = this.IsAttached
        };
    }
    public override bool Equals(object? obj)
    {
        if (obj is USBDevice other)
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

    public async Task<(ExitCode exitCode, string errMsg)> Bind()
    {
        if (string.IsNullOrEmpty(HardwareId))
            return (ExitCode.Failure, $"HardwareId is empty.");

        if (!IsConnected)
        {
            return (ExitCode.Failure, $"Device is not connected.");
        }

        if(IsBound)
        {
            return (ExitCode.Success, $"Device is already bound.");
        }
        
        var (exitCode, errMsg, newDev) = await USBIPD.BindDevice(HardwareId, IsForced);
        Updatethis(newDev);
        if (IsBound)
        {
            log.Info($"Success to bind {Description}({HardwareId}){(IsForced ? " forcibly" : "")}.");
            exitCode = 0;
        }
        else
        {
            log.Error($"Failed to bind {Description}({HardwareId}){(IsForced ? " forcibly" : "")}: {errMsg}");
        }
        return (exitCode, errMsg);
    }

    public async Task<(ExitCode exitCode, string errMsg)> Unbind()
    {
        if (string.IsNullOrEmpty(HardwareId))
            return (ExitCode.Failure, "HardwareId is empty.");

        if (!IsBound)
        {
            return (ExitCode.Success, $"Device is already unbound.");
        }
        
        var (exitCode, errMsg, newDev) = await USBIPD.UnbindDevice(HardwareId);
        Updatethis(newDev);
        if (!IsBound)
        {
            log.Info($"Success to unbind {Description}({HardwareId}).");
            exitCode = 0;
        }
        else
        {
            log.Error($"Failed to unbind {Description}({HardwareId}): {errMsg}");
        }
        return (exitCode, errMsg);
    }

    public async Task<(ExitCode exitCode, string errMsg)> Attach(string? hostIP)
    {
        if (string.IsNullOrEmpty(BusId))
        {
            return (ExitCode.Failure, $"BusID is empty.");
        }

        if (!IsConnected)
        {
            return (ExitCode.Failure, $"Device is not connected.");
        }

        if (!IsBound)
        {
            return (ExitCode.Failure, $"Device is not bound.");
        }

        if (IsAttached)
        {
            return (ExitCode.Success, $"Device is already attached.");
        }
        
        var (exitCode, errMsg, newDev) = await USBIPD.Attach(BusId, hostIP);
        Updatethis(newDev);

        if (IsAttached)
        {
            log.Info($"Success to attach {Description}({HardwareId}) to WSL {(string.IsNullOrWhiteSpace(hostIP) ? "" : "with " + hostIP)}.");
        }
        else
        {
            log.Error($"Failed to attach {Description}({HardwareId}) to WSL {(string.IsNullOrWhiteSpace(hostIP) ? "" : "with " + hostIP)}: {errMsg}");
        }
        return (exitCode, errMsg);
    }

    public async Task<(ExitCode exitCode, string errMsg)> Detach()
    {
        if (string.IsNullOrEmpty(HardwareId))
            return (ExitCode.Failure, "HardwareId is empty.");

        if (!IsConnected)
        {
            return (ExitCode.Failure, $"Device is not connected.");
        }

        if (!IsAttached)
        {
            return (ExitCode.Success, $"Device is already unbound.");
        }
        var (exitCode, errMsg, newDev) = await USBIPD.DetachDevice(HardwareId);
        Updatethis(newDev);

        if (IsAttached)
        {
            log.Info($"Success to detach {Description}({HardwareId}) from WSL.");
        }
        else
        {
            log.Error($"Failed to detach {Description}({HardwareId}) from WSL: {errMsg}");
        }
        return (exitCode, errMsg);
    }
}
