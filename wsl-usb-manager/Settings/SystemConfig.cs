/******************************************************************************
* SPDX-License-Identifier: MIT
* Project: wsl-usb-manager
* Class: SystemConfig.cs
* NameSpace: wsl_usb_manager.Settings
* Author: Chuckie
* copyright: Copyright (c) Chuckie, 2024
* Description:
* Create Date: 2024/10/5 13:35
******************************************************************************/

// Ignore Spelling: App

using Newtonsoft.Json;
using System;
using System.IO;
using System.Xml.Linq;
using wsl_usb_manager.Controller;

namespace wsl_usb_manager.Settings;

public class ApplicationConfig
{
    public bool DarkMode = false;
    public bool IsChinese = false;
    public bool CloseToTray = true;
    public bool UseWSLAttach = false;
    public bool SpecifyWSLDistribution = false;
    public string ForwardNetCard = "";
    public string DefaultDistribution = "";

    public override bool Equals(object? obj)
    {
        if (obj is ApplicationConfig other)
        {
            return (this.DarkMode == other.DarkMode && this.IsChinese == other.IsChinese
                    && this.CloseToTray == other.CloseToTray && this.UseWSLAttach == other.UseWSLAttach
                    && this.SpecifyWSLDistribution == other.SpecifyWSLDistribution && 
                    this.ForwardNetCard == other.ForwardNetCard && this.DefaultDistribution == other.DefaultDistribution);
        }
        return false;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(DarkMode, IsChinese, CloseToTray, UseWSLAttach, SpecifyWSLDistribution, ForwardNetCard);
    }

    public ApplicationConfig Clone() => new()
    {
        DarkMode = DarkMode,
        IsChinese = IsChinese,
        CloseToTray = CloseToTray,
        UseWSLAttach = UseWSLAttach,
        SpecifyWSLDistribution = SpecifyWSLDistribution,
        ForwardNetCard = new string(ForwardNetCard),
        DefaultDistribution = new string(DefaultDistribution)
    };

    public void CopyFrom(ApplicationConfig other)
    {
        DarkMode = other.DarkMode;
        IsChinese = other.IsChinese;
        CloseToTray = other.CloseToTray;
        UseWSLAttach = other.UseWSLAttach;
        SpecifyWSLDistribution = other.SpecifyWSLDistribution;
        ForwardNetCard = other.ForwardNetCard;
        DefaultDistribution = other.DefaultDistribution;
    }
}

public class SystemConfig
{
    public ApplicationConfig AppConfig = new();
    public List<List<USBDevicesInfo>> AutoAttachDevices = [];

    public override bool Equals(object? obj)
    {
        if (obj is SystemConfig other)
        {
            if (!AppConfig.Equals(other.AppConfig))
            {
                return false;
            }

            if (AutoAttachDevices.Count != other.AutoAttachDevices.Count)
            {
                return false;
            }

            for (int i = 0; i < AutoAttachDevices.Count; i++)
            {
                if (!other.AutoAttachDevices[i].Equals(AutoAttachDevices[i]))
                {
                    return false;
                }
            } 

            return true;
        }
        return false;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(AppConfig, AutoAttachDevices);
    }
}
