using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.Playwright;
using Xunit;

namespace HeroWave.E2E;

public class DemoAppFixture : IAsyncLifetime
{
    private Process? _process;
    public string BaseUrl { get; private set; } = "";
    public IPlaywright Playwright { get; private set; } = null!;
    public IBrowser Browser { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var port = GetFreePort();
        BaseUrl = $"http://localhost:{port}";

        _process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --no-build --project demo/HeroWave.Demo --urls {BaseUrl}",
                WorkingDirectory = FindSolutionRoot(),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };
        _process.Start();

        // Wait for the app to be ready
        using var client = new HttpClient();
        for (int i = 0; i < 60; i++)
        {
            try
            {
                var response = await client.GetAsync(BaseUrl);
                if (response.IsSuccessStatusCode) break;
            }
            catch { }
            await Task.Delay(1000);
        }

        Playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        Browser = await Playwright.Chromium.LaunchAsync(new() { Headless = true });
    }

    public async Task DisposeAsync()
    {
        await Browser.DisposeAsync();
        Playwright.Dispose();

        if (_process is { HasExited: false })
        {
            try
            {
                _process.Kill(entireProcessTree: true);
            }
            catch
            {
                try { _process.Kill(); } catch { }
            }
            _process.Dispose();
        }
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static string FindSolutionRoot()
    {
        var dir = Directory.GetCurrentDirectory();
        while (dir != null)
        {
            if (Directory.GetFiles(dir, "*.sln").Length > 0) return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }
        throw new Exception("Could not find solution root");
    }
}
