using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

/// <summary>
/// The Build canvas's zoom and pan. Maps between view units (the canvas
/// widget's own space, before zoom and pan) and canvas units (creature
/// coordinates, the same as <see cref="NodeDef.Position"/>):
/// <c>view = canvas * Zoom + Offset</c>. Not saved: Build opens with the
/// creation fitted (<see cref="Fit"/>). The view never shows anything outside
/// <see cref="Bounds"/>, and zooming out goes just far enough to show all of
/// it. See `docs/BUILD_MODE.md`.
/// </summary>
public sealed class CanvasView
{
    public const double MaxZoom = 3;

    /// <summary>The share of the visible area left empty on each side when fitting.</summary>
    public const double FitMargin = 0.2;

    private readonly Func<CanvasRect?> _contentBounds;
    private CanvasRect? _visibleArea;

    /// <param name="bounds">The fixed part of the canvas the view may show, in canvas units.</param>
    /// <param name="contentBounds">What <see cref="Fit"/> frames, or null when there is nothing.</param>
    public CanvasView(CanvasRect bounds, Func<CanvasRect?>? contentBounds = null)
    {
        Bounds = bounds;
        _contentBounds = contentBounds ?? (() => null);
    }

    public CanvasRect Bounds { get; }

    public double Zoom { get; private set; } = 1;

    /// <summary>Where the canvas origin sits, in view units.</summary>
    public Vector2D Offset { get; private set; }

    /// <summary>
    /// The lowest zoom: the one that shows all of <see cref="Bounds"/>. No
    /// lower limit until the <see cref="VisibleArea"/> is known.
    /// </summary>
    public double MinZoom => VisibleArea is { } visible
        ? Math.Clamp(Math.Min(visible.Width / Bounds.Width, visible.Height / Bounds.Height), double.Epsilon, MaxZoom)
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
    /// <see cref="MinZoom"/>..<see cref="MaxZoom"/>, keeping the canvas point
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
    /// on every side, zooming out if it does not fit but never in past 1×.
    /// Does nothing until the visible area is known; with no content the view
    /// shows the middle of the bounds at 1×.
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
            Apply(1, new Vector2D(target.X - Bounds.Center.X, target.Y - Bounds.Center.Y));
            return;
        }

        var room = 1 - (2 * FitMargin);
        var zoom = Math.Min(
            1,
            Math.Min(
                content.Width > 0 ? visible.Width * room / content.Width : double.PositiveInfinity,
                content.Height > 0 ? visible.Height * room / content.Height : double.PositiveInfinity));
        zoom = ClampZoom(zoom);
        var center = content.Center;
        Apply(zoom, new Vector2D(target.X - (center.X * zoom), target.Y - (center.Y * zoom)));
    }

    private double ClampZoom(double zoom) => Math.Clamp(zoom, MinZoom, MaxZoom);

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
