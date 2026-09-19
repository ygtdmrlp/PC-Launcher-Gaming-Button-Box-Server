using System;

namespace PCLauncher.Core.Models;

public class KeyActionItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = string.Empty;
    public string KeySequence { get; set; } = string.Empty; // e.g. "L", "Space", "Ctrl+Shift+S", "F12"
    public string GameOrCategory { get; set; } = "Genel";
    public string ColorHex { get; set; } = "#3B82F6"; // Default accent blue
    public string? PresetIcon { get; set; } = "🎮";
    public string? IconBase64 { get; set; }
    public int HoldDurationMs { get; set; } = 100; // Default 100ms for DirectX / DirectInput games (ETS2)
    public int Order { get; set; } = 0;
    public int PressCount { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? LastPressedAt { get; set; }
}
