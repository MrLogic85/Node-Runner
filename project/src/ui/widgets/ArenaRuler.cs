using System.Globalization;
using Godot;
using NodeRunner.App.ViewModels;
using NodeRunner.Theme;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Widgets;

/// <summary>
/// The ground line and the Training arena's two scales (#668, #1170), from <see cref="DistanceRuler"/>.
/// The line runs along the node's origin, as wide as the world zooms it; the pixel pen feathers it,
/// so a thin line does not flicker between pixel rows (#1173). The distance scale runs along the
/// view's bottom edge, its ticks rising from the edge and labels above them; the height scale runs
/// up the left edge from the ground's top (0 m), its ticks reaching in and labels sitting on them, and
/// stops above the distance scale's band, so the corner stays clear. Both share one label step, so
/// a metre reads the same either way. It draws only what the camera shows and redraws when the view
/// moves. Ticks and labels keep their screen size at any zoom; every metre is labelled, or every 2,
/// 5, 10, … when labels would overlap (#675), and from 5 m between labels the ticks thin out with
/// them (#884).
/// </summary>
public partial class ArenaRuler : Node2D
{
    // The reference scale: an 8-long tick each metre, 4-long between, labels S1 past the long ticks.
    private const float _majorTickLength = 8f;
    private const float _minorTickLength = 4f;
    private const float _tickToLabel = UiSize.Space.S1;

    // Clear space above the distance labels, inside the band the camera keeps the ground out of.
    private const float _bandPadding = UiSize.Space.S1;

    // A label fades out over this distance as it nears an edge, so none shows cut in half.
    private const float _labelFade = UiSize.Space.S4;

    // The least clear space between two labels, on screen.
    private const float _labelSpacing = UiSize.Space.S4;

    private const int _minLabelDigits = 2;

    // DistanceRuler.Fill labels nothing farther out than this many digits of metres.
    private const int _maxLabelDigits = 16;

    private readonly List<RulerMark> _marks = [];
    private readonly List<RulerMark> _heightMarks = [];
    private VisualTheme _theme = VisualTheme.Neon;
    private double _startX;
    private Rect2 _drawnView;
    private int _metresPerLabel = 1;
    private float _topClear;

    public VisualTheme Theme
    {
        get => _theme;
        set
        {
            _theme = value;
            QueueRedraw();
        }
    }

    /// <summary>Where 0 m is, in this node's coordinates: the trial's start.</summary>
    public double StartX
    {
        get => _startX;
        set
        {
            _startX = value;
            QueueRedraw();
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationTranslationChanged)
        {
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

    /// <summary>
    /// How far down from the view's top the height labels keep clear, in UI units, for what the
    /// arena shows along its top, such as the slow-motion chip and the best flag.
    /// </summary>
    public float TopClear
    {
        get => _topClear;
        set
        {
            _topClear = value;
            QueueRedraw();
        }
    }

    /// <summary>
    /// The share of the view's height the distance scale takes along the bottom; the camera keeps
    /// the ground line above it while it makes room for a part callout (#1170).
    /// </summary>
    public double BandShare
    {
        get
        {
            var height = GetViewportRect().Size.Y;
            return height > 0 ? BandHeight() * UiScale.FactorOf(this) / height : 0;
        }
    }

    public override void _Draw()
    {
        var view = VisibleArea();
        // World units per UI unit: the arena view undoes the UI size (#738), so this applies it again.
        var screenScale = view.Size.X / GetViewportRect().Size.X * UiScale.FactorOf(this);
        var metresPerLabel = MetresPerLabel(view, screenScale);
        var bandTop = view.End.Y - (BandHeight() * screenScale);
        using (var pen = UiPixelPen.Begin(this))
        {
            pen.Line(new Vector2(view.Position.X, 0), new Vector2(view.End.X, 0), _theme.GroundEdge, _theme.GroundEdgeWidth);
            DistanceRuler.Fill(_startX, view.Position.X, view.End.X, metresPerLabel, _marks);
            foreach (var mark in _marks)
            {
                var x = (float)mark.X;
                pen.Line(new Vector2(x, view.End.Y), new Vector2(x, view.End.Y - (TickLength(mark) * screenScale)), _theme.RulerTick, _theme.RulerTickWidth * screenScale);
            }

            DistanceRuler.FillHeights(-bandTop, -view.Position.Y, metresPerLabel, _heightMarks);
            foreach (var mark in _heightMarks)
            {
                var y = -(float)mark.X;
                pen.Line(new Vector2(view.Position.X, y), new Vector2(view.Position.X + (TickLength(mark) * screenScale), y), _theme.RulerTick, _theme.RulerTickWidth * screenScale);
            }
        }

        foreach (var mark in _marks)
        {
            DrawDistanceLabel(mark, view, screenScale);
        }

        foreach (var mark in _heightMarks)
        {
            DrawHeightLabel(mark, view, bandTop, screenScale);
        }

        DrawSetTransformMatrix(Transform2D.Identity);
    }

    // Tick, gap, a line of text and clear space above it, in UI units.
    private float BandHeight() =>
        _majorTickLength + _tickToLabel + _theme.RulerFont.GetHeight(_theme.RulerFontSize) + _bandPadding;

    private static float TickLength(RulerMark mark) => mark.IsMajor ? _majorTickLength : _minorTickLength;

    // Room for the widest label in view and a gap after it. It is sized for at least two digits
    // and a minus sign, so the labels do not thin out the moment a creature passes 9 m.
    private int MetresPerLabel(Rect2 view, float screenScale)
    {
        var farthest = Math.Max(Math.Abs(view.Position.X - _startX), Math.Abs(view.End.X - _startX));
        var digits = Math.Clamp(Math.Ceiling(farthest / Metres.WorldUnitsPerMetre).ToString(CultureInfo.InvariantCulture).Length, _minLabelDigits, _maxLabelDigits);
        var widest = Label(-long.Parse(new string('9', digits), CultureInfo.InvariantCulture));
        var room = _theme.RulerFont.GetStringSize(widest, HorizontalAlignment.Left, -1, _theme.RulerFontSize).X + _labelSpacing;
        _metresPerLabel = DistanceRuler.MetresPerLabel(Metres.WorldUnitsPerMetre / screenScale, room, _metresPerLabel);
        return _metresPerLabel;
    }

    private void DrawDistanceLabel(RulerMark mark, Rect2 view, float screenScale)
    {
        if (mark.LabelMetre is not { } metre)
        {
            return;
        }

        var label = Label(metre);
        var width = _theme.RulerFont.GetStringSize(label, HorizontalAlignment.Left, -1, _theme.RulerFontSize).X;
        var left = (float)mark.X - (width * screenScale / 2);
        var room = Mathf.Min(left - view.Position.X, view.End.X - (left + (width * screenScale))) / screenScale;
        var baseline = view.End.Y - ((_majorTickLength + _tickToLabel + _theme.RulerFont.GetDescent(_theme.RulerFontSize)) * screenScale);
        DrawLabel(label, new Vector2(left, baseline), room, screenScale);
    }

    // The label sits on its tick's line, just above it, so 0 m reads above the ground line rather
    // than struck through by it. It stays whole until its baseline reaches the distance scale's
    // band, so 0 m on a ground the shelf lowered onto the band still reads, then fades into it.
    private void DrawHeightLabel(RulerMark mark, Rect2 view, float bandTop, float screenScale)
    {
        if (mark.LabelMetre is not { } metre)
        {
            return;
        }

        var baseline = -(float)mark.X - (_tickToLabel * screenScale);
        var top = baseline - (_theme.RulerFont.GetAscent(_theme.RulerFontSize) * screenScale);
        var room = Mathf.Min(
            (top - (view.Position.Y + ((_topClear + _tickToLabel) * screenScale))) / screenScale,
            ((bandTop - baseline) / screenScale) + _labelFade);
        var left = view.Position.X + ((_majorTickLength + _tickToLabel) * screenScale);
        DrawLabel(Label(metre), new Vector2(left, baseline), room, screenScale);
    }

    // Drawn at screen size, scaled back up by as much as the camera zooms out, and faded over the
    // last stretch of room so a label never shows cut off.
    private void DrawLabel(string label, Vector2 baselineLeft, float room, float screenScale)
    {
        var fade = Mathf.Clamp(room / _labelFade, 0f, 1f);
        if (fade <= 0f)
        {
            return;
        }

        DrawSetTransform(baselineLeft, 0f, new Vector2(screenScale, screenScale));
        DrawString(
            _theme.RulerFont,
            Vector2.Zero,
            label,
            HorizontalAlignment.Left,
            -1,
            _theme.RulerFontSize,
            _theme.RulerLabel with { A = _theme.RulerLabel.A * fade });
    }

    private static string Label(long metre) => UiTextTranslation.Source(DistanceRuler.Label(metre))!();

    // What the camera shows, in this node's coordinates.
    private Rect2 VisibleArea() =>
        GetGlobalTransform().AffineInverse() * GetCanvasTransform().AffineInverse() * GetViewportRect();
}
