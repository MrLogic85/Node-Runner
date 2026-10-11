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
/// The ground rests at <see cref="GroundFromTop"/> of the view at any zoom, so the view does not
/// bob. The zoom leaves <see cref="Headroom"/> above the creature, and only when its top rises past
/// that into <see cref="TopMargin"/> does the camera ease up after it. While a part callout shows,
/// the framing keeps a <see cref="Shelf"/> for it at the top (#1137): it lowers the ground toward
/// <see cref="ShelfGroundFromTop"/> first and zooms out only for the rest. All sizes are in world
/// units; the view's size is the size it shows at zoom 1. See <c>docs/TRAINING_LOOP.md</c> → Camera.
/// </summary>
public sealed class ArenaFraming
{
    /// <summary>Where the ground line rests across the view's height, measured from the top, unless a <see cref="Shelf"/> lowers it.</summary>
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

    /// <summary>
    /// The widest view, 500 m, in world units. No creature's size or speed comes near it, so the
    /// zoom follows them freely; it only keeps a physics blow-up from zooming out to nothing (#884).
    /// </summary>
    public const double MaxShownWidth = 500 * Metres.WorldUnitsPerMetre;

    /// <summary>How quickly the zoom widens toward the view the creature needs, per second.</summary>
    public const double ZoomOutRate = 4;

    /// <summary>How quickly the zoom narrows back, per second, once <see cref="ZoomInDelaySeconds"/> has passed.</summary>
    public const double ZoomInRate = 0.5;

    /// <summary>How long the creature must need less room before the zoom narrows, in seconds.</summary>
    public const double ZoomInDelaySeconds = 1.5;

    /// <summary>How quickly the camera's height eases after the creature, per second.</summary>
    public const double RiseRate = ArenaFollow.EaseRate;

    /// <summary>The lowest the ground goes, measured from the top, to make room for a <see cref="Shelf"/>.</summary>
    public const double ShelfGroundFromTop = 0.95;

    /// <summary>
    /// The least share of the view's height a creature keeps when the camera zooms out for a
    /// <see cref="Shelf"/>; a shelf that needs more covers the creature's top instead.
    /// </summary>
    public const double CreatureFloor = 0.25;

    private const double _viewCentre = 0.5;

    private readonly ArenaFollow _follow = new();
    private HeldEase _logZoom = new(0, roomIsUp: false);
    private HeldEase _groundFromTop = new(GroundFromTop, roomIsUp: true);
    private HeldEase _shelfLogZoom = new(0, roomIsUp: false);
    private (FramedCreature Creature, double GroundY, double ViewWidth, double ViewHeight)? _cutNotStepped;
    private double _shelf;
    private double _groundLineY;

    /// <summary>False until the first <see cref="Step"/> or <see cref="Cut"/> with a usable creature.</summary>
    public bool HasFrame { get; private set; }

    /// <summary>
    /// The share of the view's height to keep clear at the top for a part callout, its leader and
    /// margins; 0 with none. Set it as the callout changes: the framing makes the room at
    /// <see cref="ZoomOutRate"/>, and gives it back after <see cref="ZoomInDelaySeconds"/> at
    /// <see cref="ZoomInRate"/>, so tapping from part to part does not pump the view. Set between a
    /// <see cref="Cut"/> and the next step, as when the view's new size changes the share, it is
    /// part of that cut.
    /// </summary>
    public double Shelf
    {
        get => _shelf;
        set
        {
            if (value == _shelf)
            {
                return;
            }

            _shelf = value;
            if (_cutNotStepped is var (creature, groundY, viewWidth, viewHeight))
            {
                Cut(creature, groundY, viewWidth, viewHeight);
            }
        }
    }

    /// <summary>The camera's zoom: at 1 a world unit spans one view pixel; smaller shows more.</summary>
    public double Zoom => Math.Exp(Math.Min(_logZoom.Value, _shelfLogZoom.Value));

    /// <summary>The world Y shown at the ground line: the ground's, unless the camera has risen after the creature.</summary>
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
        return Math.Min(MaxZoom, Math.Max(viewWidth / shownWidth, viewWidth / MaxShownWidth));
    }

    /// <summary>
    /// How the view makes room for a <paramref name="shelf"/> above <paramref name="creature"/>
    /// shown at <paramref name="zoom"/> in a view <paramref name="viewHeight"/> tall: where the
    /// ground line goes, from the top, between <see cref="GroundFromTop"/> and
    /// <see cref="ShelfGroundFromTop"/>, and the zoom the view needs at most once the ground is as
    /// low as it goes, never smaller than keeps the creature <see cref="CreatureFloor"/> tall.
    /// A shelf the view already has room for keeps the ground where it is and asks no zoom.
    /// </summary>
    public static (double GroundFromTop, double Zoom) FitShelf(FramedCreature creature, double zoom, double viewHeight, double shelf)
    {
        var top = Math.Max(TopMargin, shelf) + Headroom;
        var tall = creature.Bottom - creature.Top;
        var ground = Math.Clamp(top + (tall * zoom / viewHeight), GroundFromTop, ShelfGroundFromTop);
        if (tall <= 0)
        {
            return (ground, MaxZoom);
        }

        var room = Math.Max(ShelfGroundFromTop - top, CreatureFloor) * viewHeight / tall;
        return (ground, Math.Min(MaxZoom, room));
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
        _logZoom = new HeldEase(Math.Log(ZoomToFit(creature, 0, viewWidth, viewHeight)), roomIsUp: false);
        var (ground, zoom) = FitShelf(creature, Math.Exp(_logZoom.Value), viewHeight, Shelf);
        _groundFromTop = new HeldEase(ground, roomIsUp: true);
        _shelfLogZoom = new HeldEase(Math.Log(zoom), roomIsUp: false);
        _groundLineY = GroundLineTarget(creature, groundY, viewHeight);
        _cutNotStepped = (creature, groundY, viewWidth, viewHeight);
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

        _cutNotStepped = null;
        _follow.Step(creature.CentreX, deltaSeconds);
        _logZoom.Step(Math.Log(ZoomToFit(creature, _follow.SpeedX, viewWidth, viewHeight)), deltaSeconds);
        StepShelfRoom(creature, viewHeight, deltaSeconds);
        var groundLine = GroundLineTarget(creature, groundY, viewHeight);
        _groundLineY += (groundLine - _groundLineY) * Ease(RiseRate, deltaSeconds);
        Place(viewWidth, viewHeight);
    }

    /// <summary>
    /// Eases only the room for the <see cref="Shelf"/> over <paramref name="deltaSeconds"/>, as
    /// while paused: the shelf opens and closes for a callout read then, but nothing else follows
    /// the creature. Does nothing before the first frame.
    /// </summary>
    public void StepShelf(FramedCreature creature, double viewWidth, double viewHeight, double deltaSeconds)
    {
        if (!HasFrame || !creature.IsUsable || !IsUsableView(0, viewWidth, viewHeight) || deltaSeconds <= 0)
        {
            return;
        }

        _cutNotStepped = null;
        StepShelfRoom(creature, viewHeight, deltaSeconds);
        Place(viewWidth, viewHeight);
    }

    private static bool IsUsableView(double groundY, double viewWidth, double viewHeight) =>
        double.IsFinite(groundY) && viewWidth > 0 && viewHeight > 0
        && double.IsFinite(viewWidth) && double.IsFinite(viewHeight);

    /// <summary>The share of the way to its goal an eased value covers in <paramref name="deltaSeconds"/> at <paramref name="rate"/> per second.</summary>
    internal static double Ease(double rate, double deltaSeconds) => 1 - Math.Exp(-rate * deltaSeconds);

    // Measured against the zoom the creature alone needs, so the shelf's own zoom never feeds back.
    private void StepShelfRoom(FramedCreature creature, double viewHeight, double deltaSeconds)
    {
        var (ground, zoom) = FitShelf(creature, Math.Exp(_logZoom.Value), viewHeight, Shelf);
        _groundFromTop.Step(ground, deltaSeconds);
        _shelfLogZoom.Step(Math.Log(zoom), deltaSeconds);
    }

    // The ground, unless the creature's top is above the top margin: then the line that puts the
    // top right on the margin.
    private double GroundLineTarget(FramedCreature creature, double groundY, double viewHeight) =>
        Math.Min(groundY, creature.Top + ((_groundFromTop.Value - TopMargin) * viewHeight / Zoom));

    private void Place(double viewWidth, double viewHeight)
    {
        CameraX = ArenaFollow.CameraX(_follow.ShownX, viewWidth / Zoom);
        CameraY = _groundLineY + ((_viewCentre - _groundFromTop.Value) * viewHeight / Zoom);
    }

    /// <summary>
    /// A framing value that makes room at once and gives it back slowly: toward more room it eases
    /// at <see cref="ZoomOutRate"/>; toward less it waits until less has been enough for the whole
    /// <see cref="ZoomInDelaySeconds"/>, then eases at <see cref="ZoomInRate"/>. So a gait that
    /// stretches again within the delay, or a tap from part to part, never gives the room back.
    /// <paramref name="roomIsUp"/> says whether a larger value makes more room, as the ground's
    /// place does and the zoom does not.
    /// </summary>
    private sealed class HeldEase(double value, bool roomIsUp)
    {
        private double _lessSeconds;

        public double Value { get; private set; } = value;

        /// <summary>Eases toward <paramref name="target"/> over <paramref name="deltaSeconds"/>.</summary>
        public void Step(double target, double deltaSeconds)
        {
            // At rest, as right after a cut, is not less room: only time spent wanting less counts.
            if (target == Value || target > Value == roomIsUp)
            {
                _lessSeconds = 0;
                Value += (target - Value) * Ease(ZoomOutRate, deltaSeconds);
                return;
            }

            _lessSeconds += deltaSeconds;
            if (_lessSeconds >= ZoomInDelaySeconds)
            {
                Value += (target - Value) * Ease(ZoomInRate, deltaSeconds);
            }
        }
    }
}
