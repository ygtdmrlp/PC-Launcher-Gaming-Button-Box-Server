using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using PCLauncher.Core.Services;
using PCLauncher.Server;

namespace PCLauncher.App;

public partial class App : Application
{
    private static Mutex? _singleInstanceMutex;
    private const string MutexName = "PCLauncher_Desktop_SingleInstance_Mutex";

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Enforce single instance
        _singleInstanceMutex = new Mutex(true, MutexName, out bool isNewInstance);
        if (!isNewInstance)
        {
            MessageBox.Show("PC Launcher zaten çalışıyor. Sistem tepsisinden veya görev çubuğundan erişebilirsiniz.",
                "PC Launcher", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        // Initialize Services
        var loggerService = new LoggerService();
        var storageService = new JsonStorageService();
        var iconService = new IconService();
        var networkService = new NetworkService();
        var qrCodeService = new QrCodeService();
        var launcherService = new AppLauncherService(storageService, loggerService);
        var firewallService = new FirewallService();
        var startupService = new StartupService();
        var keySimulatorService = new KeySimulatorService();
        var server = new LauncherServer(storageService, launcherService, iconService, loggerService, keySimulatorService);

        var mainWindow = new MainWindow(
            storageService,
            loggerService,
            iconService,
            networkService,
            qrCodeService,
            launcherService,
            firewallService,
            startupService,
            server,
            keySimulatorService);

        // Check if launched with --minimized
        bool startMinimized = e.Args.Any(a => a.Equals("--minimized", StringComparison.OrdinalIgnoreCase) ||
                                              a.Equals("-m", StringComparison.OrdinalIgnoreCase));

        if (startMinimized)
        {
            mainWindow.WindowState = WindowState.Minimized;
            mainWindow.Hide();
        }
        else
        {
            mainWindow.Show();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}
