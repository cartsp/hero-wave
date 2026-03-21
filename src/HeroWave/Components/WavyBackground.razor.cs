using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace HeroWave.Components;

public partial class WavyBackground : ComponentBase, IAsyncDisposable
{
    [Inject] private IJSRuntime JS { get; set; } = default!;

    [Parameter] public string? Title { get; set; }
    [Parameter] public string? Subtitle { get; set; }
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public string Height { get; set; } = "100vh";

    [Parameter] public string[] Colors { get; set; } =
        ["#38bdf8", "#818cf8", "#c084fc", "#e879f9", "#22d3ee"];

    [Parameter] public string BackgroundColor { get; set; } = "#0c0c14";
    [Parameter] public int WaveCount { get; set; } = 5;
    [Parameter] public int WaveWidth { get; set; } = 50;
    [Parameter] public double Speed { get; set; } = 0.004;
    [Parameter] public double Opacity { get; set; } = 0.5;
    [Parameter] public string? CssClass { get; set; }

    private ElementReference _canvas;
    private IJSObjectReference? _module;
    private string? _instanceId;

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
        if (_module is not null && _instanceId is not null)
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

        if (_module is not null)
        {
            await _module.DisposeAsync();
        }
    }
}
