using System;

namespace PCLauncher.Core.Models;

public class ServerStatus
{
    public bool IsRunning { get; set; }
    public string IpAddress { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 5000;
    public string Url => $"http://{IpAddress}:{Port}";
    public int ConnectedClients { get; set; }
    public DateTime? StartTime { get; set; }
    public string? ErrorMessage { get; set; }

    public TimeSpan? Uptime => IsRunning && StartTime.HasValue 
        ? DateTime.Now - StartTime.Value 
        : null;
}
