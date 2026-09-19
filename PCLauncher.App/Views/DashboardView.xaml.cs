using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PCLauncher.Core.Models;
using PCLauncher.Core.Services;
using PCLauncher.Server;

namespace PCLauncher.App.Views;

public partial class DashboardView : UserControl
{
    private readonly ILauncherServer _server;
    private readonly INetworkService _networkService;
    private readonly IQrCodeService _qrCodeService;
    private readonly IStorageService _storageService;
    private readonly ILoggerService _loggerService;
    private readonly IFirewallService _firewallService;
    private readonly Action _navigateToApps;

    private readonly ObservableCollection<string> _logItems = new();
    private bool _isInitializing = true;

    public DashboardView(
        ILauncherServer server,
        INetworkService networkService,
        IQrCodeService qrCodeService,
        IStorageService storageService,
        ILoggerService loggerService,
        IFirewallService firewallService,
        Action navigateToApps)
    {
        InitializeComponent();

        _server = server;
        _networkService = networkService;
        _qrCodeService = qrCodeService;
        _storageService = storageService;
        _loggerService = loggerService;
        _firewallService = firewallService;
        _navigateToApps = navigateToApps;

        LogsListBox.ItemsSource = _logItems;

        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!_isInitializing)
        {
            await RefreshDataAsync();
            return;
        }

        // Initialize logs
        foreach (var log in _loggerService.GetRecentLogs())
        {
            _logItems.Add(log.DisplayText);
        }
        UpdateLogsCount();
        ScrollLogsToBottom();

        _loggerService.LogAdded += OnLogAdded;
        _server.StatusChanged += OnServerStatusChanged;

        // Populate Network Adapters
        PopulateNetworkAdapters();

        // Check Firewall
        CheckFirewallRule();

        // Refresh Data
        await RefreshDataAsync();

        _isInitializing = false;
    }

    public async System.Threading.Tasks.Task RefreshDataAsync()
    {
        var settings = await _storageService.LoadSettingsAsync();
        var apps = await _storageService.LoadAppsAsync();

        PortText.Text = settings.Port.ToString();
        AppsCountText.Text = apps.Count.ToString();
        PinStatusText.Text = settings.UsePin ? "Korumalı" : "Açık";
        PinStatusText.Foreground = settings.UsePin 
            ? new SolidColorBrush(Color.FromRgb(245, 158, 11)) 
            : new SolidColorBrush(Color.FromRgb(156, 163, 175));

        WelcomeBanner.Visibility = (apps.Count == 0 && settings.ShowWelcomeOnStartup) 
            ? Visibility.Visible 
            : Visibility.Collapsed;

        UpdateServerStatusUi(_server.Status);
    }

    private void PopulateNetworkAdapters()
    {
        var adapters = _networkService.GetAvailableAdapters();
        IpComboBox.Items.Clear();

        int selectedIndex = 0;
        for (int i = 0; i < adapters.Count; i++)
        {
            var item = adapters[i];
            IpComboBox.Items.Add(item.DisplayName);
            if (item.IsPrimary)
            {
                selectedIndex = i;
            }
        }

        if (IpComboBox.Items.Count > 0)
        {
            IpComboBox.SelectedIndex = selectedIndex;
        }
    }

    private void CheckFirewallRule()
    {
        try
        {
            var port = _server.Status.Port > 0 ? _server.Status.Port : 5000;
            var isConfigured = _firewallService.IsFirewallRuleConfigured(port);
            FirewallBanner.Visibility = isConfigured ? Visibility.Collapsed : Visibility.Visible;
        }
        catch
        {
            FirewallBanner.Visibility = Visibility.Collapsed;
        }
    }

    private void OnServerStatusChanged(ServerStatus status)
    {
        Dispatcher.Invoke(() =>
        {
            UpdateServerStatusUi(status);
        });
    }

    private void UpdateServerStatusUi(ServerStatus status)
    {
        ConnectedClientsText.Text = status.ConnectedClients.ToString();

        if (status.IsRunning)
        {
            StatusDot.Fill = new SolidColorBrush(Color.FromRgb(16, 185, 129));
            StatusTitleText.Text = "Server Online";
            ServerToggleBtn.Content = "⏹ Server'ı Durdur";
            ServerToggleBtn.Style = (Style)FindResource("DangerButton");

            UrlText.Text = status.Url;
            UpdateQrCode(status.Url);
        }
        else
        {
            StatusDot.Fill = new SolidColorBrush(Color.FromRgb(239, 68, 68));
            StatusTitleText.Text = "Server Offline";
            ServerToggleBtn.Content = "▶ Server'ı Başlat";
            ServerToggleBtn.Style = (Style)FindResource("SuccessButton");

            var ip = GetSelectedIp();
            var port = PortText.Text;
            var url = $"http://{ip}:{port}";
            UrlText.Text = url;
            UpdateQrCode(url);
        }
    }

    private void UpdateQrCode(string url)
    {
        try
        {
            var bytes = _qrCodeService.GenerateQrCodePng(url, 10);
            if (bytes.Length == 0) return;

            using var ms = new MemoryStream(bytes);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = ms;
            bitmap.EndInit();
            bitmap.Freeze();

            QrCodeImage.Source = bitmap;
        }
        catch (Exception ex)
        {
            _loggerService.LogError("QR kod oluşturulamadı", ex);
        }
    }

    private string GetSelectedIp()
    {
        if (IpComboBox.SelectedIndex >= 0)
        {
            var adapters = _networkService.GetAvailableAdapters();
            if (IpComboBox.SelectedIndex < adapters.Count)
            {
                return adapters[IpComboBox.SelectedIndex].IpAddress;
            }
        }
        return _networkService.GetPrimaryLanIpAddress();
    }

    private async void ServerToggle_Click(object sender, RoutedEventArgs e)
    {
        ServerToggleBtn.IsEnabled = false;
        try
        {
            if (_server.IsRunning)
            {
                await _server.StopAsync();
            }
            else
            {
                int port = int.TryParse(PortText.Text, out var p) ? p : 5000;
                var ip = GetSelectedIp();
                await _server.StartAsync(port, ip);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Server işlemi başarısız: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            ServerToggleBtn.IsEnabled = true;
        }
    }

    private async void IpComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;

        var selectedIp = GetSelectedIp();
        var settings = await _storageService.LoadSettingsAsync();
        settings.SelectedIpAddress = selectedIp;
        await _storageService.SaveSettingsAsync(settings);

        if (_server.IsRunning)
        {
            var result = MessageBox.Show(
                "Ağ arayüzü değiştirildi. Web sunucusunun yeni IP adresine bağlanması için yeniden başlatılsın mı?",
                "Sunucu Yeniden Başlatma",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                await _server.StopAsync();
                await _server.StartAsync(settings.Port, selectedIp);
            }
        }
        else
        {
            var port = settings.Port;
            var url = $"http://{selectedIp}:{port}";
            UrlText.Text = url;
            UpdateQrCode(url);
        }
    }

    private void CopyUrl_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(UrlText.Text);
            CopyUrlBtn.Content = "✅ Kopyalandı!";
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            timer.Tick += (s, args) =>
            {
                CopyUrlBtn.Content = "📋 Kopyala";
                timer.Stop();
            };
            timer.Start();
        }
        catch { }
    }

    private void OpenBrowser_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = UrlText.Text,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Tarayıcı açılamadı: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void AllowFirewall_Click(object sender, RoutedEventArgs e)
    {
        int port = int.TryParse(PortText.Text, out var p) ? p : 5000;
        var success = _firewallService.AddFirewallRule(port);
        if (success)
        {
            FirewallBanner.Visibility = Visibility.Collapsed;
            MessageBox.Show("Windows Firewall kuralı başarıyla eklendi!", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            MessageBox.Show("Firewall kuralı eklenemedi veya yönetici yetkisi reddedildi.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void AddFirstApp_Click(object sender, RoutedEventArgs e)
    {
        _navigateToApps();
    }

    private void OnLogAdded(LogEntry entry)
    {
        Dispatcher.Invoke(() =>
        {
            _logItems.Add(entry.DisplayText);
            while (_logItems.Count > 200)
            {
                _logItems.RemoveAt(0);
            }
            UpdateLogsCount();
            ScrollLogsToBottom();
        });
    }

    private void ScrollLogsToBottom()
    {
        if (_logItems.Count > 0)
        {
            LogsListBox.ScrollIntoView(_logItems[_logItems.Count - 1]);
        }
    }

    private void UpdateLogsCount()
    {
        LogsCountText.Text = $"({_logItems.Count} kayıt)";
    }

    private void ClearLogs_Click(object sender, RoutedEventArgs e)
    {
        _loggerService.Clear();
        _logItems.Clear();
        UpdateLogsCount();
    }

    private void CopyLogs_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var text = string.Join(Environment.NewLine, _logItems);
            if (!string.IsNullOrEmpty(text))
            {
                Clipboard.SetText(text);
                MessageBox.Show("Loglar panoya kopyalandı.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch { }
    }
}
