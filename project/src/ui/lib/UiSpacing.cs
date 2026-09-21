using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>
/// Reference-design spacing rules for the fixed 640 x 360 phone canvas.
/// Use these names instead of one-off pixel values when composing UI.
/// </summary>
public static class UiSpacing
{
    public static int IconLabelGap(UiTokens tokens) => (int)tokens.Space1;

    public static int ControlGap(UiTokens tokens) => (int)tokens.Space2;

    public static int PanelPadding(UiTokens tokens) => (int)tokens.Space3;

    public static int PanelGap(UiTokens tokens) => (int)tokens.Space4;

    public static int SectionGap(UiTokens tokens) => (int)tokens.Space5;

    public static int ScreenEdgeInset(UiTokens tokens) => (int)tokens.Space2;

    public static int TouchTarget(UiTokens tokens) => (int)tokens.TouchTarget;

    public static int ControlHorizontalPadding(UiTokens tokens) => (int)tokens.Space4;

    public static int SegmentedControlHorizontalPadding(UiTokens tokens) => (int)tokens.Space3;

    public static int ControlVerticalPadding(UiTokens tokens) => (int)tokens.Space2;

    public static int FocusRingGap(UiTokens tokens) => 2;

    public static int DenseStackGap(UiTokens tokens) => IconLabelGap(tokens);

    public static int StackGap(UiTokens tokens) => ControlGap(tokens);

    public static void ApplyUniformMargin(MarginContainer margin, int value)
    {
        margin.AddThemeConstantOverride("margin_left", value);
        margin.AddThemeConstantOverride("margin_top", value);
        margin.AddThemeConstantOverride("margin_right", value);
        margin.AddThemeConstantOverride("margin_bottom", value);
    }
}
