using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>
/// A small read-only bar for one live value: a <c>line</c> track with a fill. A centred meter
/// shows −1…1, filling left or right from a muted tick at 0; a filling meter shows 0…1 from its
/// left end. A vertical meter is an upright 8 × 24 bar that fills up and down instead, from its
/// bottom end. Values past the range are drawn at its end;
/// NaN draws no fill. Drawn here because Godot's ProgressBar fills only from an edge, never from
/// the centre, and <see cref="UiSlider"/> is an input with label and readout rows, far larger
/// than an inline meter.
/// </summary>
public sealed partial class UiMeterBar : Control
{
    private double _value = double.NaN;
    private bool _centred;
    private bool _vertical;
    private UiTokens.Color _fillColor = UiTokens.Color.Accent;

    public UiMeterBar()
    {
        UiMouseFilter.Prefer(this, MouseFilterEnum.Ignore);
        SizeFlagsVertical = SizeFlags.ShrinkCenter;
    }

    /// <summary>The value shown; NaN shows an empty track.</summary>
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

    /// <summary>True for a −1…1 meter filling from the centre; false for a 0…1 meter filling from the left.</summary>
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

    /// <summary>True for an upright bar filling up from its bottom end, or up and down from the centre.</summary>
    [Export]
    public bool Vertical
    {
        get => _vertical;
        set
        {
            if (_vertical == value)
            {
                return;
            }

            _vertical = value;
            UpdateMinimumSize();
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
    /// Where the fill runs, as fractions of the track from its left end: from the centre to the
    /// clamped value for a centred meter, from 0 for a filling one; null for NaN.
    /// </summary>
    public static (float Start, float End)? FillSpan(double value, bool centred)
    {
        if (double.IsNaN(value))
        {
            return null;
        }

        if (!centred)
        {
            return (0, (float)Math.Clamp(value, 0, 1));
        }

        var at = (float)(0.5 + (Math.Clamp(value, -1, 1) * 0.5));
        return (Math.Min(0.5f, at), Math.Max(0.5f, at));
    }

    /// <summary>
    /// The fill's rectangle in <paramref name="track"/>: along it from the left end, or from the
    /// bottom end for a vertical meter; null when nothing fills.
    /// </summary>
    public static Rect2? FillRect(Rect2 track, double value, bool centred, bool vertical)
    {
        if (FillSpan(value, centred) is not { } span || span.End <= span.Start)
        {
            return null;
        }

        return vertical
            ? new Rect2(track.Position.X, track.End.Y - (span.End * track.Size.Y), track.Size.X, (span.End - span.Start) * track.Size.Y)
            : new Rect2(track.Position.X + (span.Start * track.Size.X), track.Position.Y, (span.End - span.Start) * track.Size.X, track.Size.Y);
    }

    public override Vector2 _GetMinimumSize() => _vertical
        ? new(UiSize.Widget.VerticalMeterWidth, UiSize.Widget.VerticalMeterHeight)
        : new(UiSize.Widget.MeterLength, UiSize.Widget.MeterTickHeight);

    public override void _Draw()
    {
        // A vertical meter's track is the whole bar; a horizontal one is a thin line the tick stands past.
        float thickness = _vertical ? Size.X : UiSize.Widget.MeterTrackWidth;
        var radius = thickness * 0.5f;
        var track = _vertical ? new Rect2(Vector2.Zero, Size) : new Rect2(0, (Size.Y - thickness) * 0.5f, Size.X, thickness);
        UiCorners.Uniform(radius).Fill(this, track, UiThemeLookup.Color(this, UiTokens.Color.Line));

        if (FillRect(track, _value, _centred, _vertical) is { } fill)
        {
            // A centred fill is square at the centre and rounded at its outer end, like the track.
            var corners = !_centred ? UiCorners.Uniform(radius)
                : _vertical ? (_value < 0 ? new UiCorners(0, 0, radius, radius) : new UiCorners(radius, radius, 0, 0))
                : _value < 0 ? new UiCorners(radius, 0, 0, radius)
                : new UiCorners(0, radius, radius, 0);
            corners.Fill(this, fill, UiThemeLookup.Color(this, _fillColor));
        }

        if (_centred)
        {
            var centre = Size * 0.5f;
            using var pen = UiPixelPen.Begin(this);
            var (from, to) = _vertical ? (new Vector2(0, centre.Y), new Vector2(Size.X, centre.Y)) : (new Vector2(centre.X, 0), new Vector2(centre.X, Size.Y));
            pen.Line(from, to, UiThemeLookup.Color(this, UiTokens.Color.Muted), UiSize.Stroke.Hair);
        }
    }
}
