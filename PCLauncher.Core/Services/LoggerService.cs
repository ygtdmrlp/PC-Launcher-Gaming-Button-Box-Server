using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using PCLauncher.Core.Models;

namespace PCLauncher.Core.Services;

public interface ILoggerService
{
    event Action<LogEntry>? LogAdded;
    IReadOnlyList<LogEntry> GetRecentLogs();
    void Log(string message, string level = "INFO");
    void LogInfo(string message);
    void LogWarning(string message);
    void LogError(string message, Exception? ex = null);
    void LogLaunch(string appName, bool success, string? detail = null);
    void Clear();
}

public class LoggerService : ILoggerService
{
    private readonly ConcurrentQueue<LogEntry> _logs = new();
    private readonly object _fileLock = new();
    private readonly string _logFilePath;
    private const int MaxMemoryLogs = 200;

    public event Action<LogEntry>? LogAdded;

    public LoggerService()
    {
        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PCLauncher", "Logs");

        Directory.CreateDirectory(appData);
        _logFilePath = Path.Combine(appData, $"log_{DateTime.Now:yyyyMMdd}.txt");

        LogInfo("PC Launcher Logger initialized.");
    }

    public IReadOnlyList<LogEntry> GetRecentLogs() => _logs.ToArray();

    public void Log(string message, string level = "INFO")
    {
        var entry = new LogEntry
        {
            Timestamp = DateTime.Now,
            Level = level,
            Message = message
        };

        _logs.Enqueue(entry);
        while (_logs.Count > MaxMemoryLogs && _logs.TryDequeue(out _)) { }

        // Write to log file asynchronously / safely
        try
        {
            lock (_fileLock)
            {
                File.AppendAllText(_logFilePath, entry.DisplayText + Environment.NewLine);
            }
        }
        catch
        {
            // Ignore file logging failures
        }

        LogAdded?.Invoke(entry);
    }

    public void LogInfo(string message) => Log(message, "INFO");
    public void LogWarning(string message) => Log(message, "WARN");

    public void LogError(string message, Exception? ex = null)
    {
        var fullMsg = ex == null ? message : $"{message} (Hata: {ex.Message})";
        Log(fullMsg, "ERROR");
    }

    public void LogLaunch(string appName, bool success, string? detail = null)
    {
        var level = success ? "LAUNCH" : "ERROR";
        var msg = success 
            ? $"'{appName}' başarıyla başlatıldı." 
            : $"'{appName}' başlatılamadı! {detail}";
        Log(msg, level);
    }

    public void Clear()
    {
        _logs.Clear();
    }
}
