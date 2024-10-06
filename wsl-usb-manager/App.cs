/******************************************************************************
* SPDX-License-Identifier: MIT
* Project: wsl-usb-manager
* Class: App.cs
* NameSpace: wsl_usb_manager
* Author: Chuckie
* copyright: Copyright (c) Chuckie, 2024
* Description:
* Create Date: 2024/10/17 20:22
******************************************************************************/

// Ignore Spelling: App Cfg

using log4net;
using Newtonsoft.Json;
using System.IO;
using wsl_usb_manager.Resources;
using wsl_usb_manager.Settings;

namespace wsl_usb_manager;

public partial class App : System.Windows.Application
{
    private static readonly string UserAppRomingPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    private static readonly string ConfigFileDir = Path.Combine(UserAppRomingPath, "WSL USB Manager");
    private static readonly string ConfigFile = ConfigFileDir + "/config.json";
    private static SystemConfig SysConfig = new();
    private static readonly ILog log = LogManager.GetLogger(typeof(App));

    private static void InitConfiguration()
    {
        if (!Directory.Exists(ConfigFileDir))
        {
            Directory.CreateDirectory(ConfigFileDir);
        }
        
        if (File.Exists(ConfigFile))
        {
            string json = File.ReadAllText(ConfigFile);
            var cfg = JsonConvert.DeserializeObject<SystemConfig>(json);
            if (cfg != null) SysConfig = cfg;
        }

        if (SysConfig == null)
        {
            log.Warn("Config file not found or invalid, using default config.");
            SysConfig ??= new SystemConfig();
            SaveConfig();
        }

        bool isChinese;
        if (string.IsNullOrEmpty(SysConfig.AppConfig.Lang))
        {
            var currentCulture = System.Globalization.CultureInfo.CurrentCulture;
            isChinese = currentCulture.TwoLetterISOLanguageName.Equals("zh", StringComparison.OrdinalIgnoreCase);
            SysConfig.AppConfig.Lang = currentCulture.TwoLetterISOLanguageName;
            SaveConfig();
        }
        else
        {
            isChinese = SysConfig.AppConfig.Lang.Equals("zh", StringComparison.OrdinalIgnoreCase);
        }
        Lang.ChangeLanguage(isChinese);
    }

    public static SystemConfig GetSysConfig() => SysConfig;

    public static ApplicationConfig GetAppConfig() => SysConfig.AppConfig;

    public static void SetAppConfig(ApplicationConfig appCfg)
    {
        if (appCfg != null && !appCfg.Equals(SysConfig.AppConfig))
        {
            SysConfig.AppConfig = appCfg;
            SaveConfig();
        }
    }

    public static void SaveConfig()
    {
        log.Info("Saving config file...");
        var json = JsonConvert.SerializeObject(SysConfig, Formatting.Indented);
        File.WriteAllText(ConfigFile, json);
    }
}
