using Godot;
using NodeRunner.App.ViewModels;
using NodeRunner.Theme;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Widgets;

/// <summary>
/// The distance marks along the Training ground (#668), from <see cref="DistanceRuler"/>: ticks
/// hang from the ground line, the node's origin, with a label under every metre. It draws only the
/// part the camera shows and redraws when the view moves.
/// </summary>
public partial class ArenaRuler : Node2D
{
    // The reference ruler: an 8-long tick each metre, 4-long between, labels 22 below the line.
    private const float _majorTickLength = 8f;
    private const float _minorTickLength = 4f;
    private const float _labelBaseline = 22f;

    // A label fades out over this distance as it nears either edge, so none shows cut in half.
    private const float _labelFade = UiSize.Space.S4;

    private readonly List<RulerMark> _marks = [];
    private VisualTheme _theme = VisualTheme.Neon;
    private double _startX;
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
        DistanceRuler.Fill(
            _startX,
            view.Position.X - DistanceRuler.TickSpacing,
            view.End.X + DistanceRuler.TickSpacing,
            _marks);
        foreach (var mark in _marks)
        {
            DrawMark(mark, view);
        }
    }

    private void DrawMark(RulerMark mark, Rect2 view)
    {
        var x = (float)mark.X;
        var length = mark.IsMajor ? _majorTickLength : _minorTickLength;
        DrawLine(new Vector2(x, 0), new Vector2(x, length), _theme.RulerTick, _theme.RulerTickWidth);
        if (mark.Label.Length == 0)
        {
            return;
        }

        var width = _theme.RulerFont.GetStringSize(mark.Label, HorizontalAlignment.Left, -1, _theme.RulerFontSize).X;
        var left = x - (width / 2);
        var room = Mathf.Min(left - view.Position.X, view.End.X - (left + width));
        var fade = Mathf.Clamp(room / _labelFade, 0f, 1f);
        if (fade <= 0f)
        {
            return;
        }

        DrawString(
            _theme.RulerFont,
            new Vector2(left, _labelBaseline),
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
