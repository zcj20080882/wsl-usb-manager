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

    public async void UpdateThis()
    {
        var (_, _, DevicesList) = await USBIPDWin.ListConnectedDevices(HardwareId);
        if (DevicesList != null && DevicesList.Count > 0)
        {
            var dev = DevicesList[0];
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

    public async Task<(bool Success, string ErrMsg)> Bind(bool useBusID)
    {
        string id = useBusID ? BusId : HardwareId;

        if (string.IsNullOrEmpty(id))
            return (false, $"{(useBusID ? "BusID" : "HardwareID")} is empty.");

        if (!IsConnected)
        {
            return (false, $"Device({id}) is not connected.");
        }

        if (IsBound)
        {
            return (true, $"Device({id}) is already bound.");
        }

        var ret = await USBIPDWin.BindDevice(id, useBusID, IsForced);

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
            ret = await USBIPDWin.BindDevice(id, useBusID, IsForced);
        }

        if (IsBound)
        {
            log.Info($"Success to bind {Description}({id}){(IsForced ? " forcibly" : "")}.");
        }
        else
        {
            log.Error($"Failed to bind {Description}({id}){(IsForced ? " forcibly" : "")}: {ret.ErrMsg}");
        }
        return (IsBound, ret.ErrMsg);
    }

    public async Task<(bool Success, string ErrMsg)> Unbind(bool useBusID)
    {
        string id = useBusID ? BusId : HardwareId;

        if (string.IsNullOrEmpty(id))
            return (false, $"{(useBusID ? "BusID" : "HardwareID")} is empty.");

        if (!IsBound)
        {
            return (true, $"Device({id}) is already unbound.");
        }

        var (ErrCode, ErrMsg) = await USBIPDWin.UnbindDevice(id, useBusID);

        if (ErrCode == ErrorCode.Success)
        {
            IsBound = false;
            log.Info($"Success to unbind {Description}({id}).");
        }
        else
        {
            log.Error($"Failed to unbind {Description}({id}): {ErrMsg}");
        }
        return (!IsBound, ErrMsg);
    }

    public async Task<(bool Success, string ErrMsg)> Attach(bool useBusID, string? hostIP, bool isAuto)
    {
        string id = useBusID ? BusId : HardwareId;

        if (string.IsNullOrEmpty(id))
        {
            return (false, $"{(useBusID ? "BusID" : "HardwareID")} is empty.");
        }

        if (!IsConnected)
        {
            return (false, $"Device({id}) is not connected.");
        }

        if (!IsBound)
        {
            return (false, $"Device({id}) is not bound.");
        }

        if (IsAttached)
        {
            return (true, $"Device({id}) is already attached.");
        }

        var (ErrCode, ErrMsg) = await USBIPDWin.Attach(id, useBusID, isAuto, hostIP);

        if (ErrCode == ErrorCode.Success)
        {
            IsAttached = true;
            log.Info($"Success to attach {Description}({id}) to WSL {(string.IsNullOrWhiteSpace(hostIP) ? "" : "with " + hostIP)}.");
        }
        else
        {
            log.Error($"Failed to attach {Description}({id}) to WSL {(string.IsNullOrWhiteSpace(hostIP) ? "" : "with " + hostIP)}: {ErrMsg}");
        }
        return (IsAttached, ErrMsg);
    }

    public async Task<(bool Success, string ErrMsg)> Detach(bool useBusID)
    {
        string id = useBusID ? BusId : HardwareId;
        if (string.IsNullOrEmpty(id))
            return (false, $"{(useBusID ? "BusID" : "HardwareID")} is empty.");

        if (!IsConnected)
        {
            return (false, $"Device({id}) is not connected.");
        }

        if (!IsAttached)
        {
            return (true, $"Device({id}) is already unbound.");
        }
        var (ErrCode, ErrMsg) = await USBIPDWin.DetachDevice(id, useBusID);

        if (ErrCode == ErrorCode.Success)
        {
            IsAttached = false;
            log.Info($"Success to detach {Description}({id}) from WSL.");
        }
        else
        {
            log.Error($"Failed to detach {Description}({id}) from WSL: {ErrMsg}");
        }
        return (!IsAttached, ErrMsg);
    }
}
