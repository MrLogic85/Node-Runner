using Godot;

namespace NodeRunner.Ui.Lib;

public static class UiGlow
{
    public const int Extent = 10;
    public const float Opacity = 0.12f;

    public static Color FromBase(Color color, bool enabled) =>
        enabled ? UiTokens.MultiplyAlpha(color, Opacity) : Colors.Transparent;

    public static void ApplyToControl(StyleBoxFlat style, Color baseColor, bool enabled)
    {
        style.ShadowColor = FromBase(baseColor, enabled);
        style.ShadowSize = enabled ? Extent : 0;
    }
}
