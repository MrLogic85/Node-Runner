using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>
/// A small picture of the app in one palette: its background with a <c>line-strong</c> bar and a
/// shorter <c>accent</c> bar. It draws in <see cref="Choice"/>'s own palette, never the one it
/// inherits; <see cref="UiThemeChoice.Phone"/> draws Neon on the left half and Paper on the right.
/// Its top corners round by <see cref="TopRadius"/>, so it can sit flush in a card's top edge.
/// </summary>
[Tool]
[GlobalClass]
public partial class UiThemePreview : Control
{
    private UiThemeChoice _choice = UiThemeChoice.Neon;
    private float _topRadius;
    private readonly StyleBoxFlat _bar = new() { AntiAliasing = true };

    [Export]
    public UiThemeChoice Choice
    {
        get => _choice;
        set
        {
            _choice = value;
            QueueRedraw();
        }
    }

    /// <summary>The radius of the top corners, set by the card it sits flush in.</summary>
    public float TopRadius
    {
        get => _topRadius;
        set
        {
            _topRadius = value;
            QueueRedraw();
        }
    }

    public UiThemePreview()
    {
        UiMouseFilter.Prefer(this, MouseFilterEnum.Ignore);
        _bar.SetCornerRadiusAll(UiSize.Radius.Small);
    }

    public override Vector2 _GetMinimumSize() => new(0, UiSize.Widget.ThemePreviewHeight);

    public override void _Draw()
    {
        var whole = new Rect2(Vector2.Zero, Size);
        if (Choice == UiThemeChoice.Phone)
        {
            var half = Size.X / 2;
            DrawPicture(UiTokenType.Neon, whole, new Rect2(0, 0, half, Size.Y), new UiCorners(TopRadius, 0, 0, 0));
            DrawPicture(UiTokenType.Paper, whole, new Rect2(half, 0, Size.X - half, Size.Y), new UiCorners(0, TopRadius, 0, 0));
        }
        else
        {
            DrawPicture(Choice == UiThemeChoice.Paper ? UiTokenType.Paper : UiTokenType.Neon, whole, whole, UiCorners.Top(TopRadius));
        }
    }

    // Lays the picture out on the whole preview and draws the part inside visible.
    private void DrawPicture(UiTokenType palette, Rect2 whole, Rect2 visible, UiCorners corners)
    {
        var theme = UiThemes.For(palette);
        corners.Fill(this, visible, UiThemes.Color(theme, UiTokens.Color.Background));
        var inset = UiSize.Space.S2;
        var longBar = new Rect2(
            inset, UiSize.Widget.ThemePreviewLongBarTop, whole.Size.X - (2 * inset), UiSize.Widget.ThemePreviewBarHeight);
        var shortBar = new Rect2(
            inset, UiSize.Widget.ThemePreviewShortBarTop, UiSize.Widget.ThemePreviewShortBarWidth, UiSize.Widget.ThemePreviewBarHeight);
        DrawBar(longBar, visible, UiThemes.Color(theme, UiTokens.Color.LineStrong));
        DrawBar(shortBar, visible, UiThemes.Color(theme, UiTokens.Color.Accent));
    }

    // A bar cut by the edge of the visible part keeps its rounded corners only on the ends it keeps.
    private void DrawBar(Rect2 bar, Rect2 visible, Color color)
    {
        var shown = bar.Intersection(visible);
        if (shown.Size.X <= 0 || shown.Size.Y <= 0)
        {
            return;
        }

        var radius = UiSize.Radius.Small;
        var left = shown.Position.X > bar.Position.X ? 0 : radius;
        var right = shown.End.X < bar.End.X ? 0 : radius;
        _bar.BgColor = color;
        _bar.CornerRadiusTopLeft = left;
        _bar.CornerRadiusBottomLeft = left;
        _bar.CornerRadiusTopRight = right;
        _bar.CornerRadiusBottomRight = right;
        DrawStyleBox(_bar, shown);
    }
}
