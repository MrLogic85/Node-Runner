using System.Globalization;
using Godot;
using NodeRunner.App.ViewModels;
using NodeRunner.Theme;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Widgets;

/// <summary>
/// The ground line and the Training arena's two scales, from <see cref="DistanceRuler"/>.
/// The line runs along the node's origin, as wide as the world zooms it; the pixel pen feathers it,
/// so a thin line does not flicker between pixel rows. The distance scale runs along the
/// view's bottom edge, its ticks rising from the edge and labels above them; the height scale runs
/// up the left edge from the ground's top (0 m), its ticks reaching in and labels sitting on them.
/// The height scale is cut off at the distance scale's band and the view's top, as the distance
/// labels are at the view's sides, rather than faded, so a label never blinks as the view shakes.
/// Both share one label step, so a metre reads the same either way. It draws only
/// what the camera shows, and the distance labels half a label past it, so they slide in and out
/// under the view's sides; it redraws when the view moves. Ticks and labels keep their screen size at any zoom; every metre is labelled, or
/// every 2, 5, 10, … when labels would overlap, and from 5 m between labels the ticks thin
/// out with them.
/// </summary>
public partial class ArenaRuler : Node2D
{
    // The reference scale: an 8-long tick each metre, 4-long between, labels S1 past the long ticks.
    private const float _majorTickLength = 8f;
    private const float _minorTickLength = 4f;
    private const float _tickToLabel = UiSize.Space.S1;

    // Clear space above the distance labels, inside the band the camera keeps the ground out of.
    private const float _bandPadding = UiSize.Space.S1;

    // The least clear space between two labels, on screen.
    private const float _labelSpacing = UiSize.Space.S4;

    private const int _minLabelDigits = 2;

    // DistanceRuler.Fill labels nothing farther out than this many digits of metres.
    private const int _maxLabelDigits = 16;

    private readonly List<RulerMark> _marks = [];
    private readonly List<RulerMark> _heightMarks = [];

    // Draws the height scale, clipped to the room it may use, so the ground line and distance scale
    // stay whole. A scissor rect, as Control.ClipContents sets, not clip_children: that would need a
    // drawn mask and an extra buffer, and the ruler draws in the world, not as a laid-out Control.
    private readonly Node2D _heightScale = new() { Name = "HeightScale" };
    private VisualTheme _theme = VisualTheme.Neon;
    private double _startX;
    private Rect2 _drawnView;
    private int _metresPerLabel = 1;
    private float _widestLabel;

    public VisualTheme Theme
    {
        get => _theme;
        set
        {
            _theme = value;
            Redraw();
        }
    }

    /// <summary>Where 0 m is, in this node's coordinates: the trial's start.</summary>
    public double StartX
    {
        get => _startX;
        set
        {
            _startX = value;
            Redraw();
        }
    }

    public override void _Ready()
    {
        _heightScale.Draw += DrawHeightScale;
        AddChild(_heightScale, @internal: InternalMode.Front);
    }

    public override void _Notification(int what)
    {
        if (what == NotificationTranslationChanged)
        {
            Redraw();
        }
    }

    public override void _Process(double delta)
    {
        var view = VisibleArea();
        if (view != _drawnView)
        {
            _drawnView = view;
            Redraw();
        }
    }

    /// <summary>
    /// The share of the view's height the distance scale takes along the bottom; the camera keeps
    /// the ground line above it while it makes room for a part callout.
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
        var (view, screenScale, metresPerLabel) = Measure();
        using (var pen = UiPixelPen.Begin(this))
        {
            pen.Line(new Vector2(view.Position.X, 0), new Vector2(view.End.X, 0), _theme.GroundEdge, _theme.GroundEdgeWidth);
            // Half a label past each side, so a label slides in under the view's edge, which clips it.
            var reach = _widestLabel / 2 * screenScale;
            DistanceRuler.Fill(_startX, view.Position.X - reach, view.End.X + reach, metresPerLabel, _marks);
            foreach (var mark in _marks)
            {
                var x = (float)mark.X;
                pen.Line(new Vector2(x, view.End.Y), new Vector2(x, view.End.Y - (TickLength(mark) * screenScale)), _theme.RulerTick, _theme.RulerTickWidth * screenScale);
            }
        }

        foreach (var mark in _marks)
        {
            DrawDistanceLabel(mark, view, screenScale);
        }

        DrawSetTransformMatrix(Transform2D.Identity);
    }

    // Cut off at the distance scale's band; it runs up to the view's top, which cuts it as the
    // sides cut the distance labels.
    private void DrawHeightScale()
    {
        var (view, screenScale, metresPerLabel) = Measure();
        var bandTop = view.End.Y - (BandHeight() * screenScale);
        var room = new Rect2(view.Position, view.Size.X, Mathf.Max(0f, bandTop - view.Position.Y));
        RenderingServer.CanvasItemSetCustomRect(_heightScale.GetCanvasItem(), true, room);
        RenderingServer.CanvasItemSetClip(_heightScale.GetCanvasItem(), true);

        var labelReach = (_tickToLabel + _theme.RulerFont.GetAscent(_theme.RulerFontSize)) * screenScale;
        DistanceRuler.FillHeights(-bandTop, labelReach, -view.Position.Y, metresPerLabel, _heightMarks);
        using (var pen = UiPixelPen.Begin(_heightScale))
        {
            foreach (var mark in _heightMarks)
            {
                var y = -(float)mark.X;
                pen.Line(new Vector2(view.Position.X, y), new Vector2(view.Position.X + (TickLength(mark) * screenScale), y), _theme.RulerTick, _theme.RulerTickWidth * screenScale);
            }
        }

        foreach (var mark in _heightMarks)
        {
            DrawHeightLabel(mark, view, screenScale);
        }

        _heightScale.DrawSetTransformMatrix(Transform2D.Identity);
    }

    // Both scales redraw together, so they never stand a frame apart.
    private void Redraw()
    {
        QueueRedraw();
        _heightScale.QueueRedraw();
    }

    // What the camera shows, world units per UI unit (the arena view undoes the UI size, so
    // this applies it again) and the label step. Both scales measure it as they draw.
    private (Rect2 View, float ScreenScale, int MetresPerLabel) Measure()
    {
        var view = VisibleArea();
        var screenScale = view.Size.X / GetViewportRect().Size.X * UiScale.FactorOf(this);
        return (view, screenScale, MetresPerLabel(view, screenScale));
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
        _widestLabel = _theme.RulerFont.GetStringSize(widest, HorizontalAlignment.Left, -1, _theme.RulerFontSize).X;
        var room = _widestLabel + _labelSpacing;
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
        var baseline = view.End.Y - ((_majorTickLength + _tickToLabel + _theme.RulerFont.GetDescent(_theme.RulerFontSize)) * screenScale);
        DrawLabel(this, label, new Vector2(left, baseline), screenScale);
    }

    // The label sits on its tick's line, just above it, so 0 m reads above the ground line rather
    // than struck through by it.
    private void DrawHeightLabel(RulerMark mark, Rect2 view, float screenScale)
    {
        if (mark.LabelMetre is not { } metre)
        {
            return;
        }

        var baseline = -(float)mark.X - (_tickToLabel * screenScale);
        var left = view.Position.X + ((_majorTickLength + _tickToLabel) * screenScale);
        DrawLabel(_heightScale, Label(metre), new Vector2(left, baseline), screenScale);
    }

    // Drawn at screen size, scaled back up by as much as the camera zooms out.
    private void DrawLabel(CanvasItem canvas, string label, Vector2 baselineLeft, float screenScale)
    {
        canvas.DrawSetTransform(baselineLeft, 0f, new Vector2(screenScale, screenScale));
        canvas.DrawString(
            _theme.RulerFont,
            Vector2.Zero,
            label,
            HorizontalAlignment.Left,
            -1,
            _theme.RulerFontSize,
            _theme.RulerLabel);
    }

    private static string Label(long metre) => UiTextTranslation.Source(DistanceRuler.Label(metre))!();

    // What the camera shows, in this node's coordinates.
    private Rect2 VisibleArea() =>
        GetGlobalTransform().AffineInverse() * GetCanvasTransform().AffineInverse() * GetViewportRect();
}
