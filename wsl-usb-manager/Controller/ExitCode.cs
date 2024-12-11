/******************************************************************************
* SPDX-License-Identifier: MIT
* Project: wsl-usb-manager
* Class: ExitCode.cs
* NameSpace: wsl_usb_manager.Controller
* Author: Chuckie
* copyright: Copyright (c) Chuckie, 2024
* Description:
* Create Date: 2024/10/17 20:28
******************************************************************************/
namespace wsl_usb_manager.Controller;

public enum ExitCode
{
    Success = 0,
    Failure = 1,
    ParseError = 2,
    AccessDenied = 3,
    Timeout = 4,
    NotFound = 5,
    LowVersion = 6,
};