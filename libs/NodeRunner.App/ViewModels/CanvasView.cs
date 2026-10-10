using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

/// <summary>
/// The Build canvas's zoom and pan. Maps between view units (the canvas
/// widget's own space, before zoom and pan) and canvas units (creature
/// coordinates, the same as <see cref="NodeDef.Position"/>):
/// <c>view = canvas * Zoom + Offset</c>. Not saved: Build opens with the
/// creation fitted (<see cref="Fit"/>). The view never shows anything outside
/// <see cref="Bounds"/>, and zooming out goes just far enough to show all of
/// it. Zoom limits are on screen, not in view units: view units grow with
/// the UI size's root factor (<see cref="UiScale"/>, #299), so the limits shrink by it and the
/// creation keeps its size on screen. Godot's <c>Camera2D</c> would replace
/// only the clamp, so Build keeps this class (#564). See `docs/BUILD_MODE.md`.
/// </summary>
public sealed class CanvasView
{
    /// <summary>The closest zoom at a root factor of 1; <see cref="ZoomLimit"/> is the one in effect.</summary>
    public const double MaxZoom = 3;

    /// <summary>The share of the visible area left empty on each side when fitting.</summary>
    public const double FitMargin = 0.2;

    /// <summary>The share of the visible area <see cref="ZoomOutToShow"/> keeps clear on each side.</summary>
    public const double ShowMargin = 0.05;

    private readonly Func<CanvasRect?> _contentBounds;
    private CanvasRect? _visibleArea;
    private double _uiScale = 1;

    /// <param name="bounds">The fixed part of the canvas the view may show, in canvas units.</param>
    /// <param name="contentBounds">What <see cref="Fit"/> frames, or null when there is nothing.</param>
    public CanvasView(CanvasRect bounds, Func<CanvasRect?>? contentBounds = null)
    {
        Bounds = bounds;
        _contentBounds = contentBounds ?? (() => null);
    }

    public CanvasRect Bounds { get; }

    /// <summary>
    /// The UI size's root factor: how many times bigger a view unit shows
    /// than with no factor. Zoom 1 with no factor is zoom 1 / UiScale here.
    /// </summary>
    public double UiScale
    {
        get => _uiScale;
        set
        {
            if (!double.IsFinite(value) || value <= 0 || value == _uiScale)
            {
                return;
            }

            var zoom = Zoom * _uiScale / value;
            _uiScale = value;
            Apply(zoom, Offset);
        }
    }

    /// <summary>The closest zoom: <see cref="MaxZoom"/> on screen.</summary>
    public double ZoomLimit => MaxZoom / UiScale;

    /// <summary>The zoom that shows the creation at its true size on screen: 1 at a root factor of 1.</summary>
    public double TrueSizeZoom => 1 / UiScale;

    public double Zoom { get; private set; } = 1;

    /// <summary>Where the canvas origin sits, in view units.</summary>
    public Vector2D Offset { get; private set; }

    /// <summary>
    /// The lowest zoom: the one that shows all of <see cref="Bounds"/>. No
    /// lower limit until the <see cref="VisibleArea"/> is known.
    /// </summary>
    public double MinZoom => VisibleArea is { } visible
        ? Math.Clamp(Math.Min(visible.Width / Bounds.Width, visible.Height / Bounds.Height), double.Epsilon, ZoomLimit)
        : 0;

    /// <summary>The part of view space the player sees, once the canvas knows its size.</summary>
    public CanvasRect? VisibleArea
    {
        get => _visibleArea;
        set
        {
            if (_visibleArea == value)
            {
                return;
            }

            _visibleArea = value;
            Apply(Zoom, Offset);
        }
    }

    public event EventHandler? Changed;

    public Vector2D ToCanvas(Vector2D viewPosition) =>
        new((viewPosition.X - Offset.X) / Zoom, (viewPosition.Y - Offset.Y) / Zoom);

    public Vector2D ToView(Vector2D canvasPosition) =>
        new((canvasPosition.X * Zoom) + Offset.X, (canvasPosition.Y * Zoom) + Offset.Y);

    /// <summary>Moves the picture by <paramref name="delta"/> view units, as far as the bounds reach.</summary>
    public void PanBy(Vector2D delta)
    {
        Apply(Zoom, new Vector2D(Offset.X + delta.X, Offset.Y + delta.Y));
    }

    /// <summary>
    /// Multiplies the zoom by <paramref name="factor"/>, clamped to
    /// <see cref="MinZoom"/>..<see cref="ZoomLimit"/>, keeping the canvas point
    /// under <paramref name="focus"/> (view units) in place as far as the bounds allow.
    /// </summary>
    public void ZoomAbout(Vector2D focus, double factor)
    {
        if (!double.IsFinite(factor) || factor <= 0)
        {
            return;
        }

        var zoom = ClampZoom(Zoom * factor);
        var anchor = ToCanvas(focus);
        Apply(zoom, new Vector2D(focus.X - (anchor.X * zoom), focus.Y - (anchor.Y * zoom)));
    }

    /// <summary>
    /// Centres the content in <see cref="VisibleArea"/> with <see cref="FitMargin"/>
    /// on every side, zooming out if it does not fit but never in past
    /// <see cref="TrueSizeZoom"/>. Does nothing until the visible area is
    /// known; with no content the view shows the middle of the bounds at
    /// <see cref="TrueSizeZoom"/>.
    /// </summary>
    public void Fit()
    {
        if (VisibleArea is not { } visible)
        {
            return;
        }

        var target = visible.Center;
        if (_contentBounds() is not { } content)
        {
            var trueSize = ClampZoom(TrueSizeZoom);
            Apply(trueSize, new Vector2D(target.X - (Bounds.Center.X * trueSize), target.Y - (Bounds.Center.Y * trueSize)));
            return;
        }

        var room = 1 - (2 * FitMargin);
        var zoom = Math.Min(
            TrueSizeZoom,
            Math.Min(
                content.Width > 0 ? visible.Width * room / content.Width : double.PositiveInfinity,
                content.Height > 0 ? visible.Height * room / content.Height : double.PositiveInfinity));
        zoom = ClampZoom(zoom);
        var center = content.Center;
        Apply(zoom, new Vector2D(target.X - (center.X * zoom), target.Y - (center.Y * zoom)));
    }

    /// <summary>
    /// Brings <paramref name="target"/> (canvas units) into <see cref="VisibleArea"/> with
    /// <see cref="ShowMargin"/> on every side, moving the view as little as it can: nothing when it
    /// is already shown, else zooming out about the middle of the view (never in) and then panning
    /// just far enough. As far as <see cref="Bounds"/> and <see cref="MinZoom"/> allow (#1092).
    /// </summary>
    public void ZoomOutToShow(CanvasRect target)
    {
        if (VisibleArea is not { } visible)
        {
            return;
        }

        var room = new CanvasRect(
            new Vector2D(visible.Min.X + (visible.Width * ShowMargin), visible.Min.Y + (visible.Height * ShowMargin)),
            new Vector2D(visible.Max.X - (visible.Width * ShowMargin), visible.Max.Y - (visible.Height * ShowMargin)));
        if (room.Contains(ToView(target.Min)) && room.Contains(ToView(target.Max)))
        {
            return;
        }

        var zoom = ClampZoom(Math.Min(
            Zoom,
            Math.Min(
                target.Width > 0 ? room.Width / target.Width : double.PositiveInfinity,
                target.Height > 0 ? room.Height / target.Height : double.PositiveInfinity)));
        var middle = ToCanvas(visible.Center);
        Apply(zoom, new Vector2D(
            PanToShow(visible.Center.X - (middle.X * zoom), target.Min.X * zoom, target.Max.X * zoom, room.Min.X, room.Max.X),
            PanToShow(visible.Center.Y - (middle.Y * zoom), target.Min.Y * zoom, target.Max.Y * zoom, room.Min.Y, room.Max.Y)));
    }

    private double ClampZoom(double zoom) => Math.Clamp(zoom, MinZoom, ZoomLimit);

    // The offset nearest to offset that puts targetMin..targetMax (scaled canvas units) inside
    // roomMin..roomMax, or centres it there when it is wider.
    private static double PanToShow(double offset, double targetMin, double targetMax, double roomMin, double roomMax)
    {
        var least = roomMin - targetMin;
        var most = roomMax - targetMax;
        return least <= most ? Math.Clamp(offset, least, most) : (least + most) / 2;
    }

    private void Apply(double zoom, Vector2D offset)
    {
        zoom = ClampZoom(zoom);
        offset = KeepWithinBounds(zoom, offset);
        if (zoom == Zoom && offset == Offset)
        {
            return;
        }

        Zoom = zoom;
        Offset = offset;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private Vector2D KeepWithinBounds(double zoom, Vector2D offset)
    {
        if (VisibleArea is not { } visible)
        {
            return offset;
        }

        return new Vector2D(
            KeepWithinBoundsAlong(Bounds.Min.X * zoom, Bounds.Max.X * zoom, offset.X, visible.Min.X, visible.Max.X),
            KeepWithinBoundsAlong(Bounds.Min.Y * zoom, Bounds.Max.Y * zoom, offset.Y, visible.Min.Y, visible.Max.Y));
    }

    /// <summary>
    /// Shifts <paramref name="offset"/> so the visible span stays inside the
    /// bounds' span, or centres the bounds where they are the smaller of the two.
    /// </summary>
    private static double KeepWithinBoundsAlong(double boundsMin, double boundsMax, double offset, double visibleMin, double visibleMax)
    {
        var lowest = visibleMax - boundsMax;
        var highest = visibleMin - boundsMin;
        return lowest <= highest ? Math.Clamp(offset, lowest, highest) : (lowest + highest) / 2;
    }
}
