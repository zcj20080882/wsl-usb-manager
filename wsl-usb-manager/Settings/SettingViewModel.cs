/******************************************************************************
* SPDX-License-Identifier: MIT
* Project: wsl-usb-manager
* Class: SettingViewModel.cs
* NameSpace: wsl_usb_manager.Settings
* Author: Chuckie
* copyright: Copyright (c) Chuckie, 2024
* Description:
* Create Date: 2024/10/5 16:45
******************************************************************************/
using wsl_usb_manager.Controller;
using wsl_usb_manager.Domain;

namespace wsl_usb_manager.Settings;

public class SettingViewModel(SystemConfig syscfg) : ViewModelBase
{
    private string? _distribution;
    private string? _forwardNetCard;
    private bool _useWSLAttach;
    private bool _closeToTray;

    private readonly SystemConfig SysConfig = syscfg;

    public List<string> Distributions => WSLHelper.GetAllWSLDistribution();

    public List<string> NetworkCards => NetworkCardInfo.GetAllNetworkCardName() ?? [];

    public string? SelectedDistribution
    {
        get => _distribution;
        set
        {
            SetProperty(ref _distribution, value);
            if (SysConfig != null && value != null && value != SysConfig.DefaultDistribution)
            {
                SysConfig.DefaultDistribution = value;
            }
        }
    }

    public string? ForwardNetCard
    {
        get => _forwardNetCard;
        set
        {
            SetProperty(ref _forwardNetCard, value);
            if (SysConfig != null && value != null && value != SysConfig.ForwardNetCard)
            {
                SysConfig.ForwardNetCard = value;
            }
        }
    }

    public bool UseWSLAttach
    {
        get => _useWSLAttach;
        set
        {
            SetProperty(ref _useWSLAttach, value);
            if (SysConfig != null && value != SysConfig.UseWSLAttach)
            {
                SysConfig.UseWSLAttach = value;
            }
        }
    }

    public bool CloseToTray
    {
        get => _closeToTray;
        set
        {
            SetProperty(ref _closeToTray, value);
            if (SysConfig != null && value != SysConfig.CloseToTray)
            {
                SysConfig.CloseToTray = value;
            }
        }
    }
}

