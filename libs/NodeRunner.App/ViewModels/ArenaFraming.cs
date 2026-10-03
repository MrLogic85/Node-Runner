namespace NodeRunner.App.ViewModels;

/// <summary>
/// What the Training camera frames each frame: the followed creature's centre and the box around
/// its parts, in world units (Y grows downward).
/// </summary>
/// <param name="CentreX">The centre point the camera follows sideways.</param>
/// <param name="Left">The left edge of the creature's box.</param>
/// <param name="Top">The top of the creature's box, its smallest Y.</param>
/// <param name="Right">The right edge of the creature's box.</param>
/// <param name="Bottom">The bottom of the creature's box, its largest Y.</param>
public readonly record struct FramedCreature(double CentreX, double Left, double Top, double Right, double Bottom)
{
    /// <summary>True when every value is finite and the box is not inside out; a physics blow-up is not.</summary>
    public bool IsUsable =>
        double.IsFinite(CentreX) && double.IsFinite(Left) && double.IsFinite(Top)
        && double.IsFinite(Right) && double.IsFinite(Bottom) && Left <= Right && Top <= Bottom;
}

/// <summary>
/// The Training camera's framing (#675): where it looks and how far it zooms out. Sideways it
/// follows the creature's centre through <see cref="ArenaFollow"/>. It zooms out so the creature
/// fits, and further the faster it moves; it zooms out quickly and back in only after the creature
/// has stayed smaller for a while, and then slowly, so a stretching gait does not make it pump.
/// The ground sits at <see cref="GroundFromTop"/> of the view at any zoom, so the view does not
/// bob. The zoom leaves <see cref="Headroom"/> above the creature, and only when its top rises past
/// that into <see cref="TopMargin"/> does the camera ease up after it. All sizes are in world units; the view's size is the size it shows at zoom 1. See
/// <c>docs/TRAINING_LOOP.md</c> → Camera.
/// </summary>
public sealed class ArenaFraming
{
    /// <summary>Where the ground line sits across the view's height, measured from the top.</summary>
    public const double GroundFromTop = 0.8;

    /// <summary>The share of the view's height kept clear above the creature.</summary>
    public const double TopMargin = 0.1;

    /// <summary>
    /// The share of the view's height the zoom leaves above a standing creature, below the top
    /// margin, for it to rise into before the camera follows it up.
    /// </summary>
    public const double Headroom = 0.05;

    /// <summary>The share of the view's width kept clear on either side of the creature.</summary>
    public const double SideMargin = 0.05;

    /// <summary>How far ahead the view widens for the creature's speed, in seconds of travel.</summary>
    public const double SpeedLookaheadSeconds = 1;

    /// <summary>The closest zoom: one world unit per view pixel, never closer.</summary>
    public const double MaxZoom = 1;

    /// <summary>The farthest zoom, showing four times as much as zoom 1; only a blow-up should need more.</summary>
    public const double MinZoom = 0.25;

    /// <summary>How quickly the zoom widens toward the view the creature needs, per second.</summary>
    public const double ZoomOutRate = 4;

    /// <summary>How quickly the zoom narrows back, per second, once <see cref="ZoomInDelaySeconds"/> has passed.</summary>
    public const double ZoomInRate = 0.5;

    /// <summary>How long the creature must need less room before the zoom narrows, in seconds.</summary>
    public const double ZoomInDelaySeconds = 1.5;

    /// <summary>How quickly the camera's height eases after the creature, per second.</summary>
    public const double RiseRate = ArenaFollow.EaseRate;

    private const double _viewCentre = 0.5;

    private readonly ArenaFollow _follow = new();
    private double _logZoom;
    private double _groundLineY;
    private double _closerSeconds;

    /// <summary>False until the first <see cref="Step"/> or <see cref="Cut"/> with a usable creature.</summary>
    public bool HasFrame { get; private set; }

    /// <summary>The camera's zoom: at 1 a world unit spans one view pixel; smaller shows more.</summary>
    public double Zoom => Math.Exp(_logZoom);

    /// <summary>The world Y shown at <see cref="GroundFromTop"/>: the ground's, unless the camera has risen after the creature.</summary>
    public double GroundLineY => _groundLineY;

    /// <summary>The camera's centre X, in world units.</summary>
    public double CameraX { get; private set; }

    /// <summary>The camera's centre Y, in world units.</summary>
    public double CameraY { get; private set; }

    /// <summary>
    /// The zoom that fits <paramref name="creature"/> moving at <paramref name="speedX"/> into a
    /// view <paramref name="viewWidth"/> by <paramref name="viewHeight"/>, with its centre at
    /// <see cref="ArenaFollow.FocusFromLeft"/> and its margins clear.
    /// </summary>
    public static double ZoomToFit(FramedCreature creature, double speedX, double viewWidth, double viewHeight)
    {
        var left = Math.Max(0, creature.CentreX - creature.Left) / (ArenaFollow.FocusFromLeft - SideMargin);
        var right = Math.Max(0, creature.Right - creature.CentreX) / (1 - ArenaFollow.FocusFromLeft - SideMargin);
        var tall = (creature.Bottom - creature.Top) / (GroundFromTop - TopMargin - Headroom);
        var fit = Math.Min(MaxZoom, Math.Min(viewWidth / Math.Max(left, right), viewHeight / tall));
        var shownWidth = (viewWidth / fit) + (Math.Abs(speedX) * SpeedLookaheadSeconds);
        return Math.Clamp(viewWidth / shownWidth, MinZoom, MaxZoom);
    }

    /// <summary>
    /// Frames <paramref name="creature"/> at once, at rest, dropping any glide: a new trial is a
    /// new scene. An unusable creature changes nothing.
    /// </summary>
    public void Cut(FramedCreature creature, double groundY, double viewWidth, double viewHeight)
    {
        if (!creature.IsUsable || !IsUsableView(groundY, viewWidth, viewHeight))
        {
            return;
        }

        _follow.SnapTo(creature.CentreX);
        _logZoom = Math.Log(ZoomToFit(creature, 0, viewWidth, viewHeight));
        _groundLineY = GroundLineTarget(creature, groundY, viewHeight);
        _closerSeconds = 0;
        HasFrame = true;
        Place(viewWidth, viewHeight);
    }

    /// <summary>
    /// Follows another creature, now framed as <paramref name="creature"/>: the view glides there,
    /// zoom and height included, and the jump between the two is not taken as speed.
    /// </summary>
    public void Retarget(FramedCreature creature)
    {
        if (creature.IsUsable)
        {
            _follow.Retarget(creature.CentreX);
        }
    }

    /// <summary>
    /// Eases the view toward <paramref name="creature"/> over <paramref name="deltaSeconds"/>; call
    /// <see cref="Retarget"/> first when it is another creature. The first call cuts; an unusable
    /// creature or view leaves everything where it is.
    /// </summary>
    public void Step(FramedCreature creature, double groundY, double viewWidth, double viewHeight, double deltaSeconds)
    {
        if (!HasFrame)
        {
            Cut(creature, groundY, viewWidth, viewHeight);
            return;
        }

        if (!creature.IsUsable || !IsUsableView(groundY, viewWidth, viewHeight) || deltaSeconds <= 0)
        {
            return;
        }

        _follow.Step(creature.CentreX, deltaSeconds);
        StepZoom(Math.Log(ZoomToFit(creature, _follow.SpeedX, viewWidth, viewHeight)), deltaSeconds);
        var groundLine = GroundLineTarget(creature, groundY, viewHeight);
        _groundLineY += (groundLine - _groundLineY) * Ease(RiseRate, deltaSeconds);
        Place(viewWidth, viewHeight);
    }

    private static bool IsUsableView(double groundY, double viewWidth, double viewHeight) =>
        double.IsFinite(groundY) && viewWidth > 0 && viewHeight > 0
        && double.IsFinite(viewWidth) && double.IsFinite(viewHeight);

    private static double Ease(double rate, double deltaSeconds) => 1 - Math.Exp(-rate * deltaSeconds);

    // Widening follows at once; narrowing waits until the creature has needed less room for the
    // whole delay, so a gait that stretches again within it never narrows the view at all.
    private void StepZoom(double target, double deltaSeconds)
    {
        if (target < _logZoom)
        {
            _closerSeconds = 0;
            _logZoom += (target - _logZoom) * Ease(ZoomOutRate, deltaSeconds);
            return;
        }

        _closerSeconds += deltaSeconds;
        if (_closerSeconds >= ZoomInDelaySeconds)
        {
            _logZoom += (target - _logZoom) * Ease(ZoomInRate, deltaSeconds);
        }
    }

    // The ground, unless the creature's top is above the top margin: then the line that puts the
    // top right on the margin.
    private double GroundLineTarget(FramedCreature creature, double groundY, double viewHeight) =>
        Math.Min(groundY, creature.Top + ((GroundFromTop - TopMargin) * viewHeight / Zoom));

    private void Place(double viewWidth, double viewHeight)
    {
        CameraX = ArenaFollow.CameraX(_follow.ShownX, viewWidth / Zoom);
        CameraY = _groundLineY + ((_viewCentre - GroundFromTop) * viewHeight / Zoom);
    }
}
