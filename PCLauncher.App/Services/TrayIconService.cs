using System;
using System.Drawing;
using System.Windows.Forms;
using PCLauncher.Core.Models;
using PCLauncher.Server;

namespace PCLauncher.App.Services;

public class TrayIconService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ILauncherServer _server;
    private readonly Action _showMainWindow;
    private readonly Action _openSettings;
    private readonly Action _exitApplication;

    public TrayIconService(
        ILauncherServer server,
        Action showMainWindow,
        Action openSettings,
        Action exitApplication)
    {
        _server = server;
        _showMainWindow = showMainWindow;
        _openSettings = openSettings;
        _exitApplication = exitApplication;

        _notifyIcon = new NotifyIcon
        {
            Text = "PC Launcher",
            Visible = true,
            Icon = CreateTrayIcon(false)
        };

        _notifyIcon.DoubleClick += (s, e) => _showMainWindow();

        BuildContextMenu();

        _server.StatusChanged += OnServerStatusChanged;
    }

    private void OnServerStatusChanged(ServerStatus status)
    {
        _notifyIcon.Icon = CreateTrayIcon(status.IsRunning);

        var tooltip = status.IsRunning
            ? $"PC Launcher\n● Server Online\n{status.IpAddress}:{status.Port}"
            : "PC Launcher\n○ Server Offline";

        if (tooltip.Length >= 64)
        {
            tooltip = tooltip.Substring(0, 63);
        }

        _notifyIcon.Text = tooltip;
        BuildContextMenu();
    }

    private void BuildContextMenu()
    {
        var menu = new ContextMenuStrip();

        var titleItem = new ToolStripMenuItem("PC Launcher Server")
        {
            Enabled = false,
            Font = new Font(FontFamily.GenericSansSerif, 9, FontStyle.Bold)
        };
        menu.Items.Add(titleItem);
        menu.Items.Add(new ToolStripSeparator());

        var dashboardItem = new ToolStripMenuItem("📊 Dashboard", null, (s, e) => _showMainWindow());
        menu.Items.Add(dashboardItem);

        if (_server.IsRunning)
        {
            var stopItem = new ToolStripMenuItem("⏹ Server'ı Durdur", null, async (s, e) =>
            {
                await _server.StopAsync();
            });
            menu.Items.Add(stopItem);

            var openWebItem = new ToolStripMenuItem("🌐 Telefon Arayüzünü Aç", null, (s, e) =>
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = _server.Status.Url,
                        UseShellExecute = true
                    });
                }
                catch { }
            });
            menu.Items.Add(openWebItem);
        }
        else
        {
            var startItem = new ToolStripMenuItem("▶ Server'ı Başlat", null, async (s, e) =>
            {
                await _server.StartAsync(_server.Status.Port, _server.Status.IpAddress);
            });
            menu.Items.Add(startItem);
        }

        var settingsItem = new ToolStripMenuItem("⚙️ Ayarlar", null, (s, e) => _openSettings());
        menu.Items.Add(settingsItem);

        menu.Items.Add(new ToolStripSeparator());

        var exitItem = new ToolStripMenuItem("❌ Çıkış", null, (s, e) => _exitApplication());
        menu.Items.Add(exitItem);

        _notifyIcon.ContextMenuStrip = menu;
    }

    public void ShowNotification(string title, string message, ToolTipIcon icon = ToolTipIcon.Info)
    {
        _notifyIcon.ShowBalloonTip(3000, title, message, icon);
    }

    private static Icon CreateTrayIcon(bool isOnline)
    {
        try
        {
            using var bmp = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(System.Drawing.Color.Transparent);

                // Base computer monitor outline
                using var borderPen = new Pen(System.Drawing.Color.White, 1.5f);
                g.DrawRectangle(borderPen, 1, 1, 13, 10);

                // Stand
                g.DrawLine(borderPen, 7, 11, 7, 14);
                g.DrawLine(borderPen, 4, 14, 11, 14);

                // Status indicator dot
                var dotColor = isOnline 
                    ? System.Drawing.Color.FromArgb(16, 185, 129) 
                    : System.Drawing.Color.FromArgb(239, 68, 68);
                using var dotBrush = new SolidBrush(dotColor);
                g.FillEllipse(dotBrush, 9, 7, 6, 6);
            }

            var hIcon = bmp.GetHicon();
            return Icon.FromHandle(hIcon);
        }
        catch
        {
            return SystemIcons.Application;
        }
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
