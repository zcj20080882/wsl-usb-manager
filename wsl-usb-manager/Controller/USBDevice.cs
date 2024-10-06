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
using System.Runtime.Serialization;

namespace wsl_usb_manager.Controller;

[DataContract]
public sealed class USBDevice
{
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
}
