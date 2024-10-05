/******************************************************************************
* SPDX-License-Identifier: MIT
* Project: wsl-usb-manager
* Class: SystemConfig.cs
* NameSpace: wsl_usb_manager.Domain
* Author: Chuckie
* copyright: Copyright (c) Chuckie, 2024
* Description:
* Create Date: 2024/10/5 13:35
******************************************************************************/
namespace wsl_usb_manager.Domain;


public class SystemConfig
{
    public bool DarkMode { get; set; } = false;
    public string Language { get; set; } = "en-US";
    public bool CloseToTray = true;
    public bool UseWSLAttach = false;
    public string DefaultDistribution = "";

    public List<string> AutoAttachDevices = [];
}
