namespace HeroWave.Components;

/// <summary>
/// Predefined wave configuration with curated colors and settings.
/// Use with <see cref="WavyBackground.Preset"/> and override individual
/// component parameters as needed.
/// </summary>
/// <example>
/// <code>
/// &lt;WavyBackground Preset="WavePresets.OceanAurora" Speed="0.008" /&gt;
/// </code>
/// </example>
public record WavePresetConfig
{
    /// <summary>
    /// CSS color strings for each wave. Cycled if fewer than <see cref="WaveCount"/>.
    /// </summary>
    public string[] Colors { get; init; } = ["#38bdf8", "#818cf8", "#c084fc", "#e879f9", "#22d3ee"];

    /// <summary>
    /// Background color of the canvas.
    /// </summary>
    public string BackgroundColor { get; init; } = "#0c0c14";

    /// <summary>
    /// Number of wave lines to render.
    /// </summary>
    public int WaveCount { get; init; } = 5;

    /// <summary>
    /// Base stroke width of each wave in CSS pixels.
    /// </summary>
    public int WaveWidth { get; init; } = 50;

    /// <summary>
    /// Animation speed. Higher values = faster waves.
    /// </summary>
    public double Speed { get; init; } = 0.004;

    /// <summary>
    /// Wave opacity multiplier (0.0 – 1.0).
    /// </summary>
    public double Opacity { get; init; } = 0.5;
}

/// <summary>
/// Named presets with curated color palettes.
/// </summary>
public static class WavePresets
{
    /// <summary>Default HeroWave palette — blue, purple, pink, cyan.</summary>
    public static WavePresetConfig Default { get; } = new();

    /// <summary>Cool blues and greens — oceanic aurora feel.</summary>
    public static WavePresetConfig OceanAurora { get; } = new()
    {
        Colors = ["#0ea5e9", "#06b6d4", "#14b8a6", "#10b981", "#34d399"],
        BackgroundColor = "#021a2b",
        WaveCount = 6,
        WaveWidth = 60,
        Opacity = 0.6,
    };

    /// <summary>Warm oranges, reds, and pinks — fiery sunset tones.</summary>
    public static WavePresetConfig SunsetFire { get; } = new()
    {
        Colors = ["#f97316", "#ef4444", "#ec4899", "#f59e0b", "#fb923c"],
        BackgroundColor = "#1a0a00",
        Speed = 0.008,
        Opacity = 0.55,
    };

    /// <summary>Electric high-contrast neon on dark background.</summary>
    public static WavePresetConfig NeonCyberpunk { get; } = new()
    {
        Colors = ["#ff00ff", "#00ffff", "#39ff14", "#ff3131"],
        BackgroundColor = "#0a0a0a",
        WaveCount = 4,
        WaveWidth = 35,
        Speed = 0.008,
        Opacity = 0.7,
    };

    /// <summary>Subtle white and silver on dark blue — minimal and clean.</summary>
    public static WavePresetConfig MinimalFrost { get; } = new()
    {
        Colors = ["#e2e8f0", "#94a3b8", "#cbd5e1", "#f1f5f9"],
        BackgroundColor = "#0f172a",
        WaveCount = 3,
        WaveWidth = 70,
        Opacity = 0.3,
    };

    /// <summary>Purples, greens, and ethereal blues — northern lights atmosphere.</summary>
    public static WavePresetConfig NorthernLights { get; } = new()
    {
        Colors = ["#a855f7", "#6366f1", "#22d3ee", "#4ade80", "#818cf8"],
        BackgroundColor = "#0c0720",
        WaveCount = 7,
        WaveWidth = 55,
    };

    /// <summary>Luxurious golds and ambers on deep warm dark.</summary>
    public static WavePresetConfig MoltenGold { get; } = new()
    {
        Colors = ["#fbbf24", "#f59e0b", "#d97706", "#b45309", "#fcd34d"],
        BackgroundColor = "#1c1208",
        Speed = 0.008,
        Opacity = 0.45,
    };
}
