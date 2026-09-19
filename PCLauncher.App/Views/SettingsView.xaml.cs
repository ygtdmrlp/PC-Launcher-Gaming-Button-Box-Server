using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using PCLauncher.Core.Models;
using PCLauncher.Core.Services;
using PCLauncher.Server;

namespace PCLauncher.App.Views;

public partial class SettingsView : UserControl
{
    private readonly IStorageService _storageService;
    private readonly ILauncherServer _server;
    private readonly IFirewallService _firewallService;
    private readonly IStartupService _startupService;
    private readonly ILoggerService _loggerService;

    private AppSettings _settings = new();

    public event Action? SettingsSaved;

    public SettingsView(
        IStorageService storageService,
        ILauncherServer server,
        IFirewallService firewallService,
        IStartupService startupService,
        ILoggerService loggerService)
    {
        InitializeComponent();

        _storageService = storageService;
        _server = server;
        _firewallService = firewallService;
        _startupService = startupService;
        _loggerService = loggerService;

        Loaded += async (s, e) => await LoadSettingsAsync();
    }

    public async Task LoadSettingsAsync()
    {
        _settings = await _storageService.LoadSettingsAsync();

        PortTextBox.Text = _settings.Port.ToString();
        AutoStartServerCheckBox.IsChecked = _settings.AutoStartServer;
        MinimizeToTrayCheckBox.IsChecked = _settings.MinimizeToTray;
        UsePinCheckBox.IsChecked = _settings.UsePin;
        PinCodeTextBox.Text = _settings.PinCode;

        PinInputRow.Visibility = _settings.UsePin ? Visibility.Visible : Visibility.Collapsed;

        // Startup registry check
        StartWithWindowsCheckBox.IsChecked = _startupService.IsStartWithWindowsEnabled();

        // Firewall status check
        CheckFirewallStatus();
    }

    private void CheckFirewallStatus()
    {
        var port = int.TryParse(PortTextBox.Text, out var p) ? p : 5000;
        var hasRule = _firewallService.IsFirewallRuleConfigured(port);

        if (hasRule)
        {
            FirewallStatusText.Text = "✅ Güvenlik duvarı kuralı tanımlı (5000/TCP).";
            FirewallStatusText.Foreground = (System.Windows.Media.Brush)FindResource("AccentGreenBrush");
        }
        else
        {
            FirewallStatusText.Text = "⚠️ Güvenlik duvarı kuralı bulunamadı. Telefon erişimi engellenebilir.";
            FirewallStatusText.Foreground = (System.Windows.Media.Brush)FindResource("AccentYellowBrush");
        }
    }

    private void UsePinCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        PinInputRow.Visibility = (UsePinCheckBox.IsChecked == true) 
            ? Visibility.Visible 
            : Visibility.Collapsed;
    }

    private void FirewallBtn_Click(object sender, RoutedEventArgs e)
    {
        var port = int.TryParse(PortTextBox.Text, out var p) ? p : 5000;
        var result = _firewallService.AddFirewallRule(port);

        if (result)
        {
            CheckFirewallStatus();
            MessageBox.Show("Windows Firewall kuralı başarıyla eklendi!", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            MessageBox.Show("Firewall kuralı eklenemedi veya yönetici yetkisi verilmedi.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OpenDataFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dir = _storageService.DataDirectory;
            if (Directory.Exists(dir))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = dir,
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Klasör açılamadı: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(PortTextBox.Text.Trim(), out var port) || port < 1024 || port > 65535)
        {
            MessageBox.Show("Lütfen geçerli bir port numarası girin (1024 - 65535 arası).", "Geçersiz Port", MessageBoxButton.OK, MessageBoxImage.Warning);
            PortTextBox.Focus();
            return;
        }

        var usePin = UsePinCheckBox.IsChecked == true;
        var pinCode = PinCodeTextBox.Text.Trim();

        if (usePin && string.IsNullOrWhiteSpace(pinCode))
        {
            MessageBox.Show("PIN koruması açıkken PIN kodu boş bırakılamaz.", "Geçersiz PIN", MessageBoxButton.OK, MessageBoxImage.Warning);
            PinCodeTextBox.Focus();
            return;
        }

        var oldPort = _settings.Port;
        _settings.Port = port;
        _settings.AutoStartServer = AutoStartServerCheckBox.IsChecked == true;
        _settings.MinimizeToTray = MinimizeToTrayCheckBox.IsChecked == true;
        _settings.UsePin = usePin;
        _settings.PinCode = pinCode;

        // Startup registry
        var startWithWin = StartWithWindowsCheckBox.IsChecked == true;
        _settings.StartWithWindows = startWithWin;
        _startupService.SetStartWithWindows(startWithWin);

        await _storageService.SaveSettingsAsync(_settings);
        _loggerService.LogInfo("Ayarlar kaydedildi.");

        // If port changed and server is running, restart server
        if (oldPort != port && _server.IsRunning)
        {
            var restart = MessageBox.Show(
                $"Port numarası {oldPort} -> {port} olarak değiştirildi.\nWeb sunucusu yeni port ile yeniden başlatılsın mı?",
                "Sunucu Yeniden Başlatma",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (restart == MessageBoxResult.Yes)
            {
                await _server.StopAsync();
                await _server.StartAsync(port, _server.Status.IpAddress);
            }
        }

        CheckFirewallStatus();
        SettingsSaved?.Invoke();

        MessageBox.Show("Ayarlar başarıyla kaydedildi.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
