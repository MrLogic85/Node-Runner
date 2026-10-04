using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>
/// One square cell of a bar strip (reference <c>GenerationStrip</c>): a button that shows a bar
/// rising from its bottom, <see cref="Fill"/> of the way up, or an <see cref="IconId"/> instead.
/// The <see cref="Selected"/> cell's bar and frame are <c>accent</c>, the rest <c>line-strong</c>. Cells sit in a row
/// the scene authors; Godot's <see cref="BaseButton"/> handles the press.
/// </summary>
[Tool]
[GlobalClass]
public partial class UiBarCell : BaseButton
{
    private const float _disabledOpacity = 0.5f;
    private static readonly UiCorners _corners = UiCorners.Uniform(UiSize.Radius.Small);
    private float _fill;
    private bool _selected;
    private UiIconId _iconId = UiIconId.None;

    public UiBarCell() => TextureFilter = UiIcons.IconFilter;

    public override void _ValidateProperty(Godot.Collections.Dictionary property) => UiIcons.HideIconFilter(property);

    /// <summary>How far up the bar reaches, from 0 (none) to 1 (the cell's inner height).</summary>
    [Export(PropertyHint.Range, "0,1,0.01")]
    public float Fill
    {
        get => _fill;
        set
        {
            var fill = Mathf.Clamp(value, 0, 1);
            if (fill == _fill)
            {
                return;
            }

            _fill = fill;
            QueueRedraw();
        }
    }

    /// <summary>Marks the row's one chosen cell: bar and frame in <c>accent</c>.</summary>
    [Export]
    public bool Selected
    {
        get => _selected;
        set
        {
            if (value == _selected)
            {
                return;
            }

            _selected = value;
            QueueRedraw();
        }
    }

    /// <summary>An icon in place of the bar, for a cell that acts on the row (sort, page).</summary>
    [Export]
    public UiIconId IconId
    {
        get => _iconId;
        set
        {
            if (value == _iconId)
            {
                return;
            }

            _iconId = value;
            QueueRedraw();
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationThemeChanged)
        {
            QueueRedraw();
        }
    }

    public override Vector2 _GetMinimumSize() => Vector2.One * UiSize.Control.Default;

    public override void _Draw()
    {
        var opacity = Disabled ? _disabledOpacity : 1;
        var rect = new Rect2(Vector2.Zero, Size);
        var frame = UiThemeLookup.CreateStyleBox(
            UiThemeLookup.Color(this, UiTokens.Color.Panel).ScaleAlpha(opacity),
            UiThemeLookup.Color(this, _selected ? UiTokens.Color.Accent : UiTokens.Color.LineStrong).ScaleAlpha(opacity),
            _selected ? UiSize.Stroke.Signal : UiSize.Stroke.Hair,
            UiSize.Radius.Small);
        DrawStyleBox(frame, rect);

        if (_iconId != UiIconId.None)
        {
            DrawIcon(opacity);
        }
        else
        {
            DrawBar(opacity);
        }

        if (UiPressFeedback.Shows(this, selected: false))
        {
            UiPressFeedback.Draw(this, _corners, UiTokens.Color.Panel, danger: false);
        }
    }

    private void DrawBar(float opacity)
    {
        const float inset = UiSize.Space.S1;
        var height = (Size.Y - (2 * inset)) * _fill;
        if (height <= 0)
        {
            return;
        }

        var bar = new Rect2(inset, Size.Y - inset - height, Size.X - (2 * inset), height);
        var color = UiThemeLookup.Color(this, _selected ? UiTokens.Color.Accent : UiTokens.Color.LineStrong);
        _corners.Fill(this, bar, color.ScaleAlpha(opacity));
    }

    private void DrawIcon(float opacity)
    {
        var size = UiIcons.Pixels(UiIconSize.Large);
        var icon = new Rect2((Size - (Vector2.One * size)) * 0.5f, Vector2.One * size);
        DrawTextureRect(
            UiIcons.Load(_iconId, UiIconSize.Large),
            icon,
            false,
            UiThemeLookup.Color(this, UiTokens.Color.Muted).ScaleAlpha(opacity));
    }
}
