/******************************************************************************
* SPDX-License-Identifier: MIT
* Project: wsl-usb-manager
* Class: SettingViewModel.cs
* NameSpace: wsl_usb_manager.Settings
* Author: Chuckie
* copyright: Copyright (c) Chuckie, 2024
* Description:
* Create Date: 2024/10/17 20:22
******************************************************************************/

// Ignore Spelling: App

using log4net;
using System.Collections.ObjectModel;
using wsl_usb_manager.Controller;
using wsl_usb_manager.Domain;

namespace wsl_usb_manager.Settings;

public class SettingViewModel : ViewModelBase
{
    private string? _forwardNetCard;
    private bool _isSpecifyNetCard;
    private bool _closeToTray;
    private ObservableCollection<string>? _listNetworkCard;
    private ApplicationConfig AppConfig { get; set; }
    private static readonly ILog log = LogManager.GetLogger(typeof(SettingViewModel));

    public SettingViewModel(ApplicationConfig appcfg)
    {
        NetworkCards = [];
        foreach (var netcard in NetworkCardInfo.GetAllNetworkCardName())
        {
            NetworkCards.Add(netcard);
        }
        AppConfig = appcfg;
        SelectedForwardNetCard = appcfg.ForwardNetCard;
        IsSpecifyNetCard = appcfg.SpecifyNetCard;
        CloseToTray = appcfg.CloseToTray;
    }

    

    public ObservableCollection<string>? NetworkCards { get => _listNetworkCard; set => SetProperty(ref _listNetworkCard, value);}


    public string? SelectedForwardNetCard
    {
        get => _forwardNetCard;
        set => SetProperty(ref _forwardNetCard, value);
    }

    public bool IsSpecifyNetCard
    {
        get => _isSpecifyNetCard;
        set => SetProperty(ref _isSpecifyNetCard, value);
    }

    public bool CloseToTray
    {
        get => _closeToTray;
        set => SetProperty(ref _closeToTray, value);
    }


    public void SaveConfig()
    {
        AppConfig.CloseToTray = CloseToTray;
        AppConfig.SpecifyNetCard = IsSpecifyNetCard;
        AppConfig.ForwardNetCard = SelectedForwardNetCard ?? "";
    }
}

