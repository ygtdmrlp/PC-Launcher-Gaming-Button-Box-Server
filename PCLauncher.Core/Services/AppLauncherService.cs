using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PCLauncher.Core.Models;

namespace PCLauncher.Core.Services;

public record LaunchResult(bool IsSuccess, string AppName, string Message, string? ErrorDetail = null)
{
    public static LaunchResult Success(string appName) =>
        new(true, appName, $"'{appName}' başarıyla başlatıldı.");

    public static LaunchResult Failed(string appName, string message, string? detail = null) =>
        new(false, appName, message, detail);
}

public interface IAppLauncherService
{
    Task<LaunchResult> LaunchAsync(string appId);
    Task<LaunchResult> TestLaunchAsync(AppItem app);
}

public class AppLauncherService : IAppLauncherService
{
    private readonly IStorageService _storageService;
    private readonly ILoggerService _loggerService;

    public AppLauncherService(IStorageService storageService, ILoggerService loggerService)
    {
        _storageService = storageService;
        _loggerService = loggerService;
    }

    public async Task<LaunchResult> LaunchAsync(string appId)
    {
        if (string.IsNullOrWhiteSpace(appId))
        {
            return LaunchResult.Failed("Bilinmeyen", "Geçersiz program kimliği (ID).");
        }

        var apps = await _storageService.LoadAppsAsync();
        var app = apps.FirstOrDefault(a => string.Equals(a.Id, appId, StringComparison.OrdinalIgnoreCase));

        if (app == null)
        {
            _loggerService.LogWarning($"Bilinmeyen veya yetkisiz ID ile başlatma isteği reddedildi: {appId}");
            return LaunchResult.Failed("Program", "Kayıtlı program bulunamadı. Lütfen PC uygulamasından kontrol edin.");
        }

        var result = ExecuteProcess(app);

        if (result.IsSuccess)
        {
            app.LaunchCount++;
            app.LastLaunchedAt = DateTime.Now;
            _ = Task.Run(async () =>
            {
                try
                {
                    await _storageService.SaveAppsAsync(apps);
                }
                catch { }
            });
        }

        return result;
    }

    public Task<LaunchResult> TestLaunchAsync(AppItem app)
    {
        var result = ExecuteProcess(app);
        return Task.FromResult(result);
    }

    private LaunchResult ExecuteProcess(AppItem app)
    {
        _loggerService.LogInfo($"Başlatma isteği alındı: {app.Name}");

        if (string.IsNullOrWhiteSpace(app.ExePath))
        {
            _loggerService.LogError($"'{app.Name}' başlatılamadı: Dosya yolu boş.");
            return LaunchResult.Failed(app.Name, "Program dosya yolu belirtilmemiş.");
        }

        // Security check: Path traversal verification and file existence check
        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(app.ExePath);
        }
        catch (Exception ex)
        {
            _loggerService.LogError($"'{app.Name}' geçersiz dosya yolu.", ex);
            return LaunchResult.Failed(app.Name, "Geçersiz dosya yolu formatı.", ex.Message);
        }

        if (!File.Exists(fullPath))
        {
            _loggerService.LogError($"'{app.Name}' dosyası bulunamadı: {fullPath}");
            return LaunchResult.Failed(app.Name, $"Dosya bulunamadı: {Path.GetFileName(fullPath)}", fullPath);
        }

        try
        {
            var workingDir = !string.IsNullOrWhiteSpace(app.WorkingDirectory) && Directory.Exists(app.WorkingDirectory)
                ? app.WorkingDirectory
                : Path.GetDirectoryName(fullPath) ?? string.Empty;

            var startInfo = new ProcessStartInfo
            {
                FileName = fullPath,
                Arguments = app.Arguments ?? string.Empty,
                WorkingDirectory = workingDir,
                UseShellExecute = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                _loggerService.LogError($"'{app.Name}' Windows işlemi başlatılamadı.");
                return LaunchResult.Failed(app.Name, "İşlem Windows tarafından başlatılamadı.");
            }

            _loggerService.LogLaunch(app.Name, true);
            return LaunchResult.Success(app.Name);
        }
        catch (Exception ex)
        {
            _loggerService.LogLaunch(app.Name, false, ex.Message);
            return LaunchResult.Failed(app.Name, "Program başlatılırken Windows hatası oluştu.", ex.Message);
        }
    }
}
