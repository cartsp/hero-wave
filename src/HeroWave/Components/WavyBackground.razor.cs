using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace HeroWave.Components;

public partial class WavyBackground : ComponentBase, IAsyncDisposable
{
    [Inject] private IJSRuntime JS { get; set; } = default!;

    /// <summary>
    /// Optional title text displayed over the wave background.
    /// </summary>
    [Parameter] public string? Title { get; set; }

    /// <summary>
    /// Optional subtitle text displayed below the title.
    /// </summary>
    [Parameter] public string? Subtitle { get; set; }

    /// <summary>
    /// Optional child content rendered inside the wave overlay.
    /// </summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// Height of the wave background container. Defaults to <c>"100vh"</c>.
    /// Accepts any valid CSS height value (e.g. <c>"500px"</c>, <c>"80vh"</c>).
    /// </summary>
    [Parameter] public string Height { get; set; } = "100vh";

    /// <summary>
    /// Array of CSS color strings used for each wave. Defaults to a blue-purple palette.
    /// Colors are cycled if there are fewer colors than waves.
    /// </summary>
    [Parameter] public string[] Colors { get; set; } =
        ["#38bdf8", "#818cf8", "#c084fc", "#e879f9", "#22d3ee"];

    /// <summary>
    /// Background color of the canvas. Defaults to <c>"#0c0c14"</c> (dark).
    /// </summary>
    [Parameter] public string BackgroundColor { get; set; } = "#0c0c14";

    /// <summary>
    /// Number of wave lines to render. Defaults to <c>5</c>.
    /// </summary>
    [Parameter] public int WaveCount { get; set; } = 5;

    /// <summary>
    /// Base stroke width of each wave in CSS pixels. Defaults to <c>50</c>.
    /// </summary>
    [Parameter] public int WaveWidth { get; set; } = 50;

    /// <summary>
    /// Animation speed. Defaults to <c>0.004</c>. Higher values = faster waves.
    /// </summary>
    [Parameter] public double Speed { get; set; } = 0.004;

    /// <summary>
    /// Wave opacity multiplier. Defaults to <c>0.5</c>.
    /// Controls the overall visibility of the waves.
    /// </summary>
    [Parameter] public double Opacity { get; set; } = 0.5;

    /// <summary>
    /// Target frames per second for the animation loop. Defaults to <c>60</c>.
    /// Lower values reduce CPU/GPU usage on less capable devices.
    /// Clamped to the range 1–120.
    /// </summary>
    [Parameter] public int TargetFps { get; set; } = 60;

    /// <summary>
    /// Optional CSS class applied to the text overlay container.
    /// </summary>
    [Parameter] public string? CssClass { get; set; }

    private ElementReference _canvas;
    private IJSObjectReference? _module;
    private string? _instanceId;
    private string? _previousConfigHash;

    private object BuildConfig() => new
    {
        colors = Colors,
        backgroundColor = BackgroundColor,
        waveCount = WaveCount,
        waveWidth = WaveWidth,
        speed = Speed,
        opacity = Opacity,
        targetFps = TargetFps
    };

    private string ComputeConfigHash()
    {
        var hash = new HashCode();
        foreach (var c in Colors) hash.Add(c);
        hash.Add(BackgroundColor);
        hash.Add(WaveCount);
        hash.Add(WaveWidth);
        hash.Add(Speed);
        hash.Add(Opacity);
        hash.Add(TargetFps);
        return hash.ToHashCode().ToString();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;

        _module = await JS.InvokeAsync<IJSObjectReference>(
            "import", "./_content/HeroWave/wavy-background.js");

        _instanceId = await _module.InvokeAsync<string>("init", _canvas, BuildConfig());
        _previousConfigHash = ComputeConfigHash();
    }

    protected override async Task OnParametersSetAsync()
    {
        // OnParametersSetAsync fires BEFORE OnAfterRenderAsync on first render,
        // so _module/_instanceId will be null during initialisation.
        // The guard ensures we only call update() on subsequent parameter changes.

        // Clamp TargetFps to valid range
        TargetFps = Math.Max(1, Math.Min(TargetFps, 120));

        if (_module is null || _instanceId is null) return;

        var currentHash = ComputeConfigHash();
        if (currentHash == _previousConfigHash) return;
        _previousConfigHash = currentHash;

        try
        {
            await _module.InvokeVoidAsync("update", _instanceId, BuildConfig());
        }
        catch (JSDisconnectedException) { }
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null && _instanceId is not null)
        {
            try
            {
                await _module.InvokeVoidAsync("dispose", _instanceId);
            }
            catch (JSDisconnectedException) { }
        }

        if (_module is not null)
        {
            await _module.DisposeAsync();
        }
    }
}
