/******************************************************************************
* SPDX-License-Identifier: MIT
* Project: wsl-usb-manager
* Class: USBDeviceInfoModel.cs
* NameSpace: wsl_usb_manager.USBDevices
* Author: Chuckie
* copyright: Copyright (c) Chuckie, 2024
* Description:
* Create Date: 2024/10/3 11:48
******************************************************************************/
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using wsl_usb_manager.Controller;
using wsl_usb_manager.Domain;

namespace wsl_usb_manager.USBDevices;

public class BoolToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? "True" : "False";
        }
        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string strValue)
        {
            return strValue.Equals("True", StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }
}

public class USBDeviceInfoModel : ViewModelBase
{
    private string? _busID;
    private string? _hardwareId;
    private bool _isConnected;
    private bool _isBound;
    private bool _isForced;
    private string? _clientIPAddress;
    private string? _description;
    private string? _persistedGuid;
    private string? _instanceId;
    private string? _stubInstanceId;
    private bool _isAttached;

    public USBDeviceInfoModel(USBDevicesInfo? usbDeviceInfo)
    {
        InstanceId = usbDeviceInfo?.InstanceId;
        HardwareId = usbDeviceInfo?.HardwareId;
        Description = usbDeviceInfo?.Description;

        BusID = usbDeviceInfo?.BusId;
        PersistedGuid = usbDeviceInfo?.PersistedGuid;
        StubInstanceId = usbDeviceInfo?.StubInstanceId;
        ClientIPAddress = usbDeviceInfo?.ClientIPAddress;

        IsForced = StringToBool(usbDeviceInfo?.IsForced);
        IsBound = StringToBool(usbDeviceInfo?.IsBound);
        IsConnected = StringToBool(usbDeviceInfo?.IsConnected);
        IsAttached = StringToBool(usbDeviceInfo?.IsAttached);
    }

    private bool StringToBool(string? value)
    {
        if (bool.TryParse(value, out bool result))
        {
            return result;
        }
        return false;
    }

    public string? BusID
    { get => _busID; set => SetProperty(ref _busID, value); }
    public string? HardwareId { get => _hardwareId; set => SetProperty(ref _hardwareId, value); }
    public bool IsConnected { get => _isConnected; set => SetProperty(ref _isConnected, value); }
    public bool IsBound { get => _isBound; set => SetProperty(ref _isBound, value); }
    public bool IsForced { get => _isForced; set => SetProperty(ref _isForced, value); }
    public bool IsAttached { get => _isAttached; set => SetProperty(ref _isAttached, value); }
    public string? ClientIPAddress { get => _clientIPAddress; set => SetProperty(ref _clientIPAddress, value); }
    public string? Description { get => _description; set => SetProperty(ref _description, value); }
    public string? PersistedGuid { get => _persistedGuid; set => SetProperty(ref _persistedGuid, value); }
    public string? InstanceId { get => _instanceId; set => SetProperty(ref _instanceId, value); }
    public string? StubInstanceId { get => _stubInstanceId; set => SetProperty(ref _stubInstanceId, value); }
    public bool ForcedEnable => !IsBound;
    public bool AttachEnabled => IsBound;
}
