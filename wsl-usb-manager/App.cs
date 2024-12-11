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
using log4net.Layout.Pattern;
using log4net.Repository.Hierarchy;
using Newtonsoft.Json;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using wsl_usb_manager.Resources;
using wsl_usb_manager.Settings;
namespace wsl_usb_manager;

public partial class App : System.Windows.Application
{
    private static readonly Mutex mutex = new(true, "WSL-USB-Manager-1ddc73e5-c499-484f-b663-87edaee7bfdc");
    private static readonly string AppTempPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
            "WSL USB Manager");
    private static readonly string ConfigFile = AppTempPath + "/config.json";
    private static SystemConfig SysConfig = new();
    private static readonly ILog log = LogManager.GetLogger(typeof(App));
    private static readonly string LogConversionPattern = "%date [%thread] %-5level %logger:%line - %message%newline";
    private const string LogDivider = "\r\n-----------------------------------------------------------------------------------------------------------------------------------------";

    protected override void OnStartup(StartupEventArgs e)
    {
        if (!mutex.WaitOne(TimeSpan.Zero, true))
        {
            System.Windows.MessageBox.Show("Another instance of the app is already running,\r\ncheck system tray icon to restore instance.");
            Shutdown();
            return;
        }
        Initialize(e.Args);
        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        mutex.ReleaseMutex();
        base.OnExit(e);
    }

    private static void ConfigureLog4Net()
    {
        string logDirectory = Path.Combine(AppTempPath, "Logs/");
        if (!Directory.Exists(logDirectory))
        {
            Directory.CreateDirectory(logDirectory);
        }

        // Get logger repository
        Hierarchy hierarchy = (Hierarchy)LogManager.GetRepository();

        // Create PatternLayout
        PatternLayout patternLayout = new()
        {
            ConversionPattern = LogConversionPattern
        };
        patternLayout.ActivateOptions();
        
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

        hierarchy.Root.AddAppender(rollingFileAppender);

#if DEBUG
        // Create DebugAppender
        DebugAppender debugAppender = new DebugAppender
        {
            Layout = patternLayout
        };
        debugAppender.ActivateOptions();

        // Add DebugAppender to Root
        hierarchy.Root.AddAppender(debugAppender);
#endif

        // Set log level
        hierarchy.Root.Level = Level.Debug;

        // Apply config
        hierarchy.Configured = true;
        log.Info(LogDivider);
        log.Info("Starting WSL USB Manager");
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

    private void Initialize(string[]? Args)
    {
        ConfigureLog4Net();
        InitConfiguration();
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
    static void CopyDirectory(string sourceDir, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);

        foreach (string file in Directory.GetFiles(sourceDir))
        {
            string destFile = Path.Combine(destinationDir, Path.GetFileName(file));
            File.Copy(file, destFile, true);
        }

        foreach (string dir in Directory.GetDirectories(sourceDir))
        {
            string destDir = Path.Combine(destinationDir, Path.GetFileName(dir));
            CopyDirectory(dir, destDir);
        }
    }

}
