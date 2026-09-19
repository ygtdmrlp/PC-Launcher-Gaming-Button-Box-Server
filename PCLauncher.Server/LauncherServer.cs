using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PCLauncher.Core.Models;
using PCLauncher.Core.Services;

namespace PCLauncher.Server;

public interface ILauncherServer
{
    bool IsRunning { get; }
    ServerStatus Status { get; }
    event Action<ServerStatus>? StatusChanged;
    Task StartAsync(int port, string ipAddress, CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}

public class LauncherServer : ILauncherServer
{
    private readonly IStorageService _storageService;
    private readonly IAppLauncherService _launcherService;
    private readonly IIconService _iconService;
    private readonly ILoggerService _loggerService;
    private readonly IKeySimulatorService _keySimulatorService;

    private WebApplication? _app;
    private readonly SemaphoreSlim _startLock = new(1, 1);
    private DateTime? _startTime;
    private readonly ConcurrentDictionary<string, DateTime> _activeClients = new();

    public bool IsRunning => _app != null;
    public ServerStatus Status { get; private set; } = new();

    public event Action<ServerStatus>? StatusChanged;

    public LauncherServer(
        IStorageService storageService,
        IAppLauncherService launcherService,
        IIconService iconService,
        ILoggerService loggerService,
        IKeySimulatorService? keySimulatorService = null)
    {
        _storageService = storageService;
        _launcherService = launcherService;
        _iconService = iconService;
        _loggerService = loggerService;
        _keySimulatorService = keySimulatorService ?? new KeySimulatorService();
    }

    public async Task StartAsync(int port, string ipAddress, CancellationToken cancellationToken = default)
    {
        await _startLock.WaitAsync(cancellationToken);
        try
        {
            if (_app != null) return;

            _loggerService.LogInfo($"Web sunucusu başlatılıyor: http://{ipAddress}:{port}...");

            var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions
            {
                Args = Array.Empty<string>()
            });

            // Find wwwroot folder
            var baseDir = AppContext.BaseDirectory;
            var wwwrootDir = Path.Combine(baseDir, "wwwroot");
            if (!Directory.Exists(wwwrootDir))
            {
                // Fallback for debug environment
                var candidate = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "PCLauncher.Server", "wwwroot"));
                if (Directory.Exists(candidate))
                {
                    wwwrootDir = candidate;
                }
                else
                {
                    Directory.CreateDirectory(wwwrootDir);
                }
            }

            builder.WebHost.UseKestrel(options =>
            {
                options.Listen(IPAddress.Any, port);
            });

            builder.Services.AddRouting();
            builder.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                {
                    policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
                });
            });

            _app = builder.Build();

            _app.UseCors();

            // Client tracking middleware
            _app.Use(async (context, next) =>
            {
                var remoteIp = context.Connection.RemoteIpAddress?.ToString() ?? "Bilinmeyen";
                _activeClients[remoteIp] = DateTime.Now;

                // Cleanup inactive clients (older than 1 minute)
                var cutoff = DateTime.Now.AddMinutes(-1);
                foreach (var key in _activeClients.Keys)
                {
                    if (_activeClients.TryGetValue(key, out var lastSeen) && lastSeen < cutoff)
                    {
                        _activeClients.TryRemove(key, out _);
                    }
                }

                await next();
            });

            // Serve Static Files from wwwroot
            if (Directory.Exists(wwwrootDir))
            {
                var fileProvider = new PhysicalFileProvider(wwwrootDir);
                var defaultOptions = new DefaultFilesOptions
                {
                    FileProvider = fileProvider
                };
                defaultOptions.DefaultFileNames.Clear();
                defaultOptions.DefaultFileNames.Add("index.html");

                _app.UseDefaultFiles(defaultOptions);
                _app.UseStaticFiles(new StaticFileOptions
                {
                    FileProvider = fileProvider,
                    RequestPath = ""
                });
            }

            // Map Endpoints
            MapEndpoints(_app);

            await _app.StartAsync(cancellationToken);
            _startTime = DateTime.Now;

            Status = new ServerStatus
            {
                IsRunning = true,
                IpAddress = ipAddress,
                Port = port,
                StartTime = _startTime,
                ConnectedClients = _activeClients.Count
            };

            _loggerService.LogInfo($"Web sunucusu aktif! Telefon adresi: http://{ipAddress}:{port}");
            StatusChanged?.Invoke(Status);
        }
        catch (Exception ex)
        {
            _loggerService.LogError("Web sunucusu başlatılırken hata oluştu", ex);
            Status = new ServerStatus
            {
                IsRunning = false,
                IpAddress = ipAddress,
                Port = port,
                ErrorMessage = ex.Message
            };
            StatusChanged?.Invoke(Status);
            throw;
        }
        finally
        {
            _startLock.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _startLock.WaitAsync(cancellationToken);
        try
        {
            if (_app == null) return;

            _loggerService.LogInfo("Web sunucusu durduruluyor...");
            await _app.StopAsync(cancellationToken);
            await _app.DisposeAsync();
            _app = null;
            _startTime = null;

            Status = new ServerStatus
            {
                IsRunning = false,
                IpAddress = Status.IpAddress,
                Port = Status.Port
            };

            _loggerService.LogInfo("Web sunucusu durduruldu.");
            StatusChanged?.Invoke(Status);
        }
        catch (Exception ex)
        {
            _loggerService.LogError("Web sunucusu durdurulurken hata oluştu", ex);
        }
        finally
        {
            _startLock.Release();
        }
    }

    private void MapEndpoints(WebApplication app)
    {
        // GET /api/status
        app.MapGet("/api/status", async () =>
        {
            var settings = await _storageService.LoadSettingsAsync();
            var apps = await _storageService.LoadAppsAsync();

            var uptimeStr = _startTime.HasValue
                ? FormatUptime(DateTime.Now - _startTime.Value)
                : "0s";

            return Results.Ok(new
            {
                isOnline = true,
                machineName = Environment.MachineName,
                ip = Status.IpAddress,
                port = Status.Port,
                appCount = apps.Count,
                requiresPin = settings.UsePin,
                uptime = uptimeStr,
                connectedDevices = _activeClients.Count
            });
        });

        // POST /api/auth/verify
        app.MapPost("/api/auth/verify", async (HttpContext context) =>
        {
            var settings = await _storageService.LoadSettingsAsync();
            if (!settings.UsePin)
            {
                return Results.Ok(new { success = true, message = "PIN koruması kapalı." });
            }

            try
            {
                using var reader = new StreamReader(context.Request.Body);
                var body = await reader.ReadToEndAsync();
                var doc = JsonDocument.Parse(body);
                var pin = doc.RootElement.GetProperty("pin").GetString();

                if (string.Equals(pin, settings.PinCode, StringComparison.Ordinal))
                {
                    _loggerService.LogInfo("Telefon PIN doğrulaması başarılı.");
                    return Results.Ok(new { success = true });
                }

                _loggerService.LogWarning("Hatalı PIN denemesi yapıldı.");
                return Results.Json(new { success = false, message = "Hatalı PIN kodu!" }, statusCode: 401);
            }
            catch
            {
                return Results.BadRequest(new { success = false, message = "Geçersiz istek formatı." });
            }
        });

        // GET /api/apps
        app.MapGet("/api/apps", async (HttpContext context) =>
        {
            var settings = await _storageService.LoadSettingsAsync();
            if (settings.UsePin && !ValidatePin(context, settings.PinCode))
            {
                return Results.Json(new { error = "PIN doğrulaması gerekli." }, statusCode: 401);
            }

            var apps = await _storageService.LoadAppsAsync();
            var ordered = apps.OrderBy(a => a.Order).ThenBy(a => a.Name).ToList();
            return Results.Ok(ordered);
        });

        // GET /api/apps/{id}/icon
        app.MapGet("/api/apps/{id}/icon", async (string id) =>
        {
            var apps = await _storageService.LoadAppsAsync();
            var app = apps.FirstOrDefault(a => string.Equals(a.Id, id, StringComparison.OrdinalIgnoreCase));

            if (app == null)
            {
                return Results.NotFound();
            }

            byte[]? iconBytes = null;
            if (!string.IsNullOrWhiteSpace(app.IconBase64))
            {
                iconBytes = _iconService.GetIconBytes(app.IconBase64);
            }

            if (iconBytes == null && File.Exists(app.ExePath))
            {
                var b64 = _iconService.ExtractIconAsBase64(app.ExePath);
                iconBytes = _iconService.GetIconBytes(b64);
            }

            if (iconBytes == null || iconBytes.Length == 0)
            {
                var fallbackB64 = _iconService.CreateDefaultIcon(app.Name, app.Category);
                iconBytes = _iconService.GetIconBytes(fallbackB64);
            }

            if (iconBytes != null && iconBytes.Length > 0)
            {
                return Results.File(iconBytes, "image/png");
            }

            return Results.NotFound();
        });

        // POST /api/apps/{id}/launch
        app.MapPost("/api/apps/{id}/launch", async (string id, HttpContext context) =>
        {
            var remoteIp = context.Connection.RemoteIpAddress?.ToString() ?? "Bilinmeyen";
            _loggerService.LogInfo($"Cihazdan çalıştırma isteği ({remoteIp}): ID {id}");

            var settings = await _storageService.LoadSettingsAsync();
            if (settings.UsePin && !ValidatePin(context, settings.PinCode))
            {
                _loggerService.LogWarning($"PIN yetkilendirmesi başarısız ({remoteIp}).");
                return Results.Json(new { success = false, message = "Yetkisiz erişim: PIN gerekli." }, statusCode: 401);
            }

            var result = await _launcherService.LaunchAsync(id);

            return Results.Ok(new
            {
                success = result.IsSuccess,
                appName = result.AppName,
                message = result.Message,
                errorDetail = result.ErrorDetail
            });
        });

        // GET /api/keys
        app.MapGet("/api/keys", async (HttpContext context) =>
        {
            var settings = await _storageService.LoadSettingsAsync();
            if (settings.UsePin && !ValidatePin(context, settings.PinCode))
            {
                return Results.Json(new { error = "PIN doğrulaması gerekli." }, statusCode: 401);
            }

            var keys = await _storageService.LoadKeyActionsAsync();
            var ordered = keys.OrderBy(k => k.Order).ThenBy(k => k.Title).ToList();
            return Results.Ok(ordered);
        });

        // GET /api/keys/{id}/icon
        app.MapGet("/api/keys/{id}/icon", async (string id) =>
        {
            var keys = await _storageService.LoadKeyActionsAsync();
            var action = keys.FirstOrDefault(k => string.Equals(k.Id, id, StringComparison.OrdinalIgnoreCase));

            if (action == null || string.IsNullOrWhiteSpace(action.IconBase64))
            {
                return Results.NotFound();
            }

            var iconBytes = _iconService.GetIconBytes(action.IconBase64);
            if (iconBytes != null && iconBytes.Length > 0)
            {
                return Results.File(iconBytes, "image/png");
            }

            return Results.NotFound();
        });

        // POST /api/keys/{id}/press
        app.MapPost("/api/keys/{id}/press", async (string id, HttpContext context) =>
        {
            var remoteIp = context.Connection.RemoteIpAddress?.ToString() ?? "Bilinmeyen";
            var settings = await _storageService.LoadSettingsAsync();
            if (settings.UsePin && !ValidatePin(context, settings.PinCode))
            {
                _loggerService.LogWarning($"PIN yetkilendirmesi başarısız (Tuş isteği, {remoteIp}).");
                return Results.Json(new { success = false, message = "Yetkisiz erişim: PIN gerekli." }, statusCode: 401);
            }

            var keys = await _storageService.LoadKeyActionsAsync();
            var action = keys.FirstOrDefault(k => string.Equals(k.Id, id, StringComparison.OrdinalIgnoreCase));

            if (action == null)
            {
                _loggerService.LogWarning($"Tanımsız tuş basma isteği ({remoteIp}): {id}");
                return Results.Json(new { success = false, message = "Tuş aksiyonu bulunamadı." }, statusCode: 404);
            }

            _loggerService.LogInfo($"Tuş komutu alındı ({remoteIp}): {action.Title} [{action.KeySequence}]");

            var holdDuration = action.HoldDurationMs > 0 ? action.HoldDurationMs : 100;
            var result = await _keySimulatorService.SimulateKeySequenceAsync(action.KeySequence, holdDuration);

            if (result.Success)
            {
                action.PressCount++;
                action.LastPressedAt = DateTime.Now;
                _ = Task.Run(async () =>
                {
                    try { await _storageService.SaveKeyActionsAsync(keys); } catch { }
                });

                _loggerService.LogInfo($"Tuş başarıyla iletildi: {action.Title} [{action.KeySequence}]");
            }
            else
            {
                _loggerService.LogError($"Tuş iletilemedi: {action.Title} - {result.Message}");
            }

            return Results.Ok(new
            {
                success = result.Success,
                title = action.Title,
                key = action.KeySequence,
                message = result.Message,
                errorDetail = result.ErrorDetail
            });
        });
    }

    private static bool ValidatePin(HttpContext context, string? expectedPin)
    {
        if (string.IsNullOrWhiteSpace(expectedPin)) return true;

        if (context.Request.Headers.TryGetValue("X-Launcher-PIN", out var pinHeader))
        {
            return string.Equals(pinHeader.ToString(), expectedPin, StringComparison.Ordinal);
        }

        if (context.Request.Query.TryGetValue("pin", out var pinQuery))
        {
            return string.Equals(pinQuery.ToString(), expectedPin, StringComparison.Ordinal);
        }

        return false;
    }

    private static string FormatUptime(TimeSpan span)
    {
        if (span.TotalHours >= 1)
            return $"{(int)span.TotalHours}s {span.Minutes:D2}d";
        if (span.TotalMinutes >= 1)
            return $"{span.Minutes}d {span.Seconds:D2}sn";
        return $"{span.Seconds}sn";
    }
}
