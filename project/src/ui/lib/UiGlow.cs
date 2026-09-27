using Godot;

namespace NodeRunner.Ui.Lib;

public static class UiGlow
{
    // Android-reviewed values. The reference `shadow.glow` CSS blur radius is
    // deliberately not imported; see "Reference token mapping deviations" in
    // docs/UI_DIRECTION.md.
    public const int Extent = 10;
    public const float Opacity = 0.12f;

    public static Color FromBase(Color color, bool enabled) =>
        enabled ? color.ScaleAlpha(Opacity) : Colors.Transparent;

    public static void ApplyToControl(StyleBoxFlat style, Color baseColor, bool enabled)
    {
        style.ShadowColor = FromBase(baseColor, enabled);
        style.ShadowSize = enabled ? Extent : 0;
    }
}
