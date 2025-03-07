/******************************************************************************
* SPDX-License-Identifier: MIT
* Project: wsl-usb-manager
* Class: USBEventArgs.cs
* NameSpace: wsl_usb_manager.USBMonitor
* Author: Chuckie
* copyright: Copyright (c) Chuckie, 2025
* Description:
* Create Date: 2025/3/6 20:40
******************************************************************************/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace wsl_usb_manager.USBMonitor;

public class USBEventArgs : EventArgs
{
    public string? Caption { get; set; }
    public string? Name { get; set; }
    public string? HardwareID { get; set; }
    public string? Description { get; set; }
    public string? Manufacturer { get; set; }
    public bool IsConnected { get; set; }

    public USBEventArgs()
    {
    }

    public USBEventArgs(string caption, string name, string hardwareID, string description, string manufacturer, bool isConnected)
    {
        this.Caption = caption;
        this.Name = name;
        this.HardwareID = hardwareID;
        this.Description = description;
        this.Manufacturer = manufacturer;
        this.IsConnected = isConnected;
    }
}

public delegate void USBEventHandler(USBEventArgs e);
