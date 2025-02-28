/******************************************************************************
* SPDX-License-Identifier: MIT
* Project: wsl-usb-manager
* Class: USBDevice.cs
* NameSpace: wsl_usb_manager.USBIPD
* Author: Chuckie
* copyright: Copyright (c) Chuckie, 2025
* Description:
* Create Date: 2025/3/2 18:07
******************************************************************************/
using log4net;
using System.Runtime.Serialization;
using wsl_usb_manager.Domain;

namespace wsl_usb_manager.USBIPD;

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
    [DataMember]
    public string Name { get; set; } = string.Empty;

    private async void UpdateThis()
    {
        var ret = await USBIPDWin.ListConnectedDevices(HardwareId);
        if (ret.DevicesList != null && ret.DevicesList.Count > 0)
        {
            var dev = ret.DevicesList[0];
            InstanceId = dev.InstanceId;
            HardwareId = dev.HardwareId;
            Description = dev.Description;
            IsForced = dev.IsForced;
            BusId = dev.BusId;
            PersistedGuid = dev.PersistedGuid;
            StubInstanceId = dev.StubInstanceId;
            ClientIPAddress = dev.ClientIPAddress;
            IsBound = dev.IsBound;
            IsConnected = dev.IsConnected;
            IsAttached = dev.IsAttached;
        }
    }

    public USBDevice Clone()
    {
        return new USBDevice
        {
            InstanceId = InstanceId,
            HardwareId = HardwareId,
            Description = Description,
            IsForced = IsForced,
            BusId = BusId,
            PersistedGuid = PersistedGuid,
            StubInstanceId = StubInstanceId,
            ClientIPAddress = ClientIPAddress,
            IsBound = IsBound,
            IsConnected = IsConnected,
            IsAttached = IsAttached
        };
    }
    public override bool Equals(object? obj)
    {
        if (obj is USBDevice other)
        {
            return 
                    InstanceId == other.InstanceId &&
                    HardwareId == other.HardwareId &&
                    Description == other.Description &&
                    IsForced == other.IsForced &&
                    BusId == other.BusId &&
                    PersistedGuid == other.PersistedGuid &&
                    StubInstanceId == other.StubInstanceId &&
                    ClientIPAddress == other.ClientIPAddress &&
                    IsBound == other.IsBound &&
                    IsConnected == other.IsConnected &&
                    IsAttached == other.IsAttached
                ;
        }
        return false;
    }

    public override int GetHashCode()
    {
        HashCode hash = new();
        hash.Add(InstanceId);
        hash.Add(HardwareId);
        hash.Add(Description);
        hash.Add(IsForced);
        hash.Add(BusId);
        hash.Add(PersistedGuid);
        hash.Add(StubInstanceId);
        hash.Add(ClientIPAddress);
        hash.Add(IsBound);
        hash.Add(IsConnected);
        hash.Add(IsAttached);
        return hash.ToHashCode();
    }

    public async Task<(bool Success, string ErrMsg)> Bind()
    {
        if (string.IsNullOrEmpty(HardwareId))
            return (false, $"HardwareId is empty.");

        if (!IsConnected)
        {
            return (false, $"Device is not connected.");
        }

        if(IsBound)
        {
            return (true, $"Device is already bound.");
        }
        
        var ret = await USBIPDWin.BindDevice(HardwareId, IsForced);
        for (int i = 0; i < 3; i++)
        {
            if (ret.ErrCode == ErrorCode.Success)
            {
                IsBound = true;
                break;
            }
            await Task.Delay(500);
            UpdateThis();
            if (IsBound)
            {
                break;
            }
            ret = await USBIPDWin.BindDevice(HardwareId, IsForced);
        }
        
        if (IsBound)
        {
            log.Info($"Success to bind {Description}({HardwareId}){(IsForced ? " forcibly" : "")}.");
        }
        else
        {
            log.Error($"Failed to bind {Description}({HardwareId}){(IsForced ? " forcibly" : "")}: {ret.ErrMsg}");
        }
        return (IsBound, ret.ErrMsg);
    }

    public async Task<(bool Success, string ErrMsg)> Unbind()
    {
        if (string.IsNullOrEmpty(HardwareId))
            return (false, "HardwareId is empty.");

        if (!IsBound)
        {
            return (true, $"Device is already unbound.");
        }
        
        var (ErrCode, ErrMsg) = await USBIPDWin.UnbindDevice(HardwareId);

        if (ErrCode == ErrorCode.Success)
        {
            IsBound = false;
            log.Info($"Success to unbind {Description}({HardwareId}).");
        }
        else
        {
            log.Error($"Failed to unbind {Description}({HardwareId}): {ErrMsg}");
        }
        return (!IsBound, ErrMsg);
    }

    public async Task<(bool Success, string ErrMsg)> Attach(string? hostIP, bool isAuto)
    {
        if (string.IsNullOrEmpty(BusId))
        {
            return (false, $"BusID is empty.");
        }

        if (!IsConnected)
        {
            return (false, $"Device is not connected.");
        }

        if (!IsBound)
        {
            return (false, $"Device is not bound.");
        }

        if (IsAttached)
        {
            return (true, $"Device is already attached.");
        }
        
        var (ErrCode, ErrMsg) = await USBIPDWin.Attach(BusId,isAuto, hostIP);

        if (ErrCode == ErrorCode.Success)
        {
            IsAttached = true;
            log.Info($"Success to attach {Description}({HardwareId}) to WSL {(string.IsNullOrWhiteSpace(hostIP) ? "" : "with " + hostIP)}.");
        }
        else
        {
            log.Error($"Failed to attach {Description}({HardwareId}) to WSL {(string.IsNullOrWhiteSpace(hostIP) ? "" : "with " + hostIP)}: {ErrMsg}");
        }
        return (IsAttached, ErrMsg);
    }

    public async Task<(bool Success, string ErrMsg)> Detach()
    {
        if (string.IsNullOrEmpty(HardwareId))
            return (false, "HardwareId is empty.");

        if (!IsConnected)
        {
            return (false, $"Device is not connected.");
        }

        if (!IsAttached)
        {
            return (true, $"Device is already unbound.");
        }
        var (ErrCode, ErrMsg) = await USBIPDWin.DetachDevice(HardwareId);

        if (ErrCode == ErrorCode.Success)
        {
            IsAttached = false;
            log.Info($"Success to detach {Description}({HardwareId}) from WSL.");
        }
        else
        {
            log.Error($"Failed to detach {Description}({HardwareId}) from WSL: {ErrMsg}");
        }
        return (!IsAttached, ErrMsg);
    }
}
