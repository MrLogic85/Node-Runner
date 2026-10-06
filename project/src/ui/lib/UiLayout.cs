using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>
/// Reference-design layout metrics that C# reads, for the 640 x 360 reference canvas.
/// The canvas grows past that to fit the screen's shape and down to it as the UI size grows
/// (<see cref="UiScale"/>); see "Screen size and safe area" and "UI size" in docs/UI_DIRECTION.md.
/// Widths a scene authors itself (dialogs, cards, figures) stay in the scene; see
/// "Reference token mapping deviations" in docs/UI_DIRECTION.md.
/// Component-scale values live in <see cref="UiSize"/>.
/// </summary>
public static class UiLayout
{
    public const int CanvasWidth = 640;
    public const int CanvasHeight = 360;

    /// <summary>The side panel's width (the reference token <c>w-side</c>).</summary>
    public const int SidePanelWidth = 176;

    /// <summary>
    /// The collapsed side panel's tab. Not a reference token: the reference hardcodes 28px.
    /// </summary>
    public const int SidePanelTabWidth = 28;

    public const int MenuWidth = 200;
    public const int ColumnSmallWidth = 52;

    public static void ApplyScreen(Control root)
    {
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.Size = root.GetViewportRect().Size;
    }
}
