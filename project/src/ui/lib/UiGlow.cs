using Godot;

namespace NodeRunner.Ui.Lib;

public static class UiGlow
{
    // Existing controls pass a pre-composed glow token into ApplyToControl.
    // Keep their established footprint and opacity independent of button glows.
    public const int ControlExtent = 10;
    public const float ControlOpacity = 0.6f;
    public const int ButtonExtent = 12;
    public const float ButtonOpacity = 0.12f;
    public const int InsetExtent = 12;

    public static Color FromButtonBase(Color color, bool enabled) =>
        enabled ? UiTokens.MultiplyAlpha(color, ButtonOpacity) : Colors.Transparent;

    public static void ApplyToControl(StyleBoxFlat style, Color color, bool enabled)
    {
        style.ShadowColor = enabled
            ? UiTokens.MultiplyAlpha(color, ControlOpacity)
            : Colors.Transparent;
        style.ShadowSize = enabled ? ControlExtent : 0;
    }

    public static void ApplyButtonGlow(StyleBoxFlat style, Color baseColor, bool enabled)
    {
        style.ShadowColor = FromButtonBase(baseColor, enabled);
        style.ShadowSize = enabled ? ButtonExtent : 0;
    }
}
