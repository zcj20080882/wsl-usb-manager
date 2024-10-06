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

// Ignore Spelling: App

using log4net;
using System.Collections.ObjectModel;
using wsl_usb_manager.Controller;
using wsl_usb_manager.Domain;

namespace wsl_usb_manager.Settings;

public class SettingViewModel : ViewModelBase
{
    private string? _distribution;
    private string? _forwardNetCard;
    private bool _useWSLAttach;
    private bool _closeToTray;
    private bool _specifyWSLDistribution;
    private ObservableCollection<string>? _listDistribution;
    private ObservableCollection<string>? _listNetworkCard;
    private ApplicationConfig AppConfig { get; set; }
    private static readonly ILog log = LogManager.GetLogger(typeof(SettingViewModel));

    public SettingViewModel(ApplicationConfig appcfg)
    {
        Distributions = [];
        foreach (var distrib in WSLHelper.GetAllWSLDistribution())
        {
            Distributions.Add(distrib);
        }
        NetworkCards = [];
        foreach (var netcard in NetworkCardInfo.GetAllNetworkCardName())
        {
            NetworkCards.Add(netcard);
        }
        AppConfig = appcfg;
        SelectedDistribution = appcfg.DefaultDistribution;
        SelectedForwardNetCard = appcfg.ForwardNetCard;
        UseWSLAttach = appcfg.UseWSLAttach;
        CloseToTray = appcfg.CloseToTray;
        SpecifyWSLDistribution = appcfg.SpecifyWSLDistribution;
    }

    
    public ObservableCollection<string>? Distributions { get => _listDistribution; set => SetProperty(ref _listDistribution, value);}

    public ObservableCollection<string>? NetworkCards { get => _listNetworkCard; set => SetProperty(ref _listNetworkCard, value);}

    public string? SelectedDistribution
    {
        get => _distribution;
        set => SetProperty(ref _distribution, value);
    }

    public string? SelectedForwardNetCard
    {
        get => _forwardNetCard;
        set => SetProperty(ref _forwardNetCard, value);
    }

    public bool UseWSLAttach
    {
        get => _useWSLAttach;
        set => SetProperty(ref _useWSLAttach, value);
    }

    public bool CloseToTray
    {
        get => _closeToTray;
        set => SetProperty(ref _closeToTray, value);
    }

    public bool SpecifyWSLDistribution
    {
        get => _specifyWSLDistribution;
        set => SetProperty(ref _specifyWSLDistribution, value);
    }

    public void SaveConfig()
    {
        AppConfig.CloseToTray = CloseToTray;
        AppConfig.SpecifyWSLDistribution = SpecifyWSLDistribution;
        AppConfig.UseWSLAttach = UseWSLAttach;
        AppConfig.ForwardNetCard = SelectedForwardNetCard ?? "";
        AppConfig.DefaultDistribution = SelectedDistribution ?? "";
        log.Debug($"Selected distribution: {SelectedDistribution}");
    }
}

