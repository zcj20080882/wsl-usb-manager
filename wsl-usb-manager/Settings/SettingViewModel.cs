/******************************************************************************
* SPDX-License-Identifier: MIT
* Project: wsl-usb-manager
* Class: SettingViewModel.cs
* NameSpace: wsl_usb_manager.Settings
* Author: Chuckie
* copyright: Copyright (c) Chuckie, 2025
* Description:
* Create Date: 2024/10/17 20:22
******************************************************************************/

// Ignore Spelling: App

using log4net;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using wsl_usb_manager.Domain;
using wsl_usb_manager.Resources;

namespace wsl_usb_manager.Settings;

public class SettingViewModel : ViewModelBase
{
    private string? _forwardNetCard;
    private bool _isSpecifyNetCard;
    private bool _closeToTray;
    private bool _useBusID;
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
        UseBusID = appcfg.UseBusID;
        ResetCfgCommand = new CommandImplementations(RestorDefaultConfiguration);
        ClearLogCommand = new CommandImplementations(ClearHistoryLog);
        OpenLogPathCommand = new CommandImplementations(OpenConfigurationPath);
    }



    public ObservableCollection<string>? NetworkCards { get => _listNetworkCard; set => SetProperty(ref _listNetworkCard, value); }


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

    public bool UseBusID
    {
        get => _useBusID;
        set => SetProperty(ref _useBusID, value);
    }

    public void SaveConfig()
    {
        AppConfig.CloseToTray = CloseToTray;
        AppConfig.UseBusID = UseBusID;
        AppConfig.SpecifyNetCard = IsSpecifyNetCard;
        AppConfig.ForwardNetCard = SelectedForwardNetCard ?? "";
    }

    public ICommand ResetCfgCommand { get; }
    public ICommand ClearLogCommand { get; }
    public ICommand OpenLogPathCommand { get; }

    private void RestorDefaultConfiguration(object? obj)
    {
        App.RestoreDefaultConfiguration();
        ApplicationConfig appcfg = App.GetAppConfig();
        AppConfig = appcfg;
        SelectedForwardNetCard = appcfg.ForwardNetCard;
        IsSpecifyNetCard = appcfg.SpecifyNetCard;
        CloseToTray = appcfg.CloseToTray;
    }

    private void ClearHistoryLog(object? obj)
    {
        App.RemoveHistroicalLogs();
        System.Windows.MessageBox.Show(Lang.IsChinese() ? "历史日志已经删除。":"History logs have been cleared.", Lang.IsChinese() ? "提示" : "Note");
    }
    private void OpenConfigurationPath(object? obj)
    {
        if (!string.IsNullOrEmpty(App.ConfigurationDirPath) && Directory.Exists(App.ConfigurationDirPath))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = App.ConfigurationDirPath,
                UseShellExecute = true,
                Verb = "open"
            });
        }
        else
        {
            log.Error($"Directory does not exist: {App.ConfigurationDirPath}");
        }
    }
}

