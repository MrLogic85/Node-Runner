namespace NodeRunner.App.ViewModels;

/// <summary>
/// What the Training camera frames each frame: the followed creature's centre and the box around
/// its parts, in world units (Y grows downward).
/// </summary>
/// <param name="CentreX">The centre point the camera follows sideways: the centre of mass.</param>
/// <param name="CentreY">The centre point the camera follows up and down: the centre of mass.</param>
/// <param name="Left">The left edge of the creature's box.</param>
/// <param name="Top">The top of the creature's box, its smallest Y.</param>
/// <param name="Right">The right edge of the creature's box.</param>
/// <param name="Bottom">The bottom of the creature's box, its largest Y.</param>
public readonly record struct FramedCreature(double CentreX, double CentreY, double Left, double Top, double Right, double Bottom)
{
    /// <summary>True when every value is finite and the box is not inside out; a physics blow-up is not.</summary>
    public bool IsUsable =>
        double.IsFinite(CentreX) && double.IsFinite(CentreY) && double.IsFinite(Left) && double.IsFinite(Top)
        && double.IsFinite(Right) && double.IsFinite(Bottom) && Left <= Right && Top <= Bottom;
}

/// <summary>
/// The Training camera's framing (#675, #1169): where it looks and how far it zooms out. It
/// follows the creature's centre of mass on both axes through one <see cref="ArenaFollow"/> each and
/// shows it in the middle of the view at rest, except that the ground line never rises above
/// <see cref="GroundFromTop"/>, so a creature on the ground sits low and a hopping gait does not
/// bob the view, and that a moving creature shows behind the middle. It zooms out so the creature
/// fits, and further the faster it moves on either axis; it zooms out quickly and back in only
/// after the creature has stayed smaller for a while, and then slowly, so a stretching gait does
/// not make it pump. The room speed adds is shared (#1190): the view slides ahead by a sixth of it,
/// so a third of it shows behind and the creature nears a third of the view in from the back edge
/// as it speeds up. After all smoothing, a safe zone keeps the creature's box
/// <see cref="SafeMargin"/> inside the view's sides and top, so a sudden start or launch, which the
/// eased speed lags, never takes it off screen. While a part callout
/// shows, the framing keeps a <see cref="Shelf"/> for it at the top (#1137): it lowers the ground
/// first, toward <see cref="ShelfGroundFromTop"/> but never into the <see cref="ScaleBand"/> along
/// the bottom (#1170), and zooms out only for the rest. All sizes are in
/// world units; the view's size is the size it shows at zoom 1. See
/// <c>docs/TRAINING_LOOP.md</c> → Camera.
/// </summary>
public sealed class ArenaFraming
{
    /// <summary>
    /// The highest the ground line goes across the view's height, measured from the top, unless a
    /// <see cref="Shelf"/> lowers it: where it rests under a creature on the ground.
    /// </summary>
    public const double GroundFromTop = 0.8;

    /// <summary>
    /// The share of the view's height kept clear above a creature, and below it while the camera
    /// centres it in the air.
    /// </summary>
    public const double TopMargin = 0.1;

    /// <summary>The share of the view's height the zoom leaves above a standing creature, below the top margin.</summary>
    public const double Headroom = 0.05;

    /// <summary>The share of the view's width kept clear on either side of the creature.</summary>
    public const double SideMargin = 0.05;

    /// <summary>
    /// The least share of the view kept between the creature's box and the view's sides and top
    /// (#1190), or half the room the box has there at rest when that is less, so the safe zone never
    /// moves a creature at rest. The ground limit holds the bottom.
    /// </summary>
    public const double SafeMargin = 0.1;

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

    /// <summary>
    /// The lowest the ground goes, measured from the top, to make room for a <see cref="Shelf"/>;
    /// a <see cref="ScaleBand"/> stops it higher.
    /// </summary>
    public const double ShelfGroundFromTop = 0.95;

    /// <summary>
    /// The least share of the view's height a creature keeps when the camera zooms out for a
    /// <see cref="Shelf"/>; a shelf that needs more covers the creature's top instead.
    /// </summary>
    public const double CreatureFloor = 0.25;

    private const double _viewCentre = 0.5;

    private readonly ArenaFollow _followX = new();
    private readonly ArenaFollow _followY = new();
    private HeldEase _logZoom = new(0, roomIsUp: false);
    private HeldEase _restLogZoom = new(0, roomIsUp: false);
    private HeldEase _groundFromTop = new(GroundFromTop, roomIsUp: true);
    private HeldEase _shelfLogZoom = new(0, roomIsUp: false);
    private (FramedCreature Creature, double GroundY, double ViewWidth, double ViewHeight)? _cutNotStepped;
    private double _shelf;
    private double _scaleBand;
    private double _groundY;
    private bool _safeZoneHolds;

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
            RecutIfNotStepped();
        }
    }

    /// <summary>
    /// The share of the view's height the distance scale takes along the bottom (#1170); 0 with
    /// none. Making room for a <see cref="Shelf"/> lowers the ground no further than its top. Set
    /// between a <see cref="Cut"/> and the next step, it is part of that cut, as for the shelf.
    /// </summary>
    public double ScaleBand
    {
        get => _scaleBand;
        set
        {
            if (value == _scaleBand)
            {
                return;
            }

            _scaleBand = value;
            RecutIfNotStepped();
        }
    }

    /// <summary>The camera's zoom: at 1 a world unit spans one view pixel; smaller shows more.</summary>
    public double Zoom => Math.Exp(Math.Min(_logZoom.Value, _shelfLogZoom.Value));

    /// <summary>The camera's centre X, in world units.</summary>
    public double CameraX { get; private set; }

    /// <summary>The camera's centre Y, in world units.</summary>
    public double CameraY { get; private set; }

    /// <summary>
    /// The zoom that fits <paramref name="creature"/> moving at <paramref name="speedX"/> and
    /// <paramref name="speedY"/> into a view <paramref name="viewWidth"/> by
    /// <paramref name="viewHeight"/>. At rest the creature fits with its centre in the middle and
    /// its sides clear of <see cref="SideMargin"/>; standing on the ground at
    /// <see cref="GroundFromTop"/> below <see cref="TopMargin"/> and <see cref="Headroom"/>; and
    /// centred in the air clear of <see cref="TopMargin"/> above and below. Speed adds one
    /// <see cref="SpeedLookaheadSeconds"/> of travel along each axis.
    /// </summary>
    public static double ZoomToFit(FramedCreature creature, double speedX, double speedY, double viewWidth, double viewHeight)
    {
        var wide = Math.Max(creature.CentreX - creature.Left, creature.Right - creature.CentreX) / (_viewCentre - SideMargin);
        var standing = (creature.Bottom - creature.Top) / (GroundFromTop - TopMargin - Headroom);
        var centred = Math.Max(creature.CentreY - creature.Top, creature.Bottom - creature.CentreY) / (_viewCentre - TopMargin);
        var fit = Math.Min(MaxZoom, Math.Min(viewWidth / Math.Max(0, wide), viewHeight / Math.Max(0, Math.Max(standing, centred))));
        var shownWidth = (viewWidth / fit) + (Math.Abs(speedX) * SpeedLookaheadSeconds);
        var shownHeight = (viewHeight / fit) + (Math.Abs(speedY) * SpeedLookaheadSeconds);
        var zoom = Math.Min(viewWidth / shownWidth, viewHeight / shownHeight);
        return Math.Min(MaxZoom, Math.Max(zoom, viewWidth / MaxShownWidth));
    }

    /// <summary>
    /// How the view makes room for a <paramref name="shelf"/> above <paramref name="creature"/>
    /// shown at <paramref name="zoom"/> in a view <paramref name="viewHeight"/> tall: the highest
    /// the ground line goes, from the top, between <see cref="GroundFromTop"/> and
    /// <see cref="ShelfGroundFromTop"/> or the top of a <paramref name="scaleBand"/> along the
    /// bottom, whichever is higher up, and the zoom the view needs at most once the ground is as
    /// low as it goes, never smaller than keeps the creature <see cref="CreatureFloor"/> tall.
    /// A shelf the view already has room for keeps the ground where it is and asks no zoom.
    /// </summary>
    public static (double GroundFromTop, double Zoom) FitShelf(FramedCreature creature, double zoom, double viewHeight, double shelf, double scaleBand = 0)
    {
        var top = Math.Max(TopMargin, shelf) + Headroom;
        var tall = creature.Bottom - creature.Top;
        var lowest = Math.Max(GroundFromTop, Math.Min(ShelfGroundFromTop, 1 - scaleBand));
        var ground = Math.Clamp(top + (tall * zoom / viewHeight), GroundFromTop, lowest);
        if (tall <= 0)
        {
            return (ground, MaxZoom);
        }

        var room = Math.Max(lowest - top, CreatureFloor) * viewHeight / tall;
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

        _followX.SnapTo(creature.CentreX);
        _followY.SnapTo(creature.CentreY);
        var rest = Math.Log(ZoomToFit(creature, 0, 0, viewWidth, viewHeight));
        _logZoom = new HeldEase(rest, roomIsUp: false);
        _restLogZoom = new HeldEase(rest, roomIsUp: false);
        var (ground, zoom) = FitShelf(creature, Math.Exp(rest), viewHeight, Shelf, ScaleBand);
        _groundFromTop = new HeldEase(ground, roomIsUp: true);
        _shelfLogZoom = new HeldEase(Math.Log(zoom), roomIsUp: false);
        _groundY = groundY;
        _cutNotStepped = (creature, groundY, viewWidth, viewHeight);
        _safeZoneHolds = true;
        HasFrame = true;
        Place(creature, viewWidth, viewHeight);
    }

    /// <summary>
    /// Follows another creature, now framed as <paramref name="creature"/>: the view glides there,
    /// zoom and height included, and the jump between the two is not taken as speed. The safe zone
    /// holds it only once its box has glided inside, so the switch never jumps.
    /// </summary>
    public void Retarget(FramedCreature creature)
    {
        if (creature.IsUsable)
        {
            _followX.Retarget(creature.CentreX);
            _followY.Retarget(creature.CentreY);
            _safeZoneHolds = false;
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
        _groundY = groundY;
        _followX.Step(creature.CentreX, deltaSeconds);
        _followY.Step(creature.CentreY, deltaSeconds);
        _logZoom.Step(Math.Log(ZoomToFit(creature, _followX.Speed, _followY.Speed, viewWidth, viewHeight)), deltaSeconds);
        _restLogZoom.Step(Math.Log(ZoomToFit(creature, 0, 0, viewWidth, viewHeight)), deltaSeconds);
        StepShelfRoom(creature, viewHeight, deltaSeconds);
        Place(creature, viewWidth, viewHeight);
    }

    /// <summary>
    /// Eases only the room for the <see cref="Shelf"/> over <paramref name="deltaSeconds"/>, as
    /// while paused: the shelf opens and closes for a callout read then, but nothing else follows
    /// the creature. The safe zone still holds, so a shelf narrowing the view back may move it, and
    /// resuming never jumps. Does nothing before the first frame.
    /// </summary>
    public void StepShelf(FramedCreature creature, double viewWidth, double viewHeight, double deltaSeconds)
    {
        if (!HasFrame || !creature.IsUsable || !IsUsableView(0, viewWidth, viewHeight) || deltaSeconds <= 0)
        {
            return;
        }

        _cutNotStepped = null;
        StepShelfRoom(creature, viewHeight, deltaSeconds);
        Place(creature, viewWidth, viewHeight);
    }

    private void RecutIfNotStepped()
    {
        if (_cutNotStepped is var (creature, groundY, viewWidth, viewHeight))
        {
            Cut(creature, groundY, viewWidth, viewHeight);
        }
    }

    private static bool IsUsableView(double groundY, double viewWidth, double viewHeight) =>
        double.IsFinite(groundY) && viewWidth > 0 && viewHeight > 0
        && double.IsFinite(viewWidth) && double.IsFinite(viewHeight);

    /// <summary>The share of the way to its goal an eased value covers in <paramref name="deltaSeconds"/> at <paramref name="rate"/> per second.</summary>
    internal static double Ease(double rate, double deltaSeconds) => 1 - Math.Exp(-rate * deltaSeconds);

    // How far the view slides ahead of a creature moving at speed, given the room the view shows
    // beyond what it needs at rest: a sixth of the room speed added, so two thirds of it show in
    // front and one third behind.
    private static double Lead(double speed, double extraRoom) =>
        Math.Sign(speed) * Math.Min(Math.Abs(speed) * SpeedLookaheadSeconds, Math.Max(0, extraRoom)) / 6;

    // The safe zone's margin on one side, in world units: SafeMargin of the shown span, or half the
    // room the box's edge, reaching extent from the centre of mass, has there at rest when less.
    private static double SafeRoom(double extent, double restSpan, double shownSpan) =>
        Math.Clamp(((_viewCentre * restSpan) - extent) / (2 * restSpan), 0, SafeMargin) * shownSpan;

    // Measured against the zoom the creature alone needs, so the shelf's own zoom never feeds back.
    private void StepShelfRoom(FramedCreature creature, double viewHeight, double deltaSeconds)
    {
        var (ground, zoom) = FitShelf(creature, Math.Exp(_logZoom.Value), viewHeight, Shelf, ScaleBand);
        _groundFromTop.Step(ground, deltaSeconds);
        _shelfLogZoom.Step(Math.Log(zoom), deltaSeconds);
    }

    // Centred on the eased centre plus its lead, but never so high that the ground line rises above
    // its place: that holds a creature on the ground still and stops the lead into a fall at the
    // ground. Then the safe zone moves the view as little as keeps the box inside it; a box too
    // wide for both sides' margins is centred between them.
    private void Place(FramedCreature creature, double viewWidth, double viewHeight)
    {
        var shownWidth = viewWidth / Zoom;
        var shownHeight = viewHeight / Zoom;
        var restZoom = Math.Exp(_restLogZoom.Value);
        var restWidth = viewWidth / restZoom;
        var restHeight = viewHeight / restZoom;
        var x = _followX.Shown + Lead(_followX.Speed, shownWidth - restWidth);
        var centredY = _followY.Shown + Lead(_followY.Speed, shownHeight - restHeight);
        var y = Math.Min(centredY, _groundY - ((_groundFromTop.Value - _viewCentre) * shownHeight));

        // The camera centres the safe zone allows: right of lowestX keeps the box's right side in,
        // left of highestX its left side, and above highestY its top.
        var lowestX = creature.Right + SafeRoom(creature.Right - creature.CentreX, restWidth, shownWidth) - (shownWidth / 2);
        var highestX = creature.Left - SafeRoom(creature.CentreX - creature.Left, restWidth, shownWidth) + (shownWidth / 2);
        var highestY = creature.Top - SafeRoom(creature.CentreY - creature.Top, restHeight, shownHeight) + (shownHeight / 2);
        _safeZoneHolds |= lowestX <= x && x <= highestX && y <= highestY;
        if (_safeZoneHolds)
        {
            x = lowestX <= highestX ? Math.Clamp(x, lowestX, highestX) : (lowestX + highestX) / 2;
            y = Math.Min(y, highestY);
        }

        CameraX = x;
        CameraY = y;
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
