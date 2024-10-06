/******************************************************************************
* SPDX-License-Identifier: MIT
* Project: wsl-usb-manager
* Class: App.cs
* NameSpace: wsl_usb_manager
* Author: Chuckie
* copyright: Copyright (c) Chuckie, 2024
* Description:
* Create Date: 2024/10/6 14:55
******************************************************************************/

// Ignore Spelling: App

using log4net;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using wsl_usb_manager.Settings;

namespace wsl_usb_manager;

public partial class App : System.Windows.Application
{
    private static readonly string ConfigFilePath = Environment.CurrentDirectory + "/config.json";
    private static SystemConfig SysConfig = new();
    private static readonly ILog log = LogManager.GetLogger(typeof(App));

    private void InitConfiguration()
    {
        if (File.Exists(ConfigFilePath))
        {
            string json = File.ReadAllText(ConfigFilePath);
            var cfg = JsonConvert.DeserializeObject<SystemConfig>(json);
            if (cfg != null) SysConfig = cfg;
        }

        if (SysConfig == null)
        {
            log.Warn("Config file not found or invalid, using default config.");
            SysConfig ??= new SystemConfig();
            SaveConfig();
        }
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
        File.WriteAllText(ConfigFilePath, json);
    }
}
