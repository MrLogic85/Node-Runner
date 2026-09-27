namespace NodeRunner.Ui.Lib;

/// <summary>
/// Reference-design dimensions from <c>reference design/tokens.json</c>.
/// These are theme-independent: Neon, Paper, and Effects Lite share every value,
/// so they are plain constants rather than Godot Theme entries. Keeping them out
/// of the Theme also keeps fractional values exact, because Godot rounds theme
/// constants to integers.
/// </summary>
public static class UiSize
{
    /// <summary>Spacing scale (<c>space-1</c>..<c>space-5</c>).</summary>
    public static class Space
    {
        public const int S1 = 4;
        public const int S2 = 8;
        public const int S3 = 12;
        public const int S4 = 16;
        public const int S5 = 24;
    }

    /// <summary>Control heights (<c>control-xs</c>..<c>touch</c>).</summary>
    public static class Control
    {
        public const int ExtraSmall = 24;
        public const int Small = 32;
        public const int Default = 40;
        public const int Touch = 48;
    }

    /// <summary>Icon box sizes (<c>icon-sm</c>..<c>icon-xl</c>).</summary>
    public static class Icon
    {
        public const int Small = 12;
        public const int Default = 16;
        public const int Large = 20;
        public const int ExtraLarge = 24;
    }

    /// <summary>Corner radii (<c>radius-sm</c>..<c>radius-pill</c>).</summary>
    public static class Radius
    {
        public const int Small = 4;
        public const int Medium = 8;
        public const int Large = 12;
        public const int Pill = 999;
    }

    /// <summary>Stroke widths (<c>stroke-hair</c>, <c>stroke-signal</c>, <c>stroke-beam</c>).</summary>
    public static class Stroke
    {
        public const int Hair = 1;
        public const int Signal = 2;
        public const int Beam = 3;
        public const int ButtonSelected = 2;
        public const int SelectionHandle = 2;
        public const int InfoRing = 2;
        public const float Number = 1.5f;
    }

    /// <summary>Dimensions owned by a single component rather than the token scale.</summary>
    public static class Widget
    {
        public const int BadgeMinimumSize = 16;
        public const int BadgeOffset = 4;
        public const int NumberDiameter = 16;
        public const int SelectionHandleSize = 44;
        public const int SelectionHandleRadius = 13;
        public const int SliderThumbDiameter = 18;
        public const int SliderTrackWidth = 4;
        public const int SliderMarkerHeight = 16;
        public const int SliderStepTickHeight = 10;
        public const int SliderDisabledDashLength = 4;
        public const int SliderSteppedHeight = 60;
    }
}
