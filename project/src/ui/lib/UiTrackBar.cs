using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>
/// A thin read-only strip for progress along a length: a <c>line</c> track that fills from its left
/// end in <see cref="FillColor"/>, an <c>ink</c> marker tick at a point along it, and, when
/// <see cref="Capped"/>, a tick in the fill's colour where the fill ends. Both are fractions of the
/// track, clamped to 0…1; NaN draws no fill or no marker. Drawn here because Godot's ProgressBar
/// has no marker, and <see cref="UiMeterBar"/> is a short inline meter rather than a strip that
/// spans a card.
/// </summary>
public sealed partial class UiTrackBar : Control
{
    private double _fill = double.NaN;
    private double _marker = double.NaN;
    private bool _capped;
    private UiTokens.Color _fillColor = UiTokens.Color.Accent;

    public UiTrackBar()
    {
        UiMouseFilter.Prefer(this, MouseFilterEnum.Ignore);
        SizeFlagsVertical = SizeFlags.ShrinkCenter;
    }

    /// <summary>How much of the track is filled, 0…1; NaN fills none.</summary>
    [Export]
    public double Fill
    {
        get => _fill;
        set
        {
            if (_fill.Equals(value))
            {
                return;
            }

            _fill = value;
            QueueRedraw();
        }
    }

    /// <summary>Where the marker stands along the track, 0…1; NaN shows none.</summary>
    [Export]
    public double Marker
    {
        get => _marker;
        set
        {
            if (_marker.Equals(value))
            {
                return;
            }

            _marker = value;
            QueueRedraw();
        }
    }

    /// <summary>True to end the fill with a tick in its colour, as where a frozen value stopped.</summary>
    [Export]
    public bool Capped
    {
        get => _capped;
        set
        {
            if (_capped == value)
            {
                return;
            }

            _capped = value;
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

    /// <summary>The X of a point <paramref name="fraction"/> of the way along <paramref name="track"/>, clamped to it; null for NaN.</summary>
    public static float? At(Rect2 track, double fraction) =>
        double.IsNaN(fraction) ? null : track.Position.X + ((float)Math.Clamp(fraction, 0, 1) * track.Size.X);

    public override Vector2 _GetMinimumSize() => new(UiSize.Widget.MeterLength, UiSize.Widget.TrackStripTickHeight);

    public override void _Draw()
    {
        float thickness = UiSize.Widget.TrackStripHeight;
        var corners = UiCorners.Uniform(thickness * 0.5f);
        var track = new Rect2(0, (Size.Y - thickness) * 0.5f, Size.X, thickness);
        var fillColor = UiThemeLookup.Color(this, _fillColor);
        corners.Fill(this, track, UiThemeLookup.Color(this, UiTokens.Color.Line));

        var end = At(track, _fill);
        if (end is { } fillEnd && fillEnd > track.Position.X)
        {
            corners.Fill(this, new Rect2(track.Position, fillEnd - track.Position.X, thickness), fillColor);
        }

        using var pen = UiPixelPen.Begin(this);
        if (At(track, _marker) is { } marker)
        {
            Tick(pen, marker, UiThemeLookup.Color(this, UiTokens.Color.Ink), UiSize.Stroke.Signal);
        }

        if (_capped && end is { } cap)
        {
            Tick(pen, cap, fillColor, UiSize.Stroke.Signal);
        }
    }

    // A tick at either end stays whole inside the strip.
    private void Tick(UiPixelPen pen, float x, Color color, float width)
    {
        x = Math.Clamp(x, width * 0.5f, Size.X - (width * 0.5f));
        pen.Line(new Vector2(x, 0), new Vector2(x, Size.Y), color, width);
    }
}
