using Microsoft.Playwright;
using Xunit;

namespace HeroWave.E2E;

public class WavyBackgroundE2ETests : IClassFixture<DemoAppFixture>
{
    private readonly DemoAppFixture _fixture;

    public WavyBackgroundE2ETests(DemoAppFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<IPage> NewPageAsync()
    {
        var page = await _fixture.Browser.NewPageAsync();
        page.SetDefaultTimeout(60_000);
        return page;
    }

    private static PageGotoOptions NavigationOptions => new()
    {
        WaitUntil = WaitUntilState.NetworkIdle,
        Timeout = 120_000
    };

    [Fact]
    public async Task HomePage_Renders_WavyBackground()
    {
        var page = await NewPageAsync();
        await page.GotoAsync(_fixture.BaseUrl, NavigationOptions);
        await page.WaitForSelectorAsync("canvas");

        var canvas = page.Locator("canvas").First;
        var box = await canvas.BoundingBoxAsync();

        Assert.NotNull(box);
        Assert.True(box!.Width > 0, "Canvas should have non-zero width");
        Assert.True(box.Height > 0, "Canvas should have non-zero height");
    }

    [Fact]
    public async Task HomePage_Has_Title_And_Subtitle()
    {
        var page = await NewPageAsync();
        await page.GotoAsync(_fixture.BaseUrl, NavigationOptions);

        var title = await page.Locator("h1").TextContentAsync();
        var subtitle = await page.Locator("p.wavy-background-subtitle").TextContentAsync();

        Assert.Contains("Build Amazing Apps", title);
        Assert.Contains("reusable wavy background", subtitle);
    }

    [Fact]
    public async Task FullPage_Renders_Without_Errors()
    {
        var page = await NewPageAsync();
        var errors = new List<string>();
        page.Console += (_, msg) =>
        {
            if (msg.Type == "error") errors.Add(msg.Text);
        };

        await page.GotoAsync($"{_fixture.BaseUrl}/fullpage", NavigationOptions);
        await page.WaitForSelectorAsync("canvas");

        var canvas = page.Locator("canvas").First;
        var box = await canvas.BoundingBoxAsync();

        Assert.NotNull(box);
        Assert.True(box!.Height > 500, "Full page canvas should be tall");
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Navigation_Between_Pages_No_Errors()
    {
        var page = await NewPageAsync();
        var errors = new List<string>();
        page.Console += (_, msg) =>
        {
            if (msg.Type == "error") errors.Add(msg.Text);
        };

        await page.GotoAsync(_fixture.BaseUrl, NavigationOptions);
        await page.WaitForSelectorAsync("canvas");

        await page.GotoAsync($"{_fixture.BaseUrl}/fullpage", NavigationOptions);
        await page.WaitForSelectorAsync("canvas");

        await page.GotoAsync(_fixture.BaseUrl, NavigationOptions);
        await page.WaitForSelectorAsync("canvas");

        Assert.Empty(errors);
    }

    [Fact]
    public async Task Canvas_Resizes_On_Window_Resize()
    {
        var page = await NewPageAsync();
        await page.SetViewportSizeAsync(1280, 720);
        await page.GotoAsync(_fixture.BaseUrl, NavigationOptions);
        await page.WaitForSelectorAsync("canvas");

        var canvasBefore = await page.Locator("canvas").First.BoundingBoxAsync();

        await page.SetViewportSizeAsync(800, 600);

        // Poll until canvas dimensions change, with timeout
        var canvasAfter = await page.Locator("canvas").First.BoundingBoxAsync();
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (canvasAfter!.Width == canvasBefore!.Width && DateTime.UtcNow < deadline)
        {
            await Task.Delay(100);
            canvasAfter = await page.Locator("canvas").First.BoundingBoxAsync();
        }

        Assert.NotNull(canvasBefore);
        Assert.NotNull(canvasAfter);
        Assert.NotEqual(canvasBefore!.Width, canvasAfter!.Width);
    }

    [Fact]
    public async Task Showcase_Renders_Multiple_Instances()
    {
        var page = await NewPageAsync();
        await page.GotoAsync($"{_fixture.BaseUrl}/showcase", NavigationOptions);
        await page.WaitForSelectorAsync("canvas");

        var canvases = await page.Locator("canvas").CountAsync();
        Assert.True(canvases >= 6, $"Expected at least 6 canvas elements, found {canvases}");
    }
}
