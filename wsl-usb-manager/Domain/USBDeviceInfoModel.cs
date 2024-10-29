/******************************************************************************
* SPDX-License-Identifier: MIT
* Project: wsl-usb-manager
* Class: USBDeviceInfoModel.cs
* NameSpace: wsl_usb_manager.USBDevices
* Author: Chuckie
* copyright: Copyright (c) Chuckie, 2024
* Description:
* Create Date: 2024/10/17 20:22
******************************************************************************/

// Ignore Spelling: usb

using wsl_usb_manager.Controller;

namespace wsl_usb_manager.Domain;


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
    private bool _isVisible = true;
    private bool _isAutoAttach = false;
    private bool _isCBAutoAttachEnabled = true;
    private bool _isCBBindEnabled = true;
    private bool _isCBAttachEnabled = true;
    private bool _isCBForceEnabled = true;
    private readonly MainWindowViewModel MainVM;
    private readonly USBDevice _device = new();

    private bool IsInFilterDeviceList() => App.GetSysConfig().IsInFilterDeviceList(_device);
    private bool IsInAutoAttachList() => App.GetSysConfig().IsInAutoAttachDeviceList(_device);
    public USBDeviceInfoModel(USBDevice? usbDeviceInfo, MainWindowViewModel mainVM)
    {
        MainVM = mainVM;
        InstanceId = usbDeviceInfo?.InstanceId;
        HardwareId = usbDeviceInfo?.HardwareId;
        Description = usbDeviceInfo?.Description;

        BusID = usbDeviceInfo?.BusId;
        PersistedGuid = usbDeviceInfo?.PersistedGuid;
        StubInstanceId = usbDeviceInfo?.StubInstanceId;
        ClientIPAddress = usbDeviceInfo?.ClientIPAddress;

        IsForced = usbDeviceInfo?.IsForced ?? false;
        IsBound = usbDeviceInfo?.IsBound ?? false;
        IsConnected = usbDeviceInfo?.IsConnected ?? false;
        IsAttached = usbDeviceInfo?.IsAttached ?? false;
        if (usbDeviceInfo != null)
            IsVisible = !IsInFilterDeviceList();
        else
            IsVisible = false;

        _device = usbDeviceInfo ?? new USBDevice();

        if (IsInFilterDeviceList())
        {
            IsAutoAttach = false;
            CBAutoAttachEnabled = false;
            CBBindEnabled = false;
            CBAttachEnabled = false;
            CBForcedEnable = false;
        }
        else
        {
            IsAutoAttach = IsInAutoAttachList();
            CBAutoAttachEnabled = true;
            CBBindEnabled = !IsAutoAttach || !IsBound;
            if (IsAutoAttach)
            {
                CBAttachEnabled = !IsAttached;
            }
            else
            {
                CBAttachEnabled = IsBound;
            }

            CBForcedEnable = !IsBound;
        }

    }

    public string? BusID
    {
        get => _busID;
        set
        {
            SetProperty(ref _busID, value);
            _device.BusId = value ?? "";
        }
    }
    public string? HardwareId
    {
        get => _hardwareId;
        set
        {
            SetProperty(ref _hardwareId, value);
            _device.HardwareId = value ?? "";
        }
    }
    public bool IsConnected
    {
        get => _isConnected;
        set
        {
            SetProperty(ref _isConnected, value);
            _device.IsConnected = value;
        }
    }
    public bool IsBound
    {
        get => _isBound;
        set
        {
            SetProperty(ref _isBound, value);
            _device.IsBound = value;
        }
    }
    public bool IsForced
    {
        get => _isForced;
        set
        {
            SetProperty(ref _isForced, value);
            _device.IsForced = value;
        }
    }
    public bool IsAttached
    {
        get => _isAttached;
        set
        {
            SetProperty(ref _isAttached, value);
            _device.IsAttached = value;
        }
    }
    public string? ClientIPAddress
    {
        get => _clientIPAddress;
        set
        {
            SetProperty(ref _clientIPAddress, value);
            _device.ClientIPAddress = value ?? "";
        }
    }
    public string? Description
    {
        get => _description;
        set
        {
            SetProperty(ref _description, value);
            _device.Description = value ?? "";
        }
    }
    public string? PersistedGuid
    {
        get => _persistedGuid;
        set
        {
            SetProperty(ref _persistedGuid, value);
            _device.PersistedGuid = value ?? "";
        }
    }
    public string? InstanceId
    {
        get => _instanceId;
        set
        {
            SetProperty(ref _instanceId, value);
            _device.InstanceId = value ?? "";
        }
    }
    public string? StubInstanceId
    {
        get => _stubInstanceId;
        set
        {
            SetProperty(ref _stubInstanceId, value);
            _device.StubInstanceId = value ?? "";
        }
    }

    public bool CBAutoAttachEnabled
    {
        get => _isCBAutoAttachEnabled;
        set => SetProperty(ref _isCBAutoAttachEnabled, value);
    }
    public bool CBBindEnabled
    {
        get => _isCBBindEnabled;
        set => SetProperty(ref _isCBBindEnabled, value);
    }
    public bool CBForcedEnable
    {
        get => _isCBForceEnabled; set => SetProperty(ref _isCBForceEnabled, value);
    }
    public bool CBAttachEnabled
    {
        get => _isCBAttachEnabled; set => SetProperty(ref _isCBAttachEnabled, value);
    }


    public bool IsVisible
    {
        get => _isVisible;
        set => SetProperty(ref _isVisible, value);
    }

    public bool IsAutoAttach
    {
        get => _isAutoAttach;
        set
        {
            SetProperty(ref _isAutoAttach, value);
            if (value)
            {
                if (!App.GetSysConfig().IsInAutoAttachDeviceList(Device))
                {
                    App.GetSysConfig().AddToAutoAttachDeviceList(Device);
                    App.SaveConfig();
                    MainVM.ShowNotification($"{Device.Description} is added to auto attach list.");
                    AutoAttach();
                }
            }
            else
            {
                if (App.GetSysConfig().IsInAutoAttachDeviceList(Device))
                {
                    App.GetSysConfig().RemoveFromAutoAttachDeviceList(Device);
                    App.SaveConfig();
                    MainVM.ShowNotification($"{Device.Description} is removed from auto attach list.");
                }
            }
        }
    }

    public USBDevice Device { get => _device; }

    public async void Bind() => await MainVM.BindDevice(Device);
    public async void Unbind() => await MainVM.UnbindDevice(Device);
    public async void Attach() => await MainVM.AttachDevice(Device);
    public async void Detach() => await MainVM.DetachDevice(Device);
    public async void AutoAttach() => await MainVM.AutoAttachDevices(Device);

    public void AddToAutoAttach()
    {
        IsAutoAttach = true;
    }

    public void RemoveFromAutoAttach()
    {
        IsAutoAttach = false;
    }

    public void AddToFilter()
    {
        if (!App.GetSysConfig().IsInFilterDeviceList(Device))
        {
            App.GetSysConfig().AddToFilteredDeviceList(Device);
            App.SaveConfig();
            IsVisible = false;
            MainVM.ShowNotification($"{Device.Description} is added to filter list.");
        }
    }

    public void RemoveFromFilter()
    {
        if (App.GetSysConfig().IsInFilterDeviceList(Device))
        {
            App.GetSysConfig().RemoveFromFilteredDevice(Device);
            App.SaveConfig();
            MainVM.ShowNotification($"{Device.Description} is removed from filter list.");
            IsVisible = true;
        }
    }
}
