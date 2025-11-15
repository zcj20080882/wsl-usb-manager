/******************************************************************************
* SPDX-License-Identifier: MIT
* Project: wsl-usb-manager
* Class: NetworkCardInfo.cs
* NameSpace: wsl_usb_manager.Settings
* Author: Chuckie
* copyright: Copyright (c) Chuckie, 2025
* Description:
* Create Date: 2025/3/2 18:08
******************************************************************************/
using System.Net.NetworkInformation;
using wsl_usb_manager.Resources;

namespace wsl_usb_manager.Settings;

internal class NetworkCardInfo
{
    private static bool IsChinese() => Lang.IsChinese();
    public static List<string> GetAllNetworkCardName()
    {
        List<string> netlist = [];
        NetworkInterface[] networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();

        foreach (NetworkInterface ni in networkInterfaces)
        {
            if (ni.OperationalStatus == OperationalStatus.Up && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel &&
                ni.SupportsMulticast)
            {
                IPInterfaceProperties ipProperties = ni.GetIPProperties();
                foreach (UnicastIPAddressInformation ip in ipProperties.UnicastAddresses)
                {
                    if (ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        netlist.Add(ni.Name);
                    }
                }
            }
        }

        return netlist;
    }

    public static (string? IP, string ErrMsg) GetIPAddress(string networkCardName)
    {
        if (string.IsNullOrEmpty(networkCardName))
        {
            
            return (null, IsChinese() ? "网卡名称为空。" : "Network work card name is empty.");
        }
        NetworkInterface[] networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();

        foreach (NetworkInterface ni in networkInterfaces)
        {
            if (ni.Name == networkCardName)
            {
                if (ni.OperationalStatus == OperationalStatus.Up && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel &&
                ni.SupportsMulticast)
                {
                    IPInterfaceProperties ipProperties = ni.GetIPProperties();
                    foreach (UnicastIPAddressInformation ip in ipProperties.UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        {
                            return (ip.Address.ToString(), "");
                        }
                    }
                }
            }
        }

        return (null, IsChinese() ? "网卡不可用。" : "Network work card is not available.");
    }
}
