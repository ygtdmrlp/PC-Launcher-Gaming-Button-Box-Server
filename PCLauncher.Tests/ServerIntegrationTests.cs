using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PCLauncher.Core.Services;
using PCLauncher.Server;
using Xunit;

namespace PCLauncher.Tests;

public class ServerIntegrationTests : IAsyncLifetime
{
    private readonly LauncherServer _server;
    private readonly HttpClient _client;
    private const int TestPort = 5099;
    private const string TestIp = "127.0.0.1";

    private readonly string _tempDir;

    public ServerIntegrationTests()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "PCLauncher_ServerTest_" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(_tempDir);

        var logger = new LoggerService();
        var storage = new JsonStorageService(_tempDir);
        var iconService = new IconService();
        var launcherService = new AppLauncherService(storage, logger);

        _server = new LauncherServer(storage, launcherService, iconService, logger);
        _client = new HttpClient
        {
            BaseAddress = new Uri($"http://{TestIp}:{TestPort}")
        };
    }

    public async Task InitializeAsync()
    {
        await _server.StartAsync(TestPort, TestIp, CancellationToken.None);
    }

    public async Task DisposeAsync()
    {
        if (_server.IsRunning)
        {
            await _server.StopAsync(CancellationToken.None);
        }
        _client.Dispose();

        try
        {
            if (System.IO.Directory.Exists(_tempDir))
            {
                System.IO.Directory.Delete(_tempDir, true);
            }
        }
        catch { }
    }

    [Fact]
    public async Task GetStatus_ReturnsOnlineStatus()
    {
        var response = await _client.GetAsync("/api/status");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.GetProperty("isOnline").GetBoolean());
        Assert.Equal(TestIp, root.GetProperty("ip").GetString());
        Assert.Equal(TestPort, root.GetProperty("port").GetInt32());
    }

    [Fact]
    public async Task GetApps_ReturnsSuccessStatusCode()
    {
        var response = await _client.GetAsync("/api/apps");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        Assert.NotNull(json);
    }

    [Fact]
    public async Task GetIndexHtml_ServesMobileApp()
    {
        var response = await _client.GetAsync("/");
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("PC Controller Deck", content);
        Assert.Contains("keysDeckGrid", content);
    }

    [Fact]
    public async Task GetKeys_ReturnsDefaultKeys()
    {
        var response = await _client.GetAsync("/api/keys");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.GetArrayLength() > 0);
    }

    [Fact]
    public async Task PostKeyPress_ExecutesSuccessfully()
    {
        var keysResponse = await _client.GetAsync("/api/keys");
        var json = await keysResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var firstKeyId = doc.RootElement[0].GetProperty("id").GetString();

        var pressResponse = await _client.PostAsync($"/api/keys/{firstKeyId}/press", null);
        pressResponse.EnsureSuccessStatusCode();

        var resultJson = await pressResponse.Content.ReadAsStringAsync();
        using var resultDoc = JsonDocument.Parse(resultJson);
        Assert.True(resultDoc.RootElement.GetProperty("success").GetBoolean());
    }
}
