using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace PCLauncher.Core.Services;

public class NetworkAdapterInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public bool IsWireless { get; set; }
    public bool IsPrimary { get; set; }

    public string DisplayName => $"{Name} ({IpAddress})" + (IsWireless ? " [Wi-Fi]" : " [LAN]");
}

public interface INetworkService
{
    List<NetworkAdapterInfo> GetAvailableAdapters();
    string GetPrimaryLanIpAddress();
    string ResolveIpAddress(string? preferredIp);
}

public class NetworkService : INetworkService
{
    public List<NetworkAdapterInfo> GetAvailableAdapters()
    {
        var list = new List<NetworkAdapterInfo>();

        try
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(nic => nic.OperationalStatus == OperationalStatus.Up)
                .Where(nic => nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .Where(nic => !IsVirtualOrExcluded(nic.Description, nic.Name));

            foreach (var nic in interfaces)
            {
                var ipProps = nic.GetIPProperties();
                var ipv4 = ipProps.UnicastAddresses
                    .FirstOrDefault(u => u.Address.AddressFamily == AddressFamily.InterNetwork &&
                                         !IPAddress.IsLoopback(u.Address) &&
                                         !u.Address.ToString().StartsWith("169.254."));

                if (ipv4 != null)
                {
                    var isWifi = nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211;
                    list.Add(new NetworkAdapterInfo
                    {
                        Id = nic.Id,
                        Name = nic.Name,
                        Description = nic.Description,
                        IpAddress = ipv4.Address.ToString(),
                        IsWireless = isWifi
                    });
                }
            }
        }
        catch
        {
            // Fallback
        }

        // If list is empty, fallback to local hostname resolution
        if (list.Count == 0)
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                    {
                        list.Add(new NetworkAdapterInfo
                        {
                            Id = "fallback",
                            Name = "Ethernet / Wi-Fi",
                            Description = "Default Network Adapter",
                            IpAddress = ip.ToString(),
                            IsWireless = false
                        });
                        break;
                    }
                }
            }
            catch
            {
                list.Add(new NetworkAdapterInfo
                {
                    Id = "localhost",
                    Name = "Yerel Bağlantı",
                    Description = "Localhost",
                    IpAddress = "127.0.0.1",
                    IsWireless = false
                });
            }
        }

        // Mark primary
        var primary = list.FirstOrDefault(a => a.IsWireless) 
                      ?? list.FirstOrDefault(a => a.IpAddress.StartsWith("192.168."))
                      ?? list.FirstOrDefault(a => a.IpAddress.StartsWith("10."))
                      ?? list.FirstOrDefault();

        if (primary != null)
        {
            primary.IsPrimary = true;
        }

        return list;
    }

    public string GetPrimaryLanIpAddress()
    {
        var adapters = GetAvailableAdapters();
        var primary = adapters.FirstOrDefault(a => a.IsPrimary) ?? adapters.FirstOrDefault();
        return primary?.IpAddress ?? "127.0.0.1";
    }

    public string ResolveIpAddress(string? preferredIp)
    {
        if (string.IsNullOrWhiteSpace(preferredIp))
            return GetPrimaryLanIpAddress();

        var adapters = GetAvailableAdapters();
        if (adapters.Any(a => a.IpAddress == preferredIp))
            return preferredIp;

        return GetPrimaryLanIpAddress();
    }

    private static bool IsVirtualOrExcluded(string desc, string name)
    {
        var d = (desc + " " + name).ToLowerInvariant();
        return d.Contains("virtual") ||
               d.Contains("vbox") ||
               d.Contains("vmware") ||
               d.Contains("hyper-v") ||
               d.Contains("vethernet") ||
               d.Contains("wsl") ||
               d.Contains("docker") ||
               d.Contains("tap-windows") ||
               d.Contains("zerotier") ||
               d.Contains("tailscale") ||
               d.Contains("nordlynx") ||
               d.Contains("wireguard");
    }
}
