/******************************************************************************
* SPDX-License-Identifier: MIT
* Project: wsl-usb-manager
* Class: SystemConfig.cs
* NameSpace: wsl_usb_manager.Settings
* Author: Chuckie
* copyright: Copyright (c) Chuckie, 2025
* Description:
* Create Date: 2024/10/17 20:22
******************************************************************************/

// Ignore Spelling: App

using wsl_usb_manager.USBIPD;

namespace wsl_usb_manager.Settings;


public class ApplicationConfig
{
    public bool DarkMode = false;
    public string Lang = "";
    public bool CloseToTray = true;
    public bool SpecifyNetCard = false;
    public string ForwardNetCard = "";

    public override bool Equals(object? obj)
    {
        if (obj is ApplicationConfig other)
        {
            return (this.DarkMode == other.DarkMode && this.Lang == other.Lang
                    && this.CloseToTray == other.CloseToTray && this.SpecifyNetCard == other.SpecifyNetCard
                    && this.ForwardNetCard == other.ForwardNetCard);
        }
        return false;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(DarkMode, Lang, CloseToTray, SpecifyNetCard, ForwardNetCard);
    }

    public ApplicationConfig Clone() => new()
    {
        DarkMode = DarkMode,
        Lang = Lang,
        CloseToTray = CloseToTray,
        SpecifyNetCard = SpecifyNetCard,
        ForwardNetCard = new string(ForwardNetCard),
    };

    public void CopyFrom(ApplicationConfig other)
    {
        DarkMode = other.DarkMode;
        Lang = other.Lang;
        CloseToTray = other.CloseToTray;
        SpecifyNetCard = other.SpecifyNetCard;
        ForwardNetCard = other.ForwardNetCard;
    }
}

public class SystemConfig
{
    public ApplicationConfig AppConfig = new();
    public List<USBDevice> AutoAttachDeviceList = [];
    public List<USBDevice> FilteredDeviceList = [];

    public bool IsInAutoAttachDeviceList(string? HardwareId)
    {
        if (AutoAttachDeviceList.Count == 0 || string.IsNullOrEmpty(HardwareId)) { return false; }

        return AutoAttachDeviceList.Any(d => d.HardwareId.Equals(HardwareId, StringComparison.OrdinalIgnoreCase));
    }

    public bool IsInAutoAttachDeviceList(USBDevice? dev) => IsInAutoAttachDeviceList(dev?.HardwareId);

    public bool IsInFilterDeviceList(string? HardwareId)
    {
        if (FilteredDeviceList.Count == 0 || string.IsNullOrEmpty(HardwareId)) { return false; }

        return FilteredDeviceList.Any(d => d.HardwareId.Equals(HardwareId, StringComparison.OrdinalIgnoreCase));
    }

    public bool IsInFilterDeviceList(USBDevice dev) => IsInFilterDeviceList(dev?.HardwareId);

    public void AddToAutoAttachDeviceList(USBDevice dev)
    {
        if (!IsInAutoAttachDeviceList(dev))
        {
            AutoAttachDeviceList.Add(dev);
        }
    }

    public void RemoveFromAutoAttachDeviceList(USBDevice dev)
    {
        AutoAttachDeviceList.RemoveAll(d => d.HardwareId == dev.HardwareId);
    }

    public void AddToFilteredDeviceList(USBDevice dev)
    {
        if (!IsInFilterDeviceList(dev))
        {
            FilteredDeviceList.Add(dev);
        }
    }

    public void RemoveFromFilteredDevice(USBDevice dev)
    {
        FilteredDeviceList.RemoveAll(d => d.HardwareId == dev.HardwareId);
    }
}
