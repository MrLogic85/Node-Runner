namespace NodeRunner.App.ViewModels;

/// <summary>
/// Where the Training camera aims along one axis (#668, #1169). It follows a centre point of the
/// followed creature, eased so a gait's wobble never shakes the view and a change of target glides
/// rather than jumps. Smoothing has two stages: the focus eases toward the centre, then the shown
/// point (<see cref="Shown"/>) eases toward the aim, which makes a glide start gently. Both stages
/// trail a moving target, so the aim leads it by the creature's eased speed times that lag: at any
/// steady speed the shown point stays on the centre. <see cref="ArenaFraming"/> runs one per axis
/// and adds zoom, lead and the ground. See <c>docs/TRAINING_LOOP.md</c> → Camera.
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

    /// <summary>How far both stages trail a target moving at a steady speed, in seconds.</summary>
    public const double LagSeconds = (1 / EaseRate) + (1 / ShownEaseRate);

    private double _lastCentre;

    /// <summary>False until the first <see cref="Step"/> or <see cref="SnapTo"/>.</summary>
    public bool HasFocus { get; private set; }

    /// <summary>The eased centre point, in world units.</summary>
    public double Focus { get; private set; }

    /// <summary>The centre's eased, signed speed, in world units per second.</summary>
    public double Speed { get; private set; }

    /// <summary>Where the camera's smoothing should head, in world units: the focus plus the lead that cancels the lag.</summary>
    public double Aim => Focus + (Speed * LagSeconds);

    /// <summary>The point the camera shows, in world units: the aim, eased again.</summary>
    public double Shown { get; private set; }

    /// <summary>Puts the focus on <paramref name="centre"/> at once, at rest, as on the first frame or a new trial.</summary>
    public void SnapTo(double centre)
    {
        if (!double.IsFinite(centre))
        {
            return;
        }

        Focus = centre;
        Shown = centre;
        _lastCentre = centre;
        Speed = 0;
        HasFocus = true;
    }

    /// <summary>
    /// Follows another creature, now at <paramref name="centre"/>: the focus glides there on the
    /// next steps, and the jump between the two is not taken as speed.
    /// </summary>
    public void Retarget(double centre)
    {
        if (double.IsFinite(centre))
        {
            _lastCentre = centre;
        }
    }

    /// <summary>
    /// Eases the shown point toward the aim, then the focus and the speed toward <paramref name="centre"/>, over
    /// <paramref name="deltaSeconds"/>; call <see cref="Retarget"/> first when the centre belongs to
    /// another creature. The first call snaps; a non-finite centre (a physics blow-up) leaves
    /// everything where it is.
    /// </summary>
    public void Step(double centre, double deltaSeconds)
    {
        if (!HasFocus)
        {
            SnapTo(centre);
            return;
        }

        if (!double.IsFinite(centre) || deltaSeconds <= 0)
        {
            return;
        }

        // The shown point heads for the aim of the frame before, as Godot's own smoothing did: the
        // frame's delay offsets the exponential stages' shorter lag, so the lead stays right at a
        // steady speed.
        Shown += (Aim - Shown) * (1 - Math.Exp(-ShownEaseRate * deltaSeconds));
        var speed = (centre - _lastCentre) / deltaSeconds;
        _lastCentre = centre;
        Speed += (speed - Speed) * (1 - Math.Exp(-SpeedEaseRate * deltaSeconds));

        Focus += (centre - Focus) * (1 - Math.Exp(-EaseRate * deltaSeconds));
    }
}
