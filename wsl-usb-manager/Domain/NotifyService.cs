/******************************************************************************
* SPDX-License-Identifier: MIT
* Project: wsl-usb-manager
* Class: NotifyService.cs
* NameSpace: wsl_usb_manager.Domain
* Author: Chuckie
* copyright: Copyright (c) Chuckie, 2024
* Description:
* Create Date: 2024/12/7 22:41
******************************************************************************/
using log4net;

namespace wsl_usb_manager.Domain;

public interface INotifyService
{
    void ShowNotification(string message);
    void ShowErrorMessage(string message);
    void DisableWindow();
    void EnableWindow();
}

public static class NotifyService
{
    private static readonly ILog log = LogManager.GetLogger(typeof(NotifyService));
    private static INotifyService? _notifyService;

    public static void RegisterNotifyService(INotifyService notifyService)
    {
        if(_notifyService != null)
        {
            log.Warn("NotifyService is already registered.");
        }
        _notifyService = notifyService;
    }

    public static void ShowNotification(string message) => _notifyService?.ShowNotification(message);

    public static void ShowErrorMessage(string message) => _notifyService?.ShowErrorMessage(message);
    public static void DisableWindow() => _notifyService?.DisableWindow();
    public static void EnableWindow() => _notifyService?.EnableWindow();
}
