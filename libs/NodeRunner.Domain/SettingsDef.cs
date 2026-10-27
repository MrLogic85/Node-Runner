using System.Text.Json.Serialization;

namespace NodeRunner.Domain;

/// <summary>The theme the player picks: Neon, Paper, or whichever the phone's dark or light mode gives.</summary>
public enum ThemeChoice
{
    Neon,
    Paper,
    UsePhone,
}

/// <summary>
/// The player's settings for this device. A number out of range is clamped or snapped, never
/// rejected, so a hand-edited file still loads (docs/SAVE_FORMAT.md → "<c>settings.json</c>").
/// </summary>
public sealed record SettingsDef
{
    /// <summary>The smallest UI size, in device pixels per canvas unit as a percentage; <c>UiScale.MinPercent</c>.</summary>
    public const int MinUiSizePercent = 50;

    /// <summary>The step between UI sizes; <c>UiScale.StepPercent</c>.</summary>
    public const int UiSizeStepPercent = 5;

    public static SettingsDef Default { get; } = new();

    /// <param name="uiSizePercent">The chosen UI size, or null for Auto, the screen's own.</param>
    /// <param name="theme">The chosen theme.</param>
    /// <param name="sounds">Whether sounds play.</param>
    /// <param name="defaultShadows">The Shadows a Creation that has not trained yet starts with.</param>
    /// <param name="gridSizeCm">The grid size in cm a new Creation starts with, one of <see cref="BuildGridDef.SizesCm"/>.</param>
    /// <param name="snapToGrid">Whether a new Creation starts with joints snapping to its grid.</param>
    public SettingsDef(
        int? uiSizePercent = null,
        ThemeChoice theme = ThemeChoice.UsePhone,
        bool sounds = true,
        int defaultShadows = TrainSettingsDef.DefaultShadows,
        int gridSizeCm = BuildGridDef.DefaultSizeCm,
        bool snapToGrid = false)
    {
        UiSizePercent = uiSizePercent;
        Theme = theme;
        Sounds = sounds;
        DefaultShadows = defaultShadows;
        GridSizeCm = gridSizeCm;
        SnapToGrid = snapToGrid;
    }

    /// <summary>
    /// The chosen UI size on the <see cref="UiSizeStepPercent"/> grid, at least
    /// <see cref="MinUiSizePercent"/>, or null for Auto. Where it is applied, a size above the
    /// device's largest is limited to it and kept.
    /// </summary>
    [JsonRequired]
    public int? UiSizePercent
    {
        get;
        init => field = value is { } percent ? SnapUiSize(percent) : null;
    }

    [JsonRequired]
    public ThemeChoice Theme
    {
        get;
        init => field = Enum.IsDefined(value)
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown theme.");
    }

    [JsonRequired]
    public bool Sounds { get; init; }

    /// <summary>Within <see cref="TrainSettingsDef.MinShadows"/>–<see cref="TrainSettingsDef.MaxShadows"/>.</summary>
    [JsonRequired]
    public int DefaultShadows
    {
        get;
        init => field = Math.Clamp(value, TrainSettingsDef.MinShadows, TrainSettingsDef.MaxShadows);
    }

    /// <summary>The nearest of <see cref="BuildGridDef.SizesCm"/>; a tie goes to the larger.</summary>
    [JsonRequired]
    public int GridSizeCm
    {
        get;
        init => field = BuildGridDef.NearestSizeCm(value);
    }

    [JsonRequired]
    public bool SnapToGrid { get; init; }

    /// <summary>The grid a new Creation starts with (docs/SAVE_FORMAT.md → "<c>creation.json</c>").</summary>
    [JsonIgnore]
    public BuildGridDef Grid => new(GridSizeCm, SnapToGrid);

    // The same grid as UiScale.Snap, which the UI library keeps apart from Domain. Halves round up.
    private static int SnapUiSize(int percent)
    {
        var steps = Math.Round((double)percent / UiSizeStepPercent, MidpointRounding.AwayFromZero);
        return Math.Max(MinUiSizePercent, (int)steps * UiSizeStepPercent);
    }
}
