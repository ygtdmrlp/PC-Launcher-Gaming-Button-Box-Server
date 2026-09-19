using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PCLauncher.Core.Models;

namespace PCLauncher.Core.Services;

public interface IStorageService
{
    string DataDirectory { get; }
    Task<List<AppItem>> LoadAppsAsync(CancellationToken cancellationToken = default);
    Task SaveAppsAsync(IEnumerable<AppItem> apps, CancellationToken cancellationToken = default);
    Task<AppSettings> LoadSettingsAsync(CancellationToken cancellationToken = default);
    Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default);
    Task<List<KeyActionItem>> LoadKeyActionsAsync(CancellationToken cancellationToken = default);
    Task SaveKeyActionsAsync(IEnumerable<KeyActionItem> keyActions, CancellationToken cancellationToken = default);
}

public class JsonStorageService : IStorageService
{
    private readonly string _dataDir;
    private readonly string _appsFilePath;
    private readonly string _settingsFilePath;
    private readonly string _keysFilePath;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    public string DataDirectory => _dataDir;

    public JsonStorageService(string? customDataDirectory = null)
    {
        _dataDir = customDataDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PCLauncher");

        Directory.CreateDirectory(_dataDir);
        _appsFilePath = Path.Combine(_dataDir, "apps.json");
        _settingsFilePath = Path.Combine(_dataDir, "settings.json");
        _keysFilePath = Path.Combine(_dataDir, "keyactions.json");

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task<List<AppItem>> LoadAppsAsync(CancellationToken cancellationToken = default)
    {
        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(_appsFilePath))
            {
                return new List<AppItem>();
            }

            var json = await File.ReadAllTextAsync(_appsFilePath, cancellationToken);
            var list = JsonSerializer.Deserialize<List<AppItem>>(json, _jsonOptions);
            return list ?? new List<AppItem>();
        }
        catch
        {
            return new List<AppItem>();
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task SaveAppsAsync(IEnumerable<AppItem> apps, CancellationToken cancellationToken = default)
    {
        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            var json = JsonSerializer.Serialize(apps, _jsonOptions);
            await File.WriteAllTextAsync(_appsFilePath, json, cancellationToken);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task<AppSettings> LoadSettingsAsync(CancellationToken cancellationToken = default)
    {
        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(_settingsFilePath))
            {
                var defaults = new AppSettings();
                var json = JsonSerializer.Serialize(defaults, _jsonOptions);
                await File.WriteAllTextAsync(_settingsFilePath, json, cancellationToken);
                return defaults;
            }

            var content = await File.ReadAllTextAsync(_settingsFilePath, cancellationToken);
            var settings = JsonSerializer.Deserialize<AppSettings>(content, _jsonOptions);
            return settings ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            var json = JsonSerializer.Serialize(settings, _jsonOptions);
            await File.WriteAllTextAsync(_settingsFilePath, json, cancellationToken);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task<List<KeyActionItem>> LoadKeyActionsAsync(CancellationToken cancellationToken = default)
    {
        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(_keysFilePath))
            {
                var defaults = CreateDefaultKeyActions();
                var json = JsonSerializer.Serialize(defaults, _jsonOptions);
                await File.WriteAllTextAsync(_keysFilePath, json, cancellationToken);
                return defaults;
            }

            var content = await File.ReadAllTextAsync(_keysFilePath, cancellationToken);
            var list = JsonSerializer.Deserialize<List<KeyActionItem>>(content, _jsonOptions);
            return list ?? new List<KeyActionItem>();
        }
        catch
        {
            return CreateDefaultKeyActions();
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task SaveKeyActionsAsync(IEnumerable<KeyActionItem> keyActions, CancellationToken cancellationToken = default)
    {
        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            var json = JsonSerializer.Serialize(keyActions, _jsonOptions);
            await File.WriteAllTextAsync(_keysFilePath, json, cancellationToken);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private static List<KeyActionItem> CreateDefaultKeyActions()
    {
        return new List<KeyActionItem>
        {
            new() { Title = "Farlar", KeySequence = "L", GameOrCategory = "Simülasyon", ColorHex = "#3B82F6", PresetIcon = "💡", Order = 1 },
            new() { Title = "Motor Başlat", KeySequence = "E", GameOrCategory = "Simülasyon", ColorHex = "#10B981", PresetIcon = "⚡", Order = 2 },
            new() { Title = "El Freni", KeySequence = "Space", GameOrCategory = "Simülasyon", ColorHex = "#EF4444", PresetIcon = "🛑", Order = 3 },
            new() { Title = "Telsiz", KeySequence = "X", GameOrCategory = "Simülasyon", ColorHex = "#F59E0B", PresetIcon = "📻", Order = 4 },
            new() { Title = "Harita", KeySequence = "M", GameOrCategory = "Simülasyon", ColorHex = "#8B5CF6", PresetIcon = "🗺️", Order = 5 },
            new() { Title = "Screenshot", KeySequence = "F12", GameOrCategory = "Genel", ColorHex = "#EC4899", PresetIcon = "📷", Order = 6 },
            new() { Title = "Ses Sustur", KeySequence = "Mute", GameOrCategory = "Medya", ColorHex = "#64748B", PresetIcon = "🔇", Order = 7 },
            new() { Title = "Oynat / Durdur", KeySequence = "PlayPause", GameOrCategory = "Medya", ColorHex = "#06B6D4", PresetIcon = "⏯️", Order = 8 }
        };
    }
}
