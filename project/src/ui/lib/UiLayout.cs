using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>
/// Shared reference-design layout metrics for the fixed 640 x 360 landscape shell.
/// Owns every shell and surface dimension; component-scale values live in <see cref="UiSize"/>.
/// </summary>
public static class UiLayout
{
    public const int CanvasWidth = 640;
    public const int CanvasHeight = 360;

    /// <summary>The top bar is exactly one touch target tall.</summary>
    public const int TopBarHeight = UiSize.Control.Touch;

    /// <summary>Remaining height below the top bar (<c>h-screen</c>).</summary>
    public const int ScreenBodyHeight = CanvasHeight - TopBarHeight;

    /// <summary>The button bar fits one touch target plus its inset (the reference token <c>w-rail</c>).</summary>
    public const int ButtonBarWidth = UiSize.Control.Touch + UiSize.Space.S2;

    public const int SidePanelWidth = 176;
    public const int MenuWidth = 200;
    public const int DialogWidth = 300;
    public const int BrainWidth = 460;
    public const int CardWidth = 326;
    public const int TileWidth = 156;
    public const int WellWidth = 250;
    public const int SheetWidth = 720;
    public const int SheetWideWidth = 880;
    public const int StageHeight = 170;
    public const int ThumbnailHeight = 100;
    public const int ColumnExtraSmallWidth = 40;
    public const int ColumnSmallWidth = 52;
    public const int ColumnMediumWidth = 76;
    public const int ColumnLargeWidth = 96;
    public const int ColumnExtraLargeWidth = 128;

    public static Vector2 CanvasSize { get; } = new(CanvasWidth, CanvasHeight);

    public static void ApplyScreen(Control root)
    {
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.Size = root.GetViewportRect().Size;
        root.CustomMinimumSize = CanvasSize;
    }

    public static void ApplyMargins(MarginContainer margin)
    {
        UiSpacing.ApplyUniformMargin(margin, UiSpacing.ScreenEdgeInset);
    }
}
