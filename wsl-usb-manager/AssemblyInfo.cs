/******************************************************************************
* SPDX-License-Identifier: MIT
* Project: wsl-usb-manager
* Class: AssemblyInfo.cs
* NameSpace: %Namespace%
* Author: Chuckie
* copyright: Copyright (c) Chuckie, 2024
* Description:
* Create Date: 2024/10/26 13:27
******************************************************************************/
using System.Reflection;
using System.Runtime.InteropServices;

// 在此类的 SDK 样式项目中，现在，在此文件中早前定义的几个程序集属性将在生成期间自动添加，并使用在项目属性中定义的值进行填充。有关包含的属性以及如何定制此过程的详细信息，请参阅
// https://aka.ms/assembly-info-properties


// 将 ComVisible 设置为 false 会使此程序集中的类型对 COM 组件不可见。如果需要从 COM 访问此程序集中的类型，请将该类型的 ComVisible
// 属性设置为 true。

[assembly: ComVisible(false)]

// 如果此项目向 COM 公开，则下列 GUID 用于 typelib 的 ID。
[assembly: log4net.Config.XmlConfigurator(ConfigFile = "Log4Net.config", Watch = true)]
[assembly: AssemblyCompany("WSL USB Manager")]
[assembly: AssemblyConfiguration("Debug")]
[assembly: AssemblyCopyright("Copyright (c) Chuckie, 2024")]
[assembly: AssemblyProduct("WSL USB Manager")]
[assembly: AssemblyTitle("WSL USB Manager")]
[assembly: System.Runtime.Versioning.TargetPlatformAttribute("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]
[assembly: Guid("9a938903-6944-40f6-8498-45ebc76f768f")]
[assembly: AssemblyVersion("1.2.0.0")]
[assembly: AssemblyFileVersion("1.2.0")]
[assembly: AssemblyInformationalVersion("1.2.0+Branch.master.Sha.17b5741f450dc55d5de53f5a78676cd026219855")]
