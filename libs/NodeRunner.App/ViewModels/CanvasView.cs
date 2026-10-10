using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

/// <summary>
/// The Build canvas's zoom and pan. Maps between view units (the canvas
/// widget's own space, before zoom and pan) and canvas units (creature
/// coordinates, the same as <see cref="NodeDef.Position"/>):
/// <c>view = canvas * Zoom + Offset</c>. Not saved: Build opens with the
/// creation fitted (<see cref="Fit"/>). The view never shows anything outside
/// <see cref="Reach"/>, except while it eases back into a Reach that has
/// shrunk, and zooming out goes just far enough to show all of it. Zoom limits are on screen, not in view units: view units grow with
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

    /// <summary>How quickly an eased move (<see cref="ZoomOutToShow"/>, <see cref="Step"/>) closes on its goal, per second: the Training camera's zoom-out rate.</summary>
    public const double EaseRate = ArenaFraming.ZoomOutRate;

    // An eased move ends once it is closer than this: in zoom's log and in view units.
    private const double _closeLogZoom = 1e-3;
    private const double _closeOffset = 0.25;

    private readonly Func<CanvasRect?> _contentBounds;
    private readonly Func<CanvasRect?> _extraReach;
    private CanvasRect? _visibleArea;
    private double _uiScale = 1;
    private (double Zoom, Vector2D Offset)? _goal;

    /// <param name="bounds">The fixed part of the canvas the view may show, in canvas units.</param>
    /// <param name="contentBounds">What <see cref="Fit"/> frames, or null when there is nothing.</param>
    /// <param name="extraReach">More of the canvas the view may show for now, past the bounds, or null for none.</param>
    public CanvasView(CanvasRect bounds, Func<CanvasRect?>? contentBounds = null, Func<CanvasRect?>? extraReach = null)
    {
        Bounds = bounds;
        _contentBounds = contentBounds ?? (() => null);
        _extraReach = extraReach ?? (() => null);
    }

    public CanvasRect Bounds { get; }

    /// <summary>What the view may show now: <see cref="Bounds"/> and any extra reach, such as a selected Camera's rays (#1092).</summary>
    public CanvasRect Reach => _extraReach() is { } extra ? Bounds.Union(extra) : Bounds;

    /// <summary>True while the view eases toward a goal (<see cref="Step"/>).</summary>
    public bool IsEasing => _goal is not null;

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
    /// The lowest zoom: the one that shows all of <see cref="Reach"/>. No
    /// lower limit until the <see cref="VisibleArea"/> is known.
    /// </summary>
    public double MinZoom => VisibleArea is { } visible && Reach is var reach
        ? Math.Clamp(Math.Min(visible.Width / reach.Width, visible.Height / reach.Height), double.Epsilon, ZoomLimit)
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
        _goal = null;
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

        _goal = null;
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

        _goal = null;
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
    /// Eases the view out until <paramref name="target"/> (canvas units) is shown, never zooming
    /// in (#1092). Along each axis the view grows only toward the side the target reaches past,
    /// so the opposite edge stays put; it grows evenly where the target reaches past both. Nothing
    /// moves when the target is already shown, and a move on its way stops there. The move happens over <see cref="Step"/>, as far
    /// as <see cref="Reach"/> and <see cref="MinZoom"/> allow.
    /// </summary>
    public void ZoomOutToShow(CanvasRect target)
    {
        if (VisibleArea is not { } visible)
        {
            return;
        }

        var x = new Axis(visible.Min.X, visible.Max.X, Offset.X, target.Min.X, target.Max.X, Zoom);
        var y = new Axis(visible.Min.Y, visible.Max.Y, Offset.Y, target.Min.Y, target.Max.Y, Zoom);
        if (!x.Overflows && !y.Overflows)
        {
            _goal = null;
            return;
        }

        var zoom = ClampZoom(Math.Min(Zoom, Math.Min(x.ZoomToShow, y.ZoomToShow)));
        var offset = KeepWithinBounds(zoom, new Vector2D(x.OffsetAt(zoom), y.OffsetAt(zoom)));
        _goal = zoom == Zoom && offset == Offset ? null : (zoom, offset);
    }

    /// <summary>
    /// Moves the view <paramref name="deltaSeconds"/> further toward its goal, the way the
    /// Training camera eases its zoom (<see cref="EaseRate"/>). The zoom turns about the one point
    /// the start and the goal show in the same place, so an edge that stays put does not wander
    /// on the way. With no goal, a view left outside a <see cref="Reach"/> that has shrunk eases
    /// back inside it.
    /// </summary>
    public void Step(double deltaSeconds)
    {
        if (VisibleArea is not { } visible || !double.IsFinite(deltaSeconds) || deltaSeconds <= 0)
        {
            return;
        }

        _goal ??= Settled(visible);
        if (_goal is not { } goal)
        {
            return;
        }

        var ease = ArenaFraming.Ease(EaseRate, deltaSeconds);
        var logZoom = Math.Log(goal.Zoom / Zoom);
        var (zoom, offset) = (Zoom, new Vector2D(Offset.X + ((goal.Offset.X - Offset.X) * ease), Offset.Y + ((goal.Offset.Y - Offset.Y) * ease)));
        if (Math.Abs(logZoom) >= _closeLogZoom)
        {
            // The canvas point both views show at the same view position: c × zoom + offset is equal.
            var fixedCanvas = new Vector2D((Offset.X - goal.Offset.X) / (goal.Zoom - Zoom), (Offset.Y - goal.Offset.Y) / (goal.Zoom - Zoom));
            var fixedView = ToView(fixedCanvas);
            zoom = Zoom * Math.Exp(logZoom * ease);
            offset = new Vector2D(fixedView.X - (fixedCanvas.X * zoom), fixedView.Y - (fixedCanvas.Y * zoom));
        }

        if (Math.Abs(Math.Log(goal.Zoom / zoom)) < _closeLogZoom
            && Math.Abs(goal.Offset.X - offset.X) < _closeOffset && Math.Abs(goal.Offset.Y - offset.Y) < _closeOffset)
        {
            _goal = null;
            (zoom, offset) = goal;
        }

        Move(zoom, offset);
    }

    private double ClampZoom(double zoom) => Math.Clamp(zoom, MinZoom, ZoomLimit);

    // The view clamped into Reach, zooming about the middle; null when it already is.
    private (double Zoom, Vector2D Offset)? Settled(CanvasRect visible)
    {
        var zoom = ClampZoom(Zoom);
        var middle = ToCanvas(visible.Center);
        var offset = KeepWithinBounds(zoom, new Vector2D(visible.Center.X - (middle.X * zoom), visible.Center.Y - (middle.Y * zoom)));
        return zoom == Zoom && offset == Offset ? null : (zoom, offset);
    }

    private void Move(double zoom, Vector2D offset)
    {
        if (zoom == Zoom && offset == Offset)
        {
            return;
        }

        Zoom = zoom;
        Offset = offset;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// One axis of <see cref="ZoomOutToShow"/>: the visible span, the offset and the target's
    /// span at the current zoom. The view keeps the edge opposite the side the target reaches past,
    /// or its middle where it reaches past both or neither.
    /// </summary>
    private readonly record struct Axis(double VisibleMin, double VisibleMax, double Offset, double TargetMin, double TargetMax, double Zoom)
    {
        private bool PastMin => (TargetMin * Zoom) + Offset < VisibleMin;

        private bool PastMax => (TargetMax * Zoom) + Offset > VisibleMax;

        public bool Overflows => PastMin || PastMax;

        // The view position that stays put, and the canvas point under it.
        private double AnchorView => PastMin == PastMax ? (VisibleMin + VisibleMax) / 2 : PastMax ? VisibleMin : VisibleMax;

        private double AnchorCanvas => (AnchorView - Offset) / Zoom;

        /// <summary>The closest zoom that shows the target with the anchor kept in place.</summary>
        public double ZoomToShow =>
            PastMin && PastMax ? (VisibleMax - VisibleMin) / (TargetMax - TargetMin)
            : PastMax ? (VisibleMax - VisibleMin) / (TargetMax - AnchorCanvas)
            : PastMin ? (VisibleMax - VisibleMin) / (AnchorCanvas - TargetMin)
            : double.PositiveInfinity;

        /// <summary>The offset at <paramref name="zoom"/> that keeps the anchor in place, then shows the target if it still can.</summary>
        public double OffsetAt(double zoom)
        {
            var offset = AnchorView - (AnchorCanvas * zoom);
            var least = VisibleMin - (TargetMin * zoom);
            var most = VisibleMax - (TargetMax * zoom);
            return least <= most ? Math.Clamp(offset, least, most) : (least + most) / 2;
        }
    }

    private void Apply(double zoom, Vector2D offset)
    {
        zoom = ClampZoom(zoom);
        Move(zoom, KeepWithinBounds(zoom, offset));
    }

    private Vector2D KeepWithinBounds(double zoom, Vector2D offset)
    {
        if (VisibleArea is not { } visible)
        {
            return offset;
        }

        var reach = Reach;
        return new Vector2D(
            KeepWithinBoundsAlong(reach.Min.X * zoom, reach.Max.X * zoom, offset.X, visible.Min.X, visible.Max.X),
            KeepWithinBoundsAlong(reach.Min.Y * zoom, reach.Max.Y * zoom, offset.Y, visible.Min.Y, visible.Max.Y));
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
