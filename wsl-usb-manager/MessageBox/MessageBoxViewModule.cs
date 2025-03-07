/******************************************************************************
* SPDX-License-Identifier: MIT
* Project: wsl-usb-manager
* Class: MessageBoxViewModule.cs
* NameSpace: wsl_usb_manager.MessageBox
* Author: Chuckie
* copyright: Copyright (c) Chuckie, 2025
* Description:
* Create Date: 2024/12/11 18:43
******************************************************************************/
using MaterialDesignThemes.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using wsl_usb_manager.Domain;

namespace wsl_usb_manager.MessageBox;

public class MessageBoxViewModule : ViewModelBase
{
    private string? _message;
    private string? _caption;
    private PackIconKind? _icon;


    public string? Message
    {
        get => _message;
        set => SetProperty(ref _message, value);
    }   

    public string? Caption
    {
        get => _caption;
        set => SetProperty(ref _caption, value);
    }

    public PackIconKind? Icon
    {
        get => _icon;
        set => SetProperty(ref _icon, value);
    }

    public MessageBoxViewModule(MessageType type,string caption, string message)
    {
        Caption = caption;
        message = message.Replace("\r\n", "\n");
        message = message.Replace("\n", Environment.NewLine);
        Message = message;
        Icon = type switch
        {
            MessageType.Error => (PackIconKind?)PackIconKind.Error,
            MessageType.Warn => (PackIconKind?)PackIconKind.Warning,
            _ => (PackIconKind?)PackIconKind.Information,
        };
    }
}
