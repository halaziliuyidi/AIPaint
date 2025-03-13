
using System.Net.Sockets;
using System.Net;
using System;

namespace MFramework
{
    public static class GetIp
    {
        public static IPAddress GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());

            try
            {
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork)
                    {
                        
                        return ip;
                    }
                }
            }
            catch (Exception error)
            {
                DebugHelper.LogError(error.Message);
            }
            
            return IPAddress.None;
        }

        public static IPAddress GetBroadcastAddress(this IPAddress address)
        {
            IPAddress subnetMask = address.GetSubnetMask();
            byte[] ipAdressBytes = address.GetAddressBytes();
            byte[] subnetMaskBytes = subnetMask.GetAddressBytes();

            byte[] broadcastAddress = new byte[ipAdressBytes.Length];
            for (int i = 0; i < broadcastAddress.Length; i++)
            {
                broadcastAddress[i] = (byte)(ipAdressBytes[i] | (subnetMaskBytes[i] ^ 255));
            }
            return new IPAddress(broadcastAddress);
        }

        public static IPAddress GetSubnetMask(this IPAddress address)
        {
            uint firstOctet = GetFirtsOctet(address);
            string subnetMask = "0.0.0.0";
            if (firstOctet >= 0 && firstOctet <= 127)
            {
                subnetMask = "255.0.0.0";
            }
            else if (firstOctet >= 128 && firstOctet <= 191)
            {
                subnetMask = "255.255.0.0";
            }
            else if (firstOctet >= 192 && firstOctet <= 223)
            {
                subnetMask = "255.255.255.0";
            }
            return IPAddress.Parse(subnetMask);
        }

        private static uint GetFirtsOctet(IPAddress iPAddress)
        {
            byte[] byteIP = iPAddress.GetAddressBytes();
            uint ipInUint = (uint)byteIP[0];
            return ipInUint;
        }
    }
}
