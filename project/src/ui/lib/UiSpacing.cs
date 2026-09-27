using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>
/// Semantic spacing roles for the fixed 640 x 360 phone canvas. Use these names
/// instead of one-off pixel values or a raw <see cref="UiSize.Space"/> step, so the
/// intent of a gap stays visible at the call site.
/// </summary>
public static class UiSpacing
{
    public const int IconLabelGap = UiSize.Space.S1;
    public const int ControlGap = UiSize.Space.S2;
    public const int PanelPadding = UiSize.Space.S3;
    public const int PanelGap = UiSize.Space.S4;
    public const int SectionGap = UiSize.Space.S5;
    public const int ScreenEdgeInset = UiSize.Space.S2;
    public const int TouchTarget = UiSize.Control.Touch;
    public const int ControlHorizontalPadding = UiSize.Space.S4;
    public const int SegmentedControlHorizontalPadding = UiSize.Space.S3;
    public const int ControlVerticalPadding = UiSize.Space.S2;
    public const int DenseStackGap = IconLabelGap;
    public const int StackGap = ControlGap;

    public static void ApplyUniformMargin(MarginContainer margin, int value)
    {
        margin.AddThemeConstantOverride("margin_left", value);
        margin.AddThemeConstantOverride("margin_top", value);
        margin.AddThemeConstantOverride("margin_right", value);
        margin.AddThemeConstantOverride("margin_bottom", value);
    }
}
