/******************************************************************************
* SPDX-License-Identifier: MIT
* Project: wsl-usb-manager
* Class: USBDeviceInfoModel.cs
* NameSpace: wsl_usb_manager.Domain
* Author: Chuckie
* copyright: Copyright (c) Chuckie, 2024
* Description:
* Create Date: 2024/10/18 19:21
******************************************************************************/

// Ignore Spelling: usb

using log4net;
using wsl_usb_manager.Controller;

namespace wsl_usb_manager.Domain;


public class USBDeviceInfoModel : ViewModelBase
{
    private bool _isBound;
    private bool _isForced;
    private bool _isAttached;
    private bool _isVisible = true;
    private bool _isAutoAttach = false;
    private bool _isCBAutoAttachEnabled = true;
    private bool _isCBBindEnabled = true;
    private bool _isCBAttachEnabled = true;
    private bool _isCBForceEnabled = true;
    private readonly USBDevice _device = new();
    private static readonly ILog log = LogManager.GetLogger(typeof(USBDeviceInfoModel));

    private void UpdateDeviceInfo()
    {
        IsForced = _device.IsForced;
        IsBound = _device.IsBound;
        IsAttached = _device.IsAttached;
        IsVisible = !IsInFilterDeviceList();

        if (!IsVisible)
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
    public bool IsInFilterDeviceList() => App.GetSysConfig().IsInFilterDeviceList(_device);
    public bool IsInAutoAttachList() => App.GetSysConfig().IsInAutoAttachDeviceList(_device);

    public USBDeviceInfoModel(USBDevice dev)
    {
        _device = dev;
        UpdateDeviceInfo();
    }

    public string BusID { get => _device.BusId ?? ""; }

    public string HardwareId { get => _device.HardwareId ?? ""; }
    public bool IsConnected { get => _device.IsConnected; }
    public bool IsBound
    {
        get => _isBound;
        set => SetProperty(ref _isBound, value);
    }
    public bool IsForced
    {
        get => _isForced;
        set => SetProperty(ref _isForced, value);
    }
    public bool IsAttached
    {
        get => _isAttached;
        set { 
            SetProperty(ref _isAttached, value);
            
        }
    }

    public string ClientIPAddress { get => _device.ClientIPAddress ?? ""; }
    public string Description { get => _device.Description ?? ""; }
    public string PersistedGuid { get => _device.PersistedGuid ?? ""; }
    public string InstanceId { get => _device.InstanceId ?? ""; }
    public string StubInstanceId { get => _device.StubInstanceId ?? ""; }

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
            CBBindEnabled = ((!IsInFilterDeviceList()) && (!value || !IsBound));
            CBAttachEnabled = ((!IsInFilterDeviceList()) && (IsBound && ((value && !IsAttached) || !value)));
            CBForcedEnable = ((!IsInFilterDeviceList()) && (!IsBound && !value));
        }
    }

    public USBDevice Device { get => _device; }

    public async Task<bool> Bind()
    {
        Device.IsForced = IsForced;
        (ExitCode ret, string err) = await Device.Bind();
        if (ret != ExitCode.Success)
        {
            NotifyService.ShowUSBIPDError(ret, err, Device);
        }
        UpdateDeviceInfo();
        return IsBound;
    }
    public async Task<bool> Unbind()
    {
        string name = string.IsNullOrWhiteSpace(Device.Description) ? Device.HardwareId : Device.Description.Split(",", StringSplitOptions.RemoveEmptyEntries)[0];
        (ExitCode ret, string err) = await Device.Unbind();
        if (ret != ExitCode.Success) {
            NotifyService.ShowErrorMessage($"Failed to unbind '{name}': {err}");
        }
        UpdateDeviceInfo();
        return (!IsBound);
    }
    public async Task<bool> Attach()
    {
        (ExitCode ret, string err) = await Device.Attach(NetworkCardInfo.GetIPAddress(App.GetAppConfig().ForwardNetCard));
        if (ret != ExitCode.Success)
        {
            NotifyService.ShowUSBIPDError(ret,err,Device);
        }
        UpdateDeviceInfo();
        return IsAttached;
    }
    public async Task<bool> Detach()
    {
        string name = string.IsNullOrWhiteSpace(Device.Description) ? Device.HardwareId : Device.Description.Split(",", StringSplitOptions.RemoveEmptyEntries)[0];
        (ExitCode ret, string err) = await Device.Detach();
        if (ret != ExitCode.Success)
        {
            NotifyService.ShowErrorMessage($"Failed to detach '{name}': {err}");
        }
        UpdateDeviceInfo();
        return (!IsAttached);
    }

    public async Task<bool> AutoAttach()
    {
        string name = string.IsNullOrWhiteSpace(Device.Description) ? Device.HardwareId : Device.Description.Split(",", StringSplitOptions.RemoveEmptyEntries)[0];
        if (!IsInAutoAttachList())
        {
            return false;
        }

        if (!Device.IsConnected)
        {
            NotifyService.ShowErrorMessage($"The device '{name}' is not connected!");
            return false;
        }

        if (!Device.IsBound)
        {
            if (!await Bind())
            {
                return false;
            }
        }

        if (!Device.IsAttached)
        {
            return await Attach();
        }
        return true;    
    }

    public async Task AddToAutoAttach()
    {
        string? name = string.IsNullOrWhiteSpace(Description) ? HardwareId :
                        Description.Split(",", StringSplitOptions.RemoveEmptyEntries)[0];
        IsAutoAttach = true;
        if (IsInAutoAttachList())
        {
            return;
        }
        
        App.GetSysConfig().AddToAutoAttachDeviceList(Device);
        App.SaveConfig();
        NotifyService.ShowNotification($"'{name}' is added to auto attach list.");
        await AutoAttach();
    }

    public void RemoveFromAutoAttach()
    {
        string? name = string.IsNullOrWhiteSpace(Description) ? HardwareId :
                        Description.Split(",", StringSplitOptions.RemoveEmptyEntries)[0];
        IsAutoAttach = false;
        if (!IsInAutoAttachList())
        {
            return;
        }
        App.GetSysConfig().RemoveFromAutoAttachDeviceList(Device);
        App.SaveConfig();
        NotifyService.ShowNotification($"'{name}' is removed from auto attach list.");
    }

    public void AddToFilter()
    {
        if (!IsInFilterDeviceList())
        {
            App.GetSysConfig().AddToFilteredDeviceList(Device);
            App.SaveConfig();
            IsVisible = false;
            NotifyService.ShowNotification($"{Device.Description} is added to filter list.");
        }
    }

    public void RemoveFromFilter()
    {
        if (IsInFilterDeviceList())
        {
            App.GetSysConfig().RemoveFromFilteredDevice(Device);
            App.SaveConfig();
            NotifyService.ShowNotification($"{Device.Description} is removed from filter list.");
            IsVisible = true;
        }
    }
}
