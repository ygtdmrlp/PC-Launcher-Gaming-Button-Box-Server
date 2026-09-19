using System;

namespace PCLauncher.Core.Models;

public class LogEntry
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string Level { get; set; } = "INFO";
    public string Message { get; set; } = string.Empty;

    public string FormattedTime => Timestamp.ToString("HH:mm:ss");
    public string DisplayText => $"[{FormattedTime}] [{Level}] {Message}";
}
