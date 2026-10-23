using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>
/// A small read-only half dial for one live value: a <c>line</c> arc with a fill and needle in
/// <see cref="FillColor"/> over an <c>ink</c> hub. A centred dial shows −1…1, upright at 0 with a muted tick there,
/// turning right for +1 and left for −1, and fills from the top; a filling dial shows 0…1 from its
/// left end to its right, filling from the left. Values past the range are drawn at its end; NaN
/// draws the arc alone. Drawn here because Godot has no dial or gauge Control.
/// </summary>
public sealed partial class UiDial : Control
{
    private const int _arcSegments = 24;

    private double _value = double.NaN;
    private bool _centred = true;
    private UiTokens.Color _fillColor = UiTokens.Color.Output;

    public UiDial()
    {
        UiMouseFilter.Prefer(this, MouseFilterEnum.Ignore);
        SizeFlagsVertical = SizeFlags.ShrinkCenter;
    }

    /// <summary>The value shown; NaN shows the arc alone, with no needle or fill.</summary>
    [Export]
    public double Value
    {
        get => _value;
        set
        {
            if (_value.Equals(value))
            {
                return;
            }

            _value = value;
            QueueRedraw();
        }
    }

    /// <summary>True for a −1…1 dial upright at 0; false for a 0…1 dial from its left end.</summary>
    [Export]
    public bool Centred
    {
        get => _centred;
        set
        {
            if (_centred == value)
            {
                return;
            }

            _centred = value;
            QueueRedraw();
        }
    }

    [Export]
    public UiTokens.Color FillColor
    {
        get => _fillColor;
        set
        {
            if (_fillColor == value)
            {
                return;
            }

            _fillColor = value;
            QueueRedraw();
        }
    }

    /// <summary>
    /// How far the needle turns from upright, in radians, clockwise on screen: the clamped value
    /// times a quarter turn for a centred dial, or from a quarter turn left at 0 to a quarter
    /// turn right at 1 for a filling one; null for NaN.
    /// </summary>
    public static float? NeedleTurn(double value, bool centred)
    {
        if (double.IsNaN(value))
        {
            return null;
        }

        var reach = centred ? Math.Clamp(value, -1, 1) : (2 * Math.Clamp(value, 0, 1)) - 1;
        return (float)(reach * Math.PI / 2);
    }

    /// <summary>
    /// Where the fill runs along the arc, as Godot angles from the start to the end (−π is the left
    /// end, −π/2 the top, 0 the right end): from the top to the needle for a centred dial, from
    /// the left end for a filling one; null for NaN.
    /// </summary>
    public static (float Start, float End)? FillArc(double value, bool centred)
    {
        if (NeedleTurn(value, centred) is not { } turn)
        {
            return null;
        }

        var needle = turn - (Mathf.Pi / 2);
        var from = centred ? -Mathf.Pi / 2 : -Mathf.Pi;
        return (Math.Min(from, needle), Math.Max(from, needle));
    }

    public override Vector2 _GetMinimumSize() => new(UiSize.Widget.DialWidth, UiSize.Widget.DialHeight);

    public override void _Draw()
    {
        float radius = UiSize.Widget.DialRadius;
        float track = UiSize.Widget.MeterTrackWidth;
        var hub = new Vector2(Size.X * 0.5f, (Size.Y - UiSize.Widget.DialHeight) * 0.5f + UiSize.Widget.DialHubY);
        var fillColor = UiThemeLookup.Color(this, _fillColor);
        using var pen = UiPixelPen.Begin(this);

        // The arc's ends are round, like a meter's track; Godot's arcs end square.
        var line = UiThemeLookup.Color(this, UiTokens.Color.Line);
        pen.Arc(hub, radius, -Mathf.Pi, 0, _arcSegments, line, track);
        pen.Disc(hub + new Vector2(-radius, 0), track * 0.5f, line);
        pen.Disc(hub + new Vector2(radius, 0), track * 0.5f, line);

        // A centred fill is square at the top and rounded at its outer end, like a centred meter's.
        if (FillArc(_value, _centred) is { } fill && fill.End > fill.Start)
        {
            var outer = _centred && fill.Start < -Mathf.Pi / 2 ? fill.Start : fill.End;
            pen.Arc(hub, radius, fill.Start, fill.End, _arcSegments, fillColor, track);
            pen.Disc(hub + (Vector2.FromAngle(outer) * radius), track * 0.5f, fillColor);
            if (!_centred)
            {
                pen.Disc(hub + new Vector2(-radius, 0), track * 0.5f, fillColor);
            }
        }

        if (_centred)
        {
            var top = hub + new Vector2(0, -radius);
            pen.Line(top + new Vector2(0, -track * 0.5f), top + new Vector2(0, track * 0.5f), UiThemeLookup.Color(this, UiTokens.Color.Muted), UiSize.Stroke.Hair);
        }

        if (NeedleTurn(_value, _centred) is { } turn)
        {
            pen.Line(hub, hub + (Vector2.FromAngle(turn - (Mathf.Pi / 2)) * UiSize.Widget.DialNeedleLength), fillColor, UiSize.Stroke.Signal);
        }

        pen.Disc(hub, UiSize.Widget.DialHubRadius, UiThemeLookup.Color(this, UiTokens.Color.Ink));
    }
}
