using System;

namespace PCLauncher.Core.Models;

public class AppItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string ExePath { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public string WorkingDirectory { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = "Genel";
    public int Order { get; set; } = 0;
    public string? IconBase64 { get; set; }
    public bool IsFavorite { get; set; }
    public int LaunchCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? LastLaunchedAt { get; set; }
}
