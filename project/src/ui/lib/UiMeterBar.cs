using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>
/// A small read-only bar for one live value in a callout line (#1064): a <c>line</c> track with a
/// fill in the line's colour. A centred meter shows −1…1, filling left or right from a muted tick
/// at 0; a filling meter shows 0…1 from its left end. Values past the range are drawn at its end;
/// NaN draws no fill. Drawn here because Godot's ProgressBar fills only from an edge, never from
/// the centre, and <see cref="UiSlider"/> is an input with label and readout rows, far larger
/// than an inline meter.
/// </summary>
public sealed partial class UiMeterBar : Control
{
    private double _value = double.NaN;
    private bool _centred;
    private UiTokens.Color _fillColor = UiTokens.Color.Accent;

    public UiMeterBar()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SizeFlagsVertical = SizeFlags.ShrinkCenter;
    }

    /// <summary>The value shown; NaN shows an empty track.</summary>
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

    public override Vector2 _GetMinimumSize() => new(UiSize.Widget.MeterLength, UiSize.Widget.MeterTickHeight);

    public override void _Draw()
    {
        float thickness = UiSize.Widget.MeterTrackWidth;
        var radius = thickness * 0.5f;
        var track = new Rect2(0, (Size.Y - thickness) * 0.5f, Size.X, thickness);
        UiCorners.Uniform(radius).Fill(this, track, UiThemeLookup.Color(this, UiTokens.Color.Line));

        if (FillSpan(_value, _centred) is { } span && span.End > span.Start)
        {
            var fill = new Rect2(track.Position.X + (span.Start * track.Size.X), track.Position.Y, (span.End - span.Start) * track.Size.X, thickness);

            // A centred fill is square at the centre and rounded at its outer end, like the track.
            var corners = !_centred ? UiCorners.Uniform(radius)
                : span.Start < 0.5f ? new UiCorners(radius, 0, 0, radius)
                : new UiCorners(0, radius, radius, 0);
            corners.Fill(this, fill, UiThemeLookup.Color(this, _fillColor));
        }

        if (_centred)
        {
            var centre = Size.X * 0.5f;
            using var pen = UiPixelPen.Begin(this);
            pen.Line(new Vector2(centre, 0), new Vector2(centre, Size.Y), UiThemeLookup.Color(this, UiTokens.Color.Muted), UiSize.Stroke.Hair);
        }
    }
}
