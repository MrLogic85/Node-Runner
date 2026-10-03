using System.Globalization;
using Godot;
using NodeRunner.App.ViewModels;
using NodeRunner.Theme;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Widgets;

/// <summary>
/// The distance marks along the Training ground (#668), from <see cref="DistanceRuler"/>: ticks
/// hang from the ground line, the node's origin, a long one every metre. It draws only the
/// part the camera shows and redraws when the view moves. Ticks and labels keep their screen size at
/// any zoom; every metre is labelled, or every 2, 5, 10, … when labels would overlap (#675).
/// </summary>
public partial class ArenaRuler : Node2D
{
    // The reference ruler: an 8-long tick each metre, 4-long between, labels 22 below the line.
    private const float _majorTickLength = 8f;
    private const float _minorTickLength = 4f;
    private const float _labelBaseline = 22f;

    // A label fades out over this distance as it nears either edge, so none shows cut in half.
    private const float _labelFade = UiSize.Space.S4;

    // The least clear space between two labels, on screen.
    private const float _labelGap = UiSize.Space.S4;

    private const int _minLabelDigits = 2;

    private readonly List<RulerMark> _marks = [];
    private VisualTheme _theme = VisualTheme.Neon;
    private double _startX;
    private Rect2 _drawnView;
    private int _metresPerLabel = 1;

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
        // Half a metre past each side, so a label straddling the edge fades out instead of popping.
        var view = VisibleArea();
        // World units per UI unit: the arena view undoes the UI size (#738), so this applies it again.
        var screenScale = view.Size.X / GetViewportRect().Size.X * UiScale.FactorOf(this);
        DistanceRuler.Fill(
            _startX,
            view.Position.X - DistanceRuler.TickSpacing,
            view.End.X + DistanceRuler.TickSpacing,
            MetresPerLabel(view, screenScale),
            _marks);
        foreach (var mark in _marks)
        {
            DrawMark(mark, view, screenScale);
        }

        DrawSetTransformMatrix(Transform2D.Identity);
    }

    // Room for the widest label in view and a gap after it. It is sized for at least two digits
    // and a minus sign, so the labels do not thin out the moment a creature passes 9 m.
    private int MetresPerLabel(Rect2 view, float screenScale)
    {
        var farthest = Math.Max(Math.Abs(view.Position.X - _startX), Math.Abs(view.End.X - _startX));
        var digits = Math.Max(_minLabelDigits, Math.Ceiling(farthest / Metres.WorldUnitsPerMetre).ToString(CultureInfo.InvariantCulture).Length);
        var widest = $"-{new string('0', digits)} m";
        var room = _theme.RulerFont.GetStringSize(widest, HorizontalAlignment.Left, -1, _theme.RulerFontSize).X + _labelGap;
        _metresPerLabel = DistanceRuler.MetresPerLabel(Metres.WorldUnitsPerMetre / screenScale, room, _metresPerLabel);
        return _metresPerLabel;
    }

    private void DrawMark(RulerMark mark, Rect2 view, float screenScale)
    {
        var x = (float)mark.X;
        var length = (mark.IsMajor ? _majorTickLength : _minorTickLength) * screenScale;
        DrawSetTransformMatrix(Transform2D.Identity);
        DrawLine(new Vector2(x, 0), new Vector2(x, length), _theme.RulerTick, _theme.RulerTickWidth * screenScale);
        if (mark.Label.Length == 0)
        {
            return;
        }

        var width = _theme.RulerFont.GetStringSize(mark.Label, HorizontalAlignment.Left, -1, _theme.RulerFontSize).X;
        var left = x - (width * screenScale / 2);
        var room = Mathf.Min(left - view.Position.X, view.End.X - (left + (width * screenScale))) / screenScale;
        var fade = Mathf.Clamp(room / _labelFade, 0f, 1f);
        if (fade <= 0f)
        {
            return;
        }

        // Drawn at screen size: scaled back up by as much as the camera zooms out.
        DrawSetTransform(new Vector2(left, _labelBaseline * screenScale), 0f, new Vector2(screenScale, screenScale));
        DrawString(
            _theme.RulerFont,
            Vector2.Zero,
            mark.Label,
            HorizontalAlignment.Left,
            -1,
            _theme.RulerFontSize,
            _theme.RulerLabel with { A = _theme.RulerLabel.A * fade });
    }

    // What the camera shows, in this node's coordinates.
    private Rect2 VisibleArea() =>
        GetGlobalTransform().AffineInverse() * GetCanvasTransform().AffineInverse() * GetViewportRect();
}
