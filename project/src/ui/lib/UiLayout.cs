using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>
/// Shared reference-design layout metrics for the fixed 640 x 360 landscape shell.
/// </summary>
public static class UiLayout
{
    public const float CanvasWidth = UiTokens.LogicalCanvasWidth;
    public const float CanvasHeight = UiTokens.LogicalCanvasHeight;
    public const float TopBarHeight = 48;
    public const float LeftRailWidth = 56;
    public const float RightPanelWidth = 172;
    public const float RightPanelMinWidth = 168;
    public const float RightPanelMaxWidth = 176;
    public const float BottomStripHeight = 52;

    public static Vector2 CanvasSize { get; } = new(CanvasWidth, CanvasHeight);

    public static Rect2 ContentRect(UiTokens tokens)
    {
        var edgeInset = UiSpacing.ScreenEdgeInset(tokens);
        return new Rect2(edgeInset, TopBarHeight + edgeInset, CanvasWidth - (edgeInset * 2), CanvasHeight - TopBarHeight - (edgeInset * 2));
    }

    public static void ApplyScreen(Control root)
    {
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.Size = root.GetViewportRect().Size;
        root.CustomMinimumSize = CanvasSize;
    }

    public static void ApplyMargins(MarginContainer margin, UiTokens tokens)
    {
        UiSpacing.ApplyUniformMargin(margin, UiSpacing.ScreenEdgeInset(tokens));
    }
}
