namespace NodeRunner.Ui.Lib;

/// <summary>
/// A UI size the player picks (#738): Auto and Max follow the window, a fixed size is a percentage
/// on the <see cref="UiScale.StepPercent"/> grid. See "UI size" in docs/UI_DIRECTION.md.
/// </summary>
public readonly record struct UiSizeChoice
{
    public enum Mode
    {
        Auto,
        Max,
        Fixed,
    }

    private UiSizeChoice(Mode kind, int percent)
    {
        Kind = kind;
        Percent = percent;
    }

    public static UiSizeChoice Auto => default;

    public static UiSizeChoice Max => new(Mode.Max, 0);

    public Mode Kind { get; }

    /// <summary>The fixed size; 0 for Auto and Max.</summary>
    public int Percent { get; }

    public static UiSizeChoice Fixed(double percent) => new(Mode.Fixed, UiScale.Snap(percent));

    /// <summary>The size in effect on a screen with these limits, within <see cref="UiScale.MinPercent"/>..<paramref name="max"/>.</summary>
    public int Resolve(int auto, int max)
    {
        var top = Math.Max(UiScale.MinPercent, max);
        var wanted = Kind switch
        {
            Mode.Auto => auto,
            Mode.Max => top,
            _ => Percent,
        };
        return Math.Clamp(wanted, UiScale.MinPercent, top);
    }

    /// <summary>False for a fixed size above <paramref name="max"/>, which this screen cannot show.</summary>
    public bool Fits(int max) => Kind != Mode.Fixed || Percent <= Math.Max(UiScale.MinPercent, max);
}
