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

namespace wsl_usb_manager.Settings;

internal class NetworkCardInfo
{
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
            return (null, "Network work card name is empty.");
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

        return (null, "Network work card is not available.");
    }
}
