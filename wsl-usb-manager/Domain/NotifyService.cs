/******************************************************************************
* SPDX-License-Identifier: MIT
* Project: wsl-usb-manager
* Class: NotifyService.cs
* NameSpace: wsl_usb_manager.Domain
* Author: Chuckie
* copyright: Copyright (c) Chuckie, 2025
* Description:
* Create Date: 2024/12/7 22:41
******************************************************************************/
using log4net;
using System.Web;
using wsl_usb_manager.Resources;
using wsl_usb_manager.USBIPD;

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

    public static void ShowUSBIPDError(ErrorCode errCode, string error, USBDevice? dev)
    {
        string name = "";
        string msg = "";
        
        if (dev != null)
        {
            name = string.IsNullOrWhiteSpace(dev.Description) ? "" : dev.Description.Split(",")[0];
            name = $"{name} ({dev.HardwareId})";
        }
        log.Warn($"USBIPD error: {errCode}, {error}, {name}");
        switch (errCode)
        {
            case ErrorCode.USBIPDNotFound:
            case ErrorCode.USBIPDLowVersion:
                msg = error;
                break;
            case ErrorCode.DeviceBindFailed:
                if (Lang.GetText("ErrMsgBindFail") is string binderr)
                {
                    msg = $"{binderr} {name}: {Environment.NewLine}{error}";
                }
                else
                {
                    msg = $"Failed to bind {name}: {Environment.NewLine}{error}";
                }
                    break;
            case ErrorCode.DeviceAttachFailed:
                if (Lang.GetText("ErrMsgAttachFail") is string atterr)
                {
                    msg = $"{atterr} {name}: {Environment.NewLine}{error}";
                }
                else
                {
                    msg = $"Failed to attach {name}: {Environment.NewLine}{error}";
                }
                break;
            case ErrorCode.DeviceNotConnected:
                if (Lang.GetText("ErrMsgDeviceNotConnected") is string notconnected)
                {
                    msg = $"\"{name}\" {notconnected}";
                }
                else
                {
                    msg = $"The device {name} is not connected.";
                }
                break;
            case ErrorCode.DeviceNotBound:
                if (Lang.GetText("ErrMsgDeviceNotBound") is string notbound)
                {
                    msg = $"\"{name}\" {notbound}";
                }
                else
                {
                    msg = $"The device {name} is not bound.";
                }
                break;
            case ErrorCode.Failure:
            default:
                msg = error;
                break;
        }
        ShowErrorMessage(msg);
    }
}
