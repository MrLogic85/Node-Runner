using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>
/// Signpost (#848): a sign shaped like an arrow pointing right, on a short post down from the middle
/// of its body. The post's foot, <see cref="Foot"/>, is where it stands. The outline, post and text
/// take <see cref="Color"/>, a text colour; the sign is filled with <c>panel</c>.
/// </summary>
[Tool]
[GlobalClass]
public partial class UiSignpost : Container
{
    // The best flag's measures (ArenaBestMarker): 24 high, 8 padding each side of the text.
    private const float _signHeight = UiSize.Control.ExtraSmall;
    private const float _padding = UiSize.Space.S2;
    private const float _arrowDepth = _signHeight / 2;
    private const float _postHeight = UiSize.Control.ExtraSmall;
    private const float _lineWidth = UiSize.Stroke.Marker;

    private string _text = string.Empty;
    private UiTokens.Color _color = UiTokens.Color.Ink;
    private UiIconCaption? _content;

    public UiSignpost() => MouseFilter = MouseFilterEnum.Ignore;

    [Export]
    public string Text
    {
        get => _text;
        set
        {
            if (_text == value)
            {
                return;
            }

            _text = value;
            Refresh();
        }
    }

    [Export]
    public UiTokens.Color Color
    {
        get => _color;
        set
        {
            if (!UiTokens.IsTextColor(value))
            {
                GD.PushError($"Invalid signpost color: {value}. Keeping {_color}.");
                return;
            }

            _color = value;
            Refresh();
        }
    }

    /// <summary>The bottom of the post, in this control's coordinates: where the signpost stands.</summary>
    public Vector2 Foot => new(BodyWidth / 2, Size.Y);

    private float BodyWidth => Size.X - _arrowDepth;

    public override void _EnterTree() => RequestReady();

    public override void _Ready() => Refresh();

    public override void _Notification(int what)
    {
        if (what == NotificationSortChildren && _content is { } content)
        {
            var caption = content.Row.GetCombinedMinimumSize();
            FitChildInRect(content.Row, new Rect2(_padding, (_signHeight - caption.Y) / 2, caption.X, caption.Y));
        }
    }

    public override Vector2 _GetMinimumSize()
    {
        var caption = _content?.Row.GetCombinedMinimumSize() ?? Vector2.Zero;
        return new Vector2((_padding * 2) + caption.X + _arrowDepth, _signHeight + _postHeight);
    }

    public override void _Draw()
    {
        var body = BodyWidth;
        Vector2[] sign =
        [
            new(0, 0),
            new(body, 0),
            new(body + _arrowDepth, _signHeight / 2),
            new(body, _signHeight),
            new(0, _signHeight),
            new(0, 0),
        ];
        var ink = UiThemeLookup.Color(this, _color);
        using var pen = UiPixelPen.Begin(this);
        pen.Line(new Vector2(body / 2, _signHeight), Foot, ink, _lineWidth);
        pen.Polygon(sign[..^1], UiThemeLookup.Color(this, UiTokens.Color.Panel));
        pen.Polyline(sign, ink, _lineWidth);
    }

    private void Refresh()
    {
        if (!IsInsideTree())
        {
            return;
        }

        // Re-found after a C# assembly reload, which clears managed fields but keeps the child.
        _content ??= UiIconCaption.Ensure(this);
        UiTranslation.ShareContext(this, _content.Label);
        _content.Apply(Text, UiIconId.None, UiIconSize.Small, _color, UiThemeLookup.Color(this, _color));
        UpdateMinimumSize();
        QueueSort();
        QueueRedraw();
    }
}
