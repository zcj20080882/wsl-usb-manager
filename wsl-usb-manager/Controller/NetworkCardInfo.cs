/******************************************************************************
* SPDX-License-Identifier: MIT
* Project: wsl-usb-manager
* Class: NetworkCardInfo.cs
* NameSpace: wsl_usb_manager.Controller
* Author: Chuckie
* copyright: Copyright (c) Chuckie, 2024
* Description:
* Create Date: 2024/10/5 17:07
******************************************************************************/
using System.Net.NetworkInformation;

namespace wsl_usb_manager.Controller;

public class NetworkCardInfo
{
    public static List<string>? GetAllNetworkCardName()
    {
        List<string> netlist = [];
        NetworkInterface[] networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();

        foreach (NetworkInterface ni in networkInterfaces)
        {
            if (ni.OperationalStatus == OperationalStatus.Up)
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

    public static string? GetIPAddress(string networkCardName)
    {
        NetworkInterface[] networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();

        foreach (NetworkInterface ni in networkInterfaces)
        {
            if (ni.Name == networkCardName)
            {
                IPInterfaceProperties ipProperties = ni.GetIPProperties();
                foreach (UnicastIPAddressInformation ip in ipProperties.UnicastAddresses)
                {
                    if (ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        return ip.Address.ToString();
                    }
                }
            }
        }

        return null; 
    }
}
