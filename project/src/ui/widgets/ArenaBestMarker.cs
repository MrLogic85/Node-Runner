using Godot;
using NodeRunner.Theme;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Widgets;

/// <summary>
/// The best ever on this map, marked in the Training arena at its shown distance (#388, #725): a dashed line up from
/// the ground, the node's origin, to a flag near the top of the view that reads "Best 4.2 m". It
/// stands at that distance on the ruler, behind every creature, and keeps its screen size at any
/// zoom. Hidden until its distance is known; it jumps when a generation's front goes past it. It fades back
/// like a shadow while a part's name is shown, which may cover it.
/// </summary>
public partial class ArenaBestMarker : Node2D
{
    // The reference flag: 24 high, 8 padding each side of the text, a notch a quarter deep.
    private const float _flagTop = UiSize.Space.S3;
    private const float _flagHeight = UiSize.Control.ExtraSmall;
    private const float _flagPadding = UiSize.Space.S2;
    private const float _notchDepth = _flagHeight / 4;
    private const float _dash = 4f;

    private VisualTheme _theme = VisualTheme.Neon;
    private double _startX;
    private double _distance = double.NegativeInfinity;
    private string? _text;
    private Rect2 _drawnView;

    public VisualTheme Theme
    {
        get => _theme;
        set
        {
            _theme = value;
            QueueRedraw();
        }
    }

    /// <summary>Drawn at the shadows' opacity, behind what is in focus.</summary>
    public bool Faded
    {
        get => Modulate.A < 1f;
        set => Modulate = Colors.White with { A = value ? _theme.ShadowAlpha : 1f };
    }

    /// <summary>Where 0 m is, in this node's coordinates: the ruler's start.</summary>
    public double StartX
    {
        get => _startX;
        set
        {
            _startX = value;
            QueueRedraw();
        }
    }

    /// <summary>Marks <paramref name="distance"/> world units past the start; a null text hides it.</summary>
    public void Show(double distance, string? text)
    {
        if (distance.Equals(_distance) && text == _text)
        {
            return;
        }

        _distance = distance;
        _text = text;
        QueueRedraw();
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
        // World units per UI unit: the arena view undoes the UI size (#738), so this applies it again.
        var screenScale = view.Size.X / GetViewportRect().Size.X * UiScale.FactorOf(this);
        var textWidth = _theme.MarkerFont.GetStringSize(_text, HorizontalAlignment.Left, -1, _theme.MarkerFontSize).X;
        var flagWidth = (_flagPadding * 2) + textWidth + _notchDepth;
        var x = (float)(_startX + _distance);
        if (x + (flagWidth * screenScale) < view.Position.X || x > view.End.X)
        {
            return;
        }

        // Drawn at screen size from the flag's top left: scaled back up by as much as the camera zooms out.
        var flagTop = view.Position.Y + (_flagTop * screenScale);
        var flagTransform = new Transform2D(0f, new Vector2(screenScale, screenScale), 0f, new Vector2(x, flagTop));
        var groundTop = (-_theme.GroundEdgeWidth / 2 - flagTop) / screenScale;
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
                pen.DashedLine(new Vector2(0, groundTop), new Vector2(0, _flagHeight), _theme.MarkerInk, _theme.MarkerLineWidth, _dash);
            }

            pen.Polygon(flag[..^1], _theme.MarkerFill);
            pen.Polyline(flag, _theme.MarkerInk, _theme.MarkerLineWidth);
        }

        // The pen leaves flagTransform set, so the label is drawn in flag space.
        var font = _theme.MarkerFont;
        var size = _theme.MarkerFontSize;
        var baseline = ((_flagHeight - font.GetHeight(size)) / 2) + font.GetAscent(size);
        DrawString(font, new Vector2(_flagPadding, baseline), _text, HorizontalAlignment.Left, -1, size, _theme.MarkerInk);
        DrawSetTransformMatrix(Transform2D.Identity);
    }

    // What the camera shows, in this node's coordinates.
    private Rect2 VisibleArea() =>
        GetGlobalTransform().AffineInverse() * GetCanvasTransform().AffineInverse() * GetViewportRect();
}
