using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Alpha helpers for theme colors. These are plain color math, not theme lookups.</summary>
public static class UiColorExtensions
{
    public static Color WithAlpha(this Color color, float alpha) =>
        new(color.R, color.G, color.B, alpha);

    public static Color ScaleAlpha(this Color color, float factor) =>
        new(color.R, color.G, color.B, color.A * factor);
}
