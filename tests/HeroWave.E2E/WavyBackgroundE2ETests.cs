using System.Linq;
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
    public async Task A11y_Static_Button_Stops_Animation()
    {
        var page = await NavigateToAsync("a11y");

        var animatingBefore = await IsCanvasAnimatingAsync(page);
        Assert.True(animatingBefore, "Canvas should be animating initially");

        await page.Locator("#btn-static").ClickAsync();
        await WaitForSubtitleAsync(page, "AlwaysStatic");

        var animatingAfter = await IsCanvasAnimatingAsync(page);
        Assert.False(animatingAfter, "Canvas should stop after clicking Static");
    }

    [Fact]
    public async Task A11y_Animate_Button_Resumes_After_Static()
    {
        var page = await NavigateToAsync("a11y");

        await page.Locator("#btn-static").ClickAsync();
        await WaitForSubtitleAsync(page, "AlwaysStatic");

        var animatingWhileStatic = await IsCanvasAnimatingAsync(page);
        Assert.False(animatingWhileStatic, "Should be static");

        await page.Locator("#btn-animate").ClickAsync();
        await WaitForSubtitleAsync(page, "AlwaysAnimate");

        var animatingAfterResume = await IsCanvasAnimatingAsync(page);
        Assert.True(animatingAfterResume, "Should resume after clicking Animate");
    }

    [Fact]
    public async Task A11y_PrefersReducedMotion_Stops_Animation()
    {
        var page = await NavigateToAsync("a11y");

        var animatingBefore = await IsCanvasAnimatingAsync(page);
        Assert.True(animatingBefore, "Should be animating before reduced-motion");

        await page.EmulateMediaAsync(new() { ReducedMotion = ReducedMotion.Reduce });
        await page.Locator("#btn-respect").ClickAsync();
        await WaitForSubtitleAsync(page, "RespectSystemPreference");

        var animatingAfter = await IsCanvasAnimatingAsync(page);
        Assert.False(animatingAfter, "Should stop when prefers-reduced-motion is active");
    }

    [Fact]
    public async Task A11y_AlwaysAnimate_Overrides_ReducedMotion()
    {
        var page = await NavigateToAsync("a11y");

        // Enable system reduced-motion
        await page.EmulateMediaAsync(new() { ReducedMotion = ReducedMotion.Reduce });
        await page.Locator("#btn-respect").ClickAsync();
        await WaitForSubtitleAsync(page, "RespectSystemPreference");

        var animatingWhileReduced = await IsCanvasAnimatingAsync(page);
        Assert.False(animatingWhileReduced, "Should be static when system prefers reduced-motion");

        // Override with AlwaysAnimate — should animate despite system preference
        await page.Locator("#btn-animate").ClickAsync();
        await WaitForSubtitleAsync(page, "AlwaysAnimate");

        var animatingAfterOverride = await IsCanvasAnimatingAsync(page);
        Assert.True(animatingAfterOverride, "AlwaysAnimate should override prefers-reduced-motion");
    }

    /// <summary>
    /// Waits for the Blazor subtitle to update with the given mode text.
    /// More reliable than a hardcoded timeout — confirms the JS interop round-trip completed.
    /// </summary>
    private static async Task WaitForSubtitleAsync(IPage page, string modeText)
    {
        await page.WaitForFunctionAsync(
            $"document.querySelector('.wavy-background-subtitle')?.textContent.includes('{modeText}')",
            new PageWaitForFunctionOptions { Timeout = 10_000 });
    }

    /// <summary>
    /// Detects animation by comparing pixel checksums of a small canvas region across multiple samples.
    /// Uses getImageData on a 50×50 region (cheaper than full toDataURL) with a simple sum checksum.
    /// </summary>
    private static async Task<bool> IsCanvasAnimatingAsync(IPage page, int sampleCount = 5, int intervalMs = 150)
    {
        var checksums = new HashSet<string>();
        for (int i = 0; i < sampleCount; i++)
        {
            await Task.Delay(intervalMs);
            var checksum = await page.EvaluateAsync<string>(@"() => {
                const c = document.querySelector('canvas');
                const ctx = c.getContext('2d');
                if (!ctx || c.width === 0 || c.height === 0) return 'empty';
                const w = Math.min(c.width, 50);
                const h = Math.min(c.height, 50);
                const data = ctx.getImageData(0, 0, w, h).data;
                let sum = 0;
                for (let i = 0; i < data.length; i++) sum += data[i];
                return sum.toString();
            }");
            checksums.Add(checksum);
        }
        return checksums.Count > 1;
    }
}
