using Godot;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Widgets;

/// <summary>
/// The best ever on this map, marked in the Training arena at its shown distance (#388, #725): a dashed line up from
/// the ground, the node's origin, to a flag near the top of the view that reads "Best 4.2 m". It
/// stands at that distance on the ruler, behind every creature, and keeps its screen size at any
/// zoom. Hidden until its distance is known; it jumps when a generation's front goes past it.
/// </summary>
public partial class ArenaBestMarker : ArenaMark
{
    // The reference flag: 24 high, 8 padding each side of the text, a notch a quarter deep.
    private const float _flagTop = UiSize.Space.S3;
    private const float _flagHeight = UiSize.Control.ExtraSmall;
    private const float _flagPadding = UiSize.Space.S2;
    private const float _notchDepth = _flagHeight / 4;
    private const float _dash = 4f;

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
        var flagTop = view.Position.Y + (_flagTop * screenScale);
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
                pen.DashedLine(new Vector2(0, groundTop), new Vector2(0, _flagHeight), Theme.MarkerInk, Theme.MarkerLineWidth, _dash);
            }

            pen.Polygon(flag[..^1], Theme.MarkerFill);
            pen.Polyline(flag, Theme.MarkerInk, Theme.MarkerLineWidth);
        }

        // The pen leaves flagTransform set, so the label is drawn in flag space.
        var font = Theme.MarkerFont;
        var size = Theme.MarkerFontSize;
        var baseline = ((_flagHeight - font.GetHeight(size)) / 2) + font.GetAscent(size);
        DrawString(font, new Vector2(_flagPadding, baseline), _text, HorizontalAlignment.Left, -1, size, Theme.MarkerInk);
        DrawSetTransformMatrix(Transform2D.Identity);
    }

    protected override void OnChanged() => QueueRedraw();
}
