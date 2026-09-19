using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using PCLauncher.App.Services;
using PCLauncher.App.Views;
using PCLauncher.Core.Models;
using PCLauncher.Core.Services;
using PCLauncher.Server;

namespace PCLauncher.App;

public partial class MainWindow : Window
{
    private readonly IStorageService _storageService;
    private readonly ILoggerService _loggerService;
    private readonly IIconService _iconService;
    private readonly INetworkService _networkService;
    private readonly IQrCodeService _qrCodeService;
    private readonly IAppLauncherService _launcherService;
    private readonly IFirewallService _firewallService;
    private readonly IStartupService _startupService;
    private readonly ILauncherServer _server;
    private readonly IKeySimulatorService _keySimulatorService;

    private readonly DashboardView _dashboardView;
    private readonly AppsView _appsView;
    private readonly KeyActionsView _keyActionsView;
    private readonly SettingsView _settingsView;

    private TrayIconService? _trayIconService;
    private bool _isExplicitExit = false;

    public MainWindow(
        IStorageService storageService,
        ILoggerService loggerService,
        IIconService iconService,
        INetworkService networkService,
        IQrCodeService qrCodeService,
        IAppLauncherService launcherService,
        IFirewallService firewallService,
        IStartupService startupService,
        ILauncherServer server,
        IKeySimulatorService keySimulatorService)
    {
        InitializeComponent();

        _storageService = storageService;
        _loggerService = loggerService;
        _iconService = iconService;
        _networkService = networkService;
        _qrCodeService = qrCodeService;
        _launcherService = launcherService;
        _firewallService = firewallService;
        _startupService = startupService;
        _server = server;
        _keySimulatorService = keySimulatorService;

        // Initialize Views
        _dashboardView = new DashboardView(
            _server,
            _networkService,
            _qrCodeService,
            _storageService,
            _loggerService,
            _firewallService,
            () => NavApps.IsChecked = true);

        _appsView = new AppsView(
            _storageService,
            _launcherService,
            _iconService,
            _loggerService);

        _keyActionsView = new KeyActionsView(
            _storageService,
            _keySimulatorService,
            _iconService,
            _loggerService);

        _settingsView = new SettingsView(
            _storageService,
            _server,
            _firewallService,
            _startupService,
            _loggerService);

        _appsView.AppsChanged += async () =>
        {
            await _dashboardView.RefreshDataAsync();
        };

        _settingsView.SettingsSaved += async () =>
        {
            await _dashboardView.RefreshDataAsync();
        };

        MainContentArea.Content = _dashboardView;

        _server.StatusChanged += OnServerStatusChanged;

        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Setup System Tray
        _trayIconService = new TrayIconService(
            _server,
            ShowMainWindow,
            OpenSettings,
            ExitApplication);

        var settings = await _storageService.LoadSettingsAsync();

        // Auto start server if enabled
        if (settings.AutoStartServer && !_server.IsRunning)
        {
            try
            {
                var ip = _networkService.ResolveIpAddress(settings.SelectedIpAddress);
                await _server.StartAsync(settings.Port, ip);
            }
            catch (Exception ex)
            {
                _loggerService.LogError("Otomatik sunucu başlatma başarısız", ex);
            }
        }

        UpdateSidebarStatus(_server.Status);
    }

    private void Nav_Checked(object sender, RoutedEventArgs e)
    {
        if (MainContentArea == null) return;

        if (NavDashboard?.IsChecked == true)
        {
            MainContentArea.Content = _dashboardView;
            _ = _dashboardView.RefreshDataAsync();
        }
        else if (NavApps?.IsChecked == true)
        {
            MainContentArea.Content = _appsView;
            _ = _appsView.LoadAppsAsync();
        }
        else if (NavKeys?.IsChecked == true)
        {
            MainContentArea.Content = _keyActionsView;
            _ = _keyActionsView.LoadKeysAsync();
        }
        else if (NavSettings?.IsChecked == true)
        {
            MainContentArea.Content = _settingsView;
            _ = _settingsView.LoadSettingsAsync();
        }
    }

    private void OnServerStatusChanged(ServerStatus status)
    {
        Dispatcher.Invoke(() =>
        {
            UpdateSidebarStatus(status);
        });
    }

    private void UpdateSidebarStatus(ServerStatus status)
    {
        if (status.IsRunning)
        {
            SidebarStatusDot.Fill = new SolidColorBrush(Color.FromRgb(16, 185, 129));
            SidebarStatusText.Text = "Server Online";
            SidebarUrlText.Text = status.Url;
        }
        else
        {
            SidebarStatusDot.Fill = new SolidColorBrush(Color.FromRgb(239, 68, 68));
            SidebarStatusText.Text = "Server Offline";
            SidebarUrlText.Text = $"{status.Port} Portu Kapalı";
        }
    }

    public void ShowMainWindow()
    {
        Dispatcher.Invoke(() =>
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        });
    }

    public void OpenSettings()
    {
        Dispatcher.Invoke(() =>
        {
            ShowMainWindow();
            NavSettings.IsChecked = true;
        });
    }

    public void ExitApplication()
    {
        _isExplicitExit = true;
        _trayIconService?.Dispose();

        if (_server.IsRunning)
        {
            _ = _server.StopAsync();
        }

        Application.Current.Shutdown();
    }

    protected override async void OnClosing(CancelEventArgs e)
    {
        if (!_isExplicitExit)
        {
            var settings = await _storageService.LoadSettingsAsync();
            if (settings.MinimizeToTray)
            {
                e.Cancel = true;
                Hide();
                _trayIconService?.ShowNotification("PC Launcher", "Sunucu sistem tepsisinde arka planda çalışmaya devam ediyor.");
                return;
            }
        }

        _trayIconService?.Dispose();
        if (_server.IsRunning)
        {
            await _server.StopAsync();
        }

        base.OnClosing(e);
    }
}
