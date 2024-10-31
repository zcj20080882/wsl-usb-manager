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
using log4net.Appender;
using log4net.Core;
using log4net.Layout;
using log4net.Repository.Hierarchy;
using Newtonsoft.Json;
using System.IO;
using wsl_usb_manager.Resources;
using wsl_usb_manager.Settings;

namespace wsl_usb_manager;

public partial class App : System.Windows.Application
{
    private static readonly string AppTempPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
            "WSL USB Manager");
    private static readonly string ConfigFile = AppTempPath + "/config.json";
    private static SystemConfig SysConfig = new();
    private static readonly ILog log = LogManager.GetLogger(typeof(App));
    private static readonly string LogConversionPattern = "%date [%thread]" +"" +
        " %-5level %logger %method (%file:%line) - %message%newline";

    private static void ConfigureLog4Net()
    {
        string logDirectory = Path.Combine(AppTempPath, "Logs/");
        if (!Directory.Exists(logDirectory))
        {
            Directory.CreateDirectory(logDirectory);
        }

        // 获取日志仓库
        Hierarchy hierarchy = (Hierarchy)LogManager.GetRepository();

        // 创建 PatternLayout
        PatternLayout patternLayout = new()
        {
            ConversionPattern = LogConversionPattern
        };
        patternLayout.ActivateOptions();

        // 创建 RollingFileAppender
        RollingFileAppender rollingFileAppender = new()
        {
            File = logDirectory,
            AppendToFile = true,
            RollingStyle = RollingFileAppender.RollingMode.Date,
            StaticLogFileName = false,
            Layout = patternLayout,
            DatePattern = "yyyyMMdd'.log'"
        };
        rollingFileAppender.ActivateOptions();

        // 将 Appender 添加到 Root
        hierarchy.Root.AddAppender(rollingFileAppender);

#if DEBUG
        // 创建 DebugAppender
        DebugAppender debugAppender = new DebugAppender
        {
            Layout = patternLayout
        };
        debugAppender.ActivateOptions();

        // 将 DebugAppender 添加到 Root
        hierarchy.Root.AddAppender(debugAppender);
#endif

        // 设置日志级别
        hierarchy.Root.Level = Level.Debug;

        // 应用配置
        hierarchy.Configured = true;
        log.Info("Start debug level log server...");
    }


    private static void InitConfiguration()
    {
        if (!Directory.Exists(AppTempPath))
        {
            Directory.CreateDirectory(AppTempPath);
        }

        if (File.Exists(ConfigFile))
        {
            string json = File.ReadAllText(ConfigFile);
            var cfg = JsonConvert.DeserializeObject<SystemConfig>(json);
            if (cfg != null) SysConfig = cfg;
        }

        if (SysConfig == null)
        {
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
        log.Info("Application started.");
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
        var json = JsonConvert.SerializeObject(SysConfig, Formatting.Indented);
        File.WriteAllText(ConfigFile, json);
    }
}
