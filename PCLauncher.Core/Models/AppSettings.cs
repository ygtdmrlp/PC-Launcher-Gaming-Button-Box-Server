namespace PCLauncher.Core.Models;

public class AppSettings
{
    public int Port { get; set; } = 5000;
    public bool AutoStartServer { get; set; } = true;
    public bool StartWithWindows { get; set; } = false;
    public bool MinimizeToTray { get; set; } = true;
    public bool UsePin { get; set; } = false;
    public string PinCode { get; set; } = string.Empty;
    public string? SelectedIpAddress { get; set; }
    public string Theme { get; set; } = "Dark";
    public bool ShowWelcomeOnStartup { get; set; } = true;
}
