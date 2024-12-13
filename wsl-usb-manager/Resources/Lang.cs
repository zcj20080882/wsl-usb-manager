/******************************************************************************
* SPDX-License-Identifier: MIT
* Project: wsl-usb-manager
* Class: Lang.cs
* NameSpace: wsl_usb_manager.Resources
* Author: Chuckie
* copyright: Copyright (c) Chuckie, 2024
* Description:
* Create Date: 2024/10/19 11:55
******************************************************************************/
using log4net;

namespace wsl_usb_manager.Resources;

public class Lang
{
    private static readonly string EnglishResource = @"pack://application:,,,/Resources/LangEnglish.xaml";
    private static readonly string ChineseResource = @"pack://application:,,,/Resources/LangChinese.xaml";
    private static readonly ILog log = LogManager.GetLogger(typeof(Lang));

    public static string? GetText(string key)
    {
        if(System.Windows.Application.Current.FindResource(key) is string text)
        {
            return text.Replace("{br}", Environment.NewLine);
        }
        return System.Windows.Application.Current.FindResource(key).ToString();
    }

    public static bool IsChinese()
    {
        return App.GetAppConfig().Lang == "zh";
    }

    public static bool ChangeLanguage(bool isChinese)
    {
        try
        {
            if (isChinese)
            {
                System.Windows.Application.Current.Resources.MergedDictionaries[0].Source = new Uri(ChineseResource);
                App.GetAppConfig().Lang = "zh";
            }
            else
            {
                System.Windows.Application.Current.Resources.MergedDictionaries[0].Source = new Uri(EnglishResource);
                App.GetAppConfig().Lang = "en";
            }

            App.SaveConfig();
        }
        catch (Exception e)
        {
            log.Error(e);
            return false;
        }
        return true;
    }
}
