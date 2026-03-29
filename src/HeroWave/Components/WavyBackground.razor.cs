using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace HeroWave.Components;

/// <summary>
/// Controls how the wave animation behaves with respect to reduced-motion preferences.
/// </summary>
public enum ReducedMotionBehavior
{
    /// <summary>
    /// Automatically pauses animation when the user's OS/browser requests reduced motion.
    /// </summary>
    RespectSystemPreference,

    /// <summary>
    /// Always animate regardless of system preference.
    /// </summary>
    AlwaysAnimate,

    /// <summary>
    /// Never animate; render a single static frame.
    /// </summary>
    AlwaysStatic
}

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
    /// Optional CSS class applied to the text overlay container.
    /// </summary>
    [Parameter] public string? CssClass { get; set; }

    /// <summary>
    /// Controls animation behavior for users who prefer reduced motion.
    /// Defaults to <see cref="ReducedMotionBehavior.RespectSystemPreference"/>.
    /// </summary>
    [Parameter] public ReducedMotionBehavior ReducedMotion { get; set; } = ReducedMotionBehavior.RespectSystemPreference;

    private ElementReference _canvas;
    private IJSObjectReference? _module;
    private string? _instanceId;
    private ReducedMotionBehavior _lastReducedMotion;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _module = await JS.InvokeAsync<IJSObjectReference>(
                "import", "./_content/HeroWave/wavy-background.js");

            var config = new
            {
                colors = Colors,
                backgroundColor = BackgroundColor,
                waveCount = WaveCount,
                waveWidth = WaveWidth,
                speed = Speed,
                opacity = Opacity,
                reducedMotion = MapReducedMotion(ReducedMotion)
            };

            _instanceId = await _module.InvokeAsync<string>("init", _canvas, config);
            _lastReducedMotion = ReducedMotion;
        }
        else if (_module is not null && ReducedMotion != _lastReducedMotion)
        {
            await _module.InvokeVoidAsync("update", _instanceId, new { reducedMotion = MapReducedMotion(ReducedMotion) });
            _lastReducedMotion = ReducedMotion;
        }
    }

    private static string MapReducedMotion(ReducedMotionBehavior behavior) => behavior switch
    {
        ReducedMotionBehavior.AlwaysAnimate => "alwaysAnimate",
        ReducedMotionBehavior.AlwaysStatic => "alwaysStatic",
        _ => "respectSystemPreference"
    };

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            if (_instanceId is not null)
            {
                try
                {
                    await _module.InvokeVoidAsync("dispose", _instanceId);
                }
                catch (JSDisconnectedException)
                {
                    // Circuit may already be gone during app shutdown
                }
            }
            await _module.DisposeAsync();
        }
    }
}
