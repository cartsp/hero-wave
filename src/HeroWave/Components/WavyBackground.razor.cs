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
    /// Apply a named preset configuration. Individual parameters below override
    /// the preset values when explicitly set.
    /// </summary>
    /// <example>
    /// <code>&lt;WavyBackground Preset="WavePresets.OceanAurora" Speed="0.008" /&gt;</code>
    /// </example>
    [Parameter] public WavePresetConfig? Preset { get; set; }

    private string[]? _colorsOverride;

    /// <summary>
    /// CSS color strings for each wave, cycled if fewer than <see cref="WavePresetConfig.WaveCount"/>.
    /// When set, overrides the preset value.
    /// </summary>
    [Parameter]
    public string[] Colors
    {
        get => _colorsOverride ?? Preset?.Colors ?? Defaults.Colors;
        set => _colorsOverride = value;
    }

    private string? _backgroundColorOverride;

    /// <summary>
    /// Background color of the canvas. When set, overrides the preset value.
    /// </summary>
    [Parameter]
    public string BackgroundColor
    {
        get => _backgroundColorOverride ?? Preset?.BackgroundColor ?? Defaults.BackgroundColor;
        set => _backgroundColorOverride = value;
    }

    private int? _waveCountOverride;

    /// <summary>
    /// Number of wave lines to render. When set, overrides the preset value.
    /// </summary>
    [Parameter]
    public int WaveCount
    {
        get => _waveCountOverride ?? Preset?.WaveCount ?? Defaults.WaveCount;
        set => _waveCountOverride = value;
    }

    private int? _waveWidthOverride;

    /// <summary>
    /// Base stroke width of each wave in CSS pixels. When set, overrides the preset value.
    /// </summary>
    [Parameter]
    public int WaveWidth
    {
        get => _waveWidthOverride ?? Preset?.WaveWidth ?? Defaults.WaveWidth;
        set => _waveWidthOverride = value;
    }

    private double? _speedOverride;

    /// <summary>
    /// Animation speed — higher values produce faster waves. When set, overrides the preset value.
    /// </summary>
    [Parameter]
    public double Speed
    {
        get => _speedOverride ?? Preset?.Speed ?? Defaults.Speed;
        set => _speedOverride = value;
    }

    private double? _opacityOverride;

    /// <summary>
    /// Wave opacity multiplier (0.0 – 1.0). When set, overrides the preset value.
    /// </summary>
    [Parameter]
    public double Opacity
    {
        get => _opacityOverride ?? Preset?.Opacity ?? Defaults.Opacity;
        set => _opacityOverride = value;
    }

    /// <summary>
    /// Optional CSS class applied to the text overlay container.
    /// </summary>
    [Parameter] public string? CssClass { get; set; }

    private ElementReference _canvas;
    private IJSObjectReference? _module;
    private string? _instanceId;

    private static readonly WavePresetConfig Defaults = new();

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;

        _module = await JS.InvokeAsync<IJSObjectReference>(
            "import", "./_content/HeroWave/wavy-background.js");

        var config = new
        {
            colors = Colors,
            backgroundColor = BackgroundColor,
            waveCount = WaveCount,
            waveWidth = WaveWidth,
            speed = Speed,
            opacity = Opacity
        };

        _instanceId = await _module.InvokeAsync<string>("init", _canvas, config);
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is null) return;

        if (_instanceId is not null)
        {
            try { await _module.InvokeVoidAsync("dispose", _instanceId); }
            catch (JSDisconnectedException) { }
        }

        await _module.DisposeAsync();
    }
}
