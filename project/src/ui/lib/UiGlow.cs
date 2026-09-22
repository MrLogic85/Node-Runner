using Godot;

namespace NodeRunner.Ui.Lib;

public static class UiGlow
{
    public const int ControlExtent = 12;
    public const float ControlOpacity = 0.4f;

    public static void ApplyToControl(StyleBoxFlat style, Color color, bool enabled)
    {
        style.ShadowColor = enabled
            ? UiTokens.MultiplyAlpha(color, ControlOpacity)
            : Colors.Transparent;
        style.ShadowSize = enabled ? ControlExtent : 0;
    }
}
