namespace NodeRunner.App.ViewModels;

/// <summary>
/// Where the Training camera aims along the ground (#668). It follows a centre point of the
/// followed creature, eased so a gait's wobble never shakes the view and a change of target glides
/// rather than jumps. Smoothing has two stages: the focus eases toward the centre, then the shown
/// point (<see cref="ShownX"/>) eases toward the aim, which makes a glide start gently. Both stages
/// trail a moving target, so the aim leads it by the creature's eased speed times that lag: a
/// creature at any steady speed stays at <see cref="FocusFromLeft"/>. This is the horizontal part;
/// <see cref="ArenaFraming"/> adds zoom and height. See <c>docs/TRAINING_LOOP.md</c> → Camera.
/// </summary>
public sealed class ArenaFollow
{
    /// <summary>How quickly the eased focus closes the gap to the centre point, per second.</summary>
    public const double EaseRate = 3;

    /// <summary>How quickly the shown point closes the gap to the aim, the second stage, per second.</summary>
    public const double ShownEaseRate = 4;

    /// <summary>
    /// How quickly the eased speed takes up the centre's speed, per second. Slow, so a gait's
    /// back-and-forth averages out instead of shaking the lead.
    /// </summary>
    public const double SpeedEaseRate = 1;

    /// <summary>Where the focus sits across the view, measured from the left (reference design).</summary>
    public const double FocusFromLeft = 0.43;

    /// <summary>How far both stages trail a target moving at a steady speed, in seconds.</summary>
    public const double LagSeconds = (1 / EaseRate) + (1 / ShownEaseRate);

    private const double _viewCentre = 0.5;

    private double _lastCentreX;

    /// <summary>False until the first <see cref="Step"/> or <see cref="SnapTo"/>.</summary>
    public bool HasFocus { get; private set; }

    /// <summary>The eased centre point, in world units.</summary>
    public double FocusX { get; private set; }

    /// <summary>The centre's eased speed, in world units per second.</summary>
    public double SpeedX { get; private set; }

    /// <summary>Where the camera's smoothing should head, in world units: the focus plus the lead that cancels the lag.</summary>
    public double AimX => FocusX + (SpeedX * LagSeconds);

    /// <summary>The point the camera shows at <see cref="FocusFromLeft"/>, in world units: the aim, eased again.</summary>
    public double ShownX { get; private set; }

    /// <summary>Puts the focus on <paramref name="centreX"/> at once, at rest, as on the first frame or a new trial.</summary>
    public void SnapTo(double centreX)
    {
        if (!double.IsFinite(centreX))
        {
            return;
        }

        FocusX = centreX;
        ShownX = centreX;
        _lastCentreX = centreX;
        SpeedX = 0;
        HasFocus = true;
    }

    /// <summary>
    /// Follows another creature, now at <paramref name="centreX"/>: the focus glides there on the
    /// next steps, and the jump between the two is not taken as speed.
    /// </summary>
    public void Retarget(double centreX)
    {
        if (double.IsFinite(centreX))
        {
            _lastCentreX = centreX;
        }
    }

    /// <summary>
    /// Eases the shown point toward the aim, then the focus and the speed toward <paramref name="centreX"/>, over
    /// <paramref name="deltaSeconds"/>; call <see cref="Retarget"/> first when the centre belongs to
    /// another creature. The first call snaps; a non-finite centre (a physics blow-up) leaves
    /// everything where it is.
    /// </summary>
    public void Step(double centreX, double deltaSeconds)
    {
        if (!HasFocus)
        {
            SnapTo(centreX);
            return;
        }

        if (!double.IsFinite(centreX) || deltaSeconds <= 0)
        {
            return;
        }

        // The shown point heads for the aim of the frame before, as Godot's own smoothing did: the
        // frame's delay offsets the exponential stages' shorter lag, so the lead stays right at a
        // steady speed.
        ShownX += (AimX - ShownX) * (1 - Math.Exp(-ShownEaseRate * deltaSeconds));
        var speed = (centreX - _lastCentreX) / deltaSeconds;
        _lastCentreX = centreX;
        SpeedX += (speed - SpeedX) * (1 - Math.Exp(-SpeedEaseRate * deltaSeconds));

        FocusX += (centreX - FocusX) * (1 - Math.Exp(-EaseRate * deltaSeconds));
    }

    /// <summary>The camera centre that shows <paramref name="aimX"/> at <see cref="FocusFromLeft"/> of a view <paramref name="viewWidth"/> wide.</summary>
    public static double CameraX(double aimX, double viewWidth) =>
        aimX + ((_viewCentre - FocusFromLeft) * viewWidth);
}
