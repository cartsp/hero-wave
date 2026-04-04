using System.Text.Json.Serialization;

namespace HeroWave.Components;

/// <summary>
/// Strongly-typed configuration passed to the wavy-background JS module via JS interop.
/// Property names are serialized as camelCase to match the JavaScript API contract.
/// </summary>
public sealed record WavyBackgroundConfig
{
    /// <summary>
    /// Array of CSS color strings used for each wave.
    /// Colors are cycled if there are fewer colors than waves.
    /// </summary>
    [JsonPropertyName("colors")]
    public required string[] Colors { get; init; }

    /// <summary>
    /// Background color of the canvas.
    /// </summary>
    [JsonPropertyName("backgroundColor")]
    public required string BackgroundColor { get; init; }

    /// <summary>
    /// Number of wave lines to render.
    /// </summary>
    [JsonPropertyName("waveCount")]
    public required int WaveCount { get; init; }

    /// <summary>
    /// Base stroke width of each wave in CSS pixels.
    /// </summary>
    [JsonPropertyName("waveWidth")]
    public required int WaveWidth { get; init; }

    /// <summary>
    /// Animation speed. Higher values = faster waves.
    /// </summary>
    [JsonPropertyName("speed")]
    public required double Speed { get; init; }

    /// <summary>
    /// Wave opacity multiplier. Controls the overall visibility of the waves.
    /// </summary>
    [JsonPropertyName("opacity")]
    public required double Opacity { get; init; }

    /// <summary>
    /// Target frames per second for the animation loop.
    /// Clamped to the range 1–120.
    /// </summary>
    [JsonPropertyName("targetFps")]
    public required int TargetFps { get; init; }
}
