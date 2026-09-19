using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using PCLauncher.Core.Models;
using PCLauncher.Core.Services;
using Xunit;

namespace PCLauncher.Tests;

public class CoreServicesTests
{
    [Fact]
    public void NetworkService_DetectsValidIPv4Address()
    {
        var service = new NetworkService();
        var ip = service.GetPrimaryLanIpAddress();

        Assert.False(string.IsNullOrWhiteSpace(ip));
        Assert.True(IPAddress.TryParse(ip, out var parsedIp));
        Assert.Equal(System.Net.Sockets.AddressFamily.InterNetwork, parsedIp.AddressFamily);
    }

    [Fact]
    public void QrCodeService_GeneratesValidPngBytes()
    {
        var service = new QrCodeService();
        var bytes = service.GenerateQrCodePng("http://192.168.1.100:5000", 6);

        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 64);

        // Verify PNG magic header: 89 50 4E 47 0D 0A 1A 0A
        Assert.Equal(0x89, bytes[0]);
        Assert.Equal(0x50, bytes[1]);
        Assert.Equal(0x4E, bytes[2]);
        Assert.Equal(0x47, bytes[3]);
        Assert.Equal(0x0D, bytes[4]);
        Assert.Equal(0x0A, bytes[5]);
        Assert.Equal(0x1A, bytes[6]);
        Assert.Equal(0x0A, bytes[7]);
    }

    [Fact]
    public void IconService_GeneratesValidDefaultIconBase64()
    {
        var service = new IconService();
        var b64 = service.CreateDefaultIcon("Discord", "Oyunlar");

        Assert.StartsWith("data:image/png;base64,", b64);
        var bytes = service.GetIconBytes(b64);
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);
    }

    [Fact]
    public async Task AppLauncherService_RejectsUnknownAppIdSafely()
    {
        var storage = new JsonStorageService();
        var logger = new LoggerService();
        var launcher = new AppLauncherService(storage, logger);

        var result = await launcher.LaunchAsync("non-existent-guid-9999");
        Assert.False(result.IsSuccess);
        Assert.Contains("bulunamadı", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AppLauncherService_RejectsMissingExecutableGracefully()
    {
        var storage = new JsonStorageService();
        var logger = new LoggerService();
        var launcher = new AppLauncherService(storage, logger);

        var fakeApp = new AppItem
        {
            Name = "Fake App",
            ExePath = @"C:\NonExistentFolder\nonexistent_game.exe"
        };

        var result = await launcher.TestLaunchAsync(fakeApp);
        Assert.False(result.IsSuccess);
        Assert.Contains("bulunamadı", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StorageService_SavesAndLoadsSettings()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "PCLauncher_Test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var storage = new JsonStorageService(tempDir);
            var settings = new AppSettings
            {
                Port = 5555,
                UsePin = true,
                PinCode = "4321"
            };

            await storage.SaveSettingsAsync(settings);
            var loaded = await storage.LoadSettingsAsync();

            Assert.Equal(5555, loaded.Port);
            Assert.True(loaded.UsePin);
            Assert.Equal("4321", loaded.PinCode);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    [Fact]
    public async Task KeySimulatorService_ResolvesAndSimulatesKey()
    {
        var service = new KeySimulatorService();
        var result = await service.SimulateKeySequenceAsync("Ctrl+Shift+L");

        Assert.True(result.Success);
        Assert.Contains("iletildi", result.Message);
    }

    [Fact]
    public async Task KeySimulatorService_RejectsInvalidKey()
    {
        var service = new KeySimulatorService();
        var result = await service.SimulateKeySequenceAsync("INVALID_KEY_NAME_123");

        Assert.False(result.Success);
        Assert.Contains("Tanınmayan", result.Message);
    }

    [Fact]
    public async Task StorageService_SavesAndLoadsKeyActions()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "PCLauncher_KeyTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var storage = new JsonStorageService(tempDir);
            var initialKeys = await storage.LoadKeyActionsAsync();
            Assert.NotEmpty(initialKeys); // Default keys should be loaded

            initialKeys.Add(new KeyActionItem
            {
                Title = "Nitro",
                KeySequence = "Shift",
                GameOrCategory = "Yarış",
                PresetIcon = "🚀"
            });

            await storage.SaveKeyActionsAsync(initialKeys);
            var reloaded = await storage.LoadKeyActionsAsync();

            Assert.Contains(reloaded, k => k.Title == "Nitro" && k.KeySequence == "Shift");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    [Theory]
    [InlineData("E", 0x12, false)]       // ETS2 Engine Start (DIK_E)
    [InlineData("Space", 0x39, false)]   // ETS2 Handbrake (DIK_SPACE)
    [InlineData("L", 0x26, false)]       // ETS2 Lights (DIK_L)
    [InlineData("H", 0x23, false)]       // ETS2 Horn (DIK_H)
    [InlineData("[", 0x1A, false)]       // ETS2 Retarder - (DIK_LBRACKET)
    [InlineData("]", 0x1B, false)]       // ETS2 Retarder + (DIK_RBRACKET)
    [InlineData("Up", 0x48, true)]       // Extended Arrow Up
    public void KeySimulatorService_ResolvesDirectInputScanCodesForETS2(string keyName, ushort expectedScanCode, bool expectedExtended)
    {
        var key = KeySimulatorService.ResolveKey(keyName);
        Assert.NotNull(key);
        Assert.Equal(expectedScanCode, key.ScanCode);
        Assert.Equal(expectedExtended, key.IsExtended);
    }

    [Fact]
    public async Task KeySimulatorService_SimulatesETS2KeyWithCustomHoldDuration()
    {
        var service = new KeySimulatorService();
        var result = await service.SimulateKeySequenceAsync("E", 100);

        Assert.True(result.Success);
        Assert.Contains("DirectInput/ScanCode", result.Message);
    }
}
