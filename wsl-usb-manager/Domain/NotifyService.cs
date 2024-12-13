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
using System.Web;
using wsl_usb_manager.Controller;
using wsl_usb_manager.Resources;

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

    public static void ShowUSBIPDError(ExitCode exitCode, string error, USBDevice? dev)
    {
        string name = "";
        string msg = "";
        
        if (dev != null)
        {
            name = string.IsNullOrWhiteSpace(dev.Description) ? "" : dev.Description.Split(",")[0];
            name = $"{name} ({dev.HardwareId})";
        }
        log.Warn($"USBIPD error: {exitCode}, {error}, {name}");
        switch (exitCode)
        {
            case ExitCode.NotFound:
                if (Lang.GetText("ErrMsgUSBIPDNotInstalled") is string m1
                    && Lang.GetText("InstallUSBIPDTips") is string m2)
                {
                    msg = $"{m1}{Environment.NewLine}{Environment.NewLine}{m2}{Environment.NewLine}";
                }
                else
                {
                    msg = $"usbipd-win is not installed.{Environment.NewLine}{error}";
                }
                break;
            case ExitCode.LowVersion:
                if (Lang.GetText("ErrMsgUSBIPDVersionLow") is string s1
                    && Lang.GetText("InstallUSBIPDTips") is string s2)
                {
                    msg = $"{s1.Replace("{version}",USBIPD.GetUSBIPDVersion())} {s2}{Environment.NewLine}";
                }
                else
                {
                    msg = $"usbipd-win version ({USBIPD.GetUSBIPDVersion()}) is too low.";
                }
                break;
            case ExitCode.BindError:
                if (Lang.GetText("ErrMsgBindFail") is string binderr)
                {
                    msg = $"{binderr} {name}: {Environment.NewLine}{error}";
                }
                else
                {
                    msg = $"Failed to bind {name}: {Environment.NewLine}{error}";
                }
                    break;
            case ExitCode.AttachError:
                if (Lang.GetText("ErrMsgAttachFail") is string atterr)
                {
                    msg = $"{atterr} {name}: {Environment.NewLine}{error}";
                }
                else
                {
                    msg = $"Failed to attach {name}: {Environment.NewLine}{error}";
                }
                break;
            case ExitCode.Failure:
            default:
                msg = error;
                break;
        }
        ShowErrorMessage(msg);
    }
}
