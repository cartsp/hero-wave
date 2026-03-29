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

    private async Task<IPage> NavigateToAsync(string path = "")
    {
        var page = await NewPageAsync();
        var url = string.IsNullOrEmpty(path) ? _fixture.BaseUrl : $"{_fixture.BaseUrl}/{path}";
        await page.GotoAsync(url, NavigationOptions);
        await page.WaitForSelectorAsync("canvas");
        return page;
    }

    private static async Task WaitForBlazorUpdateAsync() => await Task.Delay(500);

    [Fact]
    public async Task HomePage_Renders_WavyBackground()
    {
        var page = await NavigateToAsync();
        var canvas = page.Locator("canvas").First;
        var box = await canvas.BoundingBoxAsync();

        Assert.NotNull(box);
        Assert.True(box!.Width > 0, "Canvas should have non-zero width");
        Assert.True(box.Height > 0, "Canvas should have non-zero height");
    }

    [Fact]
    public async Task HomePage_Has_Title_And_Subtitle()
    {
        var page = await NavigateToAsync();
        var title = await page.Locator("h1").TextContentAsync();
        var subtitle = await page.Locator("p.wavy-background-subtitle").TextContentAsync();

        Assert.Contains("Build Amazing Apps", title);
        Assert.Contains("reusable wavy background", subtitle);
    }

    [Fact]
    public async Task FullPage_Renders_Without_Errors()
    {
        var page = await NavigateToAsync("fullpage");
        var errors = new List<string>();
        page.Console += (_, msg) =>
        {
            if (msg.Type == "error") errors.Add(msg.Text);
        };

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
        var page = await NavigateToAsync("showcase");
        var canvases = await page.Locator("canvas").CountAsync();
        Assert.True(canvases >= 6, $"Expected at least 6 canvas elements, found {canvases}");
    }

    // --- Accessibility & ReducedMotion E2E Tests ---

    [Fact]
    public async Task A11y_Canvas_Has_AriaHidden_True()
    {
        var page = await NavigateToAsync("a11y");
        var ariaHidden = await page.Locator("canvas").GetAttributeAsync("aria-hidden");
        Assert.Equal("true", ariaHidden);
    }

    [Fact]
    public async Task A11y_Container_Has_No_Role_Attribute()
    {
        var page = await NavigateToAsync("a11y");
        var container = page.Locator(".wavy-background-container");
        var role = await container.GetAttributeAsync("role");
        Assert.Null(role);
    }

    [Fact]
    public async Task A11y_PrefersReducedMotion_Stops_Animation()
    {
        var page = await NavigateToAsync("a11y");

        var animatingBefore = await IsCanvasAnimatingAsync(page);
        Assert.True(animatingBefore, "Canvas should be animating before reduced-motion is applied");

        // Emulate prefers-reduced-motion: reduce and switch to RespectSystemPreference
        await page.EmulateMediaAsync(new()
        {
            ReducedMotion = Microsoft.Playwright.ReducedMotion.Reduce
        });
        await page.Locator("#btn-respect").ClickAsync();
        await WaitForBlazorUpdateAsync();

        var animatingAfter = await IsCanvasAnimatingAsync(page);
        Assert.False(animatingAfter, "Canvas should stop animating when prefers-reduced-motion is active");
    }

    [Fact]
    public async Task A11y_StaticButton_Stops_Animation()
    {
        var page = await NavigateToAsync("a11y");

        var animatingBefore = await IsCanvasAnimatingAsync(page);
        Assert.True(animatingBefore, "Canvas should be animating initially");

        await page.Locator("#btn-static").ClickAsync();
        await WaitForBlazorUpdateAsync();

        var animatingAfter = await IsCanvasAnimatingAsync(page);
        Assert.False(animatingAfter, "Canvas should stop animating after clicking Static");
    }

    [Fact]
    public async Task A11y_AnimateButton_Resumes_Animation_After_Static()
    {
        var page = await NavigateToAsync("a11y");

        // Stop animation first
        await page.Locator("#btn-static").ClickAsync();
        await WaitForBlazorUpdateAsync();

        var animatingWhileStatic = await IsCanvasAnimatingAsync(page);
        Assert.False(animatingWhileStatic, "Canvas should be static after clicking Static");

        // Resume animation
        await page.Locator("#btn-animate").ClickAsync();
        await WaitForBlazorUpdateAsync();

        var animatingAfterResume = await IsCanvasAnimatingAsync(page);
        Assert.True(animatingAfterResume, "Canvas should resume animating after clicking Animate");
    }

    private static async Task<bool> IsCanvasAnimatingAsync(IPage page, int sampleCount = 3)
    {
        var snapshots = new List<string>();
        for (int i = 0; i < sampleCount; i++)
        {
            await Task.Delay(200);
            var dataUrl = await page.EvaluateAsync<string>("document.querySelector('canvas').toDataURL()");
            snapshots.Add(dataUrl);
        }
        return snapshots.Distinct().Count() > 1;
    }
}
