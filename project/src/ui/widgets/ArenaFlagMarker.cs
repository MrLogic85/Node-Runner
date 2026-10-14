using Godot;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Widgets;

/// <summary>
/// A distance marked in the Training arena (#388, #725): a dashed line up from the ground, the
/// node's origin, to a flag near the top of the view, such as "Best 4.2 m" or, in <c>danger</c>,
/// "Crash 3.4 m" (#1257). It stands at that distance on the ruler, behind every creature, and keeps
/// its screen size at any zoom. Hidden until its distance is known. Each marker has its own
/// <see cref="Row"/>, so two flags at the same distance never cover each other. The flag always
/// flies right of its line and is cut off at the view's edge like the scales (owner, #1257).
/// </summary>
public partial class ArenaFlagMarker : ArenaMark
{
    // The reference flag: 24 high, 8 padding each side of the text, a notch a quarter deep.
    private const float _firstRowTop = UiSize.Space.S3;
    private const float _rowGap = UiSize.Space.S2;
    private const float _flagHeight = UiSize.Control.ExtraSmall;
    private const float _flagPadding = UiSize.Space.S2;
    private const float _notchDepth = _flagHeight / 4;
    private const float _dash = 4f;

    private int _row;
    private bool _danger;

    /// <summary>The flag's row from the view's top, 0 the first.</summary>
    [Export]
    public int Row
    {
        get => _row;
        set
        {
            _row = Math.Max(0, value);
            QueueRedraw();
        }
    }

    /// <summary>Whether the line, outline and text are <c>danger</c> instead of the marker ink.</summary>
    [Export]
    public bool Danger
    {
        get => _danger;
        set
        {
            _danger = value;
            QueueRedraw();
        }
    }

    /// <summary>How far down from the view's top this marker's flag reaches, in UI units.</summary>
    public float FlagBottom => FlagTop + _flagHeight;

    private float FlagTop => _firstRowTop + (_row * (_flagHeight + _rowGap));

    private Color Ink => _danger ? Theme.Danger : Theme.MarkerInk;

    private double _distance = double.NegativeInfinity;
    private string? _text;
    private Func<string>? _textSource;
    private Rect2 _drawnView;

    /// <summary>
    /// Marks <paramref name="distance"/> world units past the start with the flag's text, asked
    /// again when the language changes; a null text hides it.
    /// </summary>
    public void Show(double distance, Func<string>? text)
    {
        _textSource = text;
        var shown = text?.Invoke();
        if (distance.Equals(_distance) && shown == _text)
        {
            return;
        }

        _distance = distance;
        _text = shown;
        QueueRedraw();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationTranslationChanged && _textSource is not null)
        {
            _text = _textSource();
            QueueRedraw();
        }
    }

    public override void _Process(double delta)
    {
        var view = VisibleArea();
        if (view != _drawnView)
        {
            _drawnView = view;
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        if (_text is null || !double.IsFinite(_distance))
        {
            return;
        }

        var view = VisibleArea();
        var screenScale = ScreenScale(view);
        var textWidth = Theme.MarkerFont.GetStringSize(_text, HorizontalAlignment.Left, -1, Theme.MarkerFontSize).X;
        var flagWidth = (_flagPadding * 2) + textWidth + _notchDepth;
        var x = (float)(StartX + _distance);
        if (x + (flagWidth * screenScale) < view.Position.X || x > view.End.X)
        {
            return;
        }

        // Drawn at screen size from the flag's top left: scaled back up by as much as the camera zooms out.
        var flagTop = view.Position.Y + (FlagTop * screenScale);
        var flagTransform = new Transform2D(0f, new Vector2(screenScale, screenScale), 0f, new Vector2(x, flagTop));
        var groundTop = (-Theme.GroundEdgeWidth / 2 - flagTop) / screenScale;
        Vector2[] flag =
        [
            new(0, 0),
            new(flagWidth, 0),
            new(flagWidth - _notchDepth, _flagHeight / 2),
            new(flagWidth, _flagHeight),
            new(0, _flagHeight),
            new(0, 0),
        ];
        using (var pen = UiPixelPen.Begin(this, flagTransform))
        {
            if (groundTop > _flagHeight)
            {
                pen.DashedLine(new Vector2(0, groundTop), new Vector2(0, _flagHeight), Ink, Theme.MarkerLineWidth, _dash);
            }

            pen.Polygon(flag[..^1], Theme.MarkerFill);
            pen.Polyline(flag, Ink, Theme.MarkerLineWidth);
        }

        // The pen leaves flagTransform set, so the label is drawn in flag space.
        var font = Theme.MarkerFont;
        var size = Theme.MarkerFontSize;
        var baseline = ((_flagHeight - font.GetHeight(size)) / 2) + font.GetAscent(size);
        DrawString(font, new Vector2(_flagPadding, baseline), _text, HorizontalAlignment.Left, -1, size, Ink);
        DrawSetTransformMatrix(Transform2D.Identity);
    }

    protected override void OnChanged() => QueueRedraw();
}
