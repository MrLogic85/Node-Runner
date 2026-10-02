namespace NodeRunner.Domain;

/// <summary>
/// The accelerometer's pure math (#127): a proof mass on a damped spring in its beam's sensor
/// frame. Forces are specific forces in g; the frame's X is along the beam and Y is up as built.
/// Stateless and shared by the sim and the visual (#576).
/// See docs/CREATURE_MODEL.md.
/// </summary>
public static class Accelerometer
{
    public const double NaturalFrequencyHz = 3.0;

    public const double DampingRatio = 0.7;

    public const double AngularFrequency = Math.Tau * NaturalFrequencyHz;

    public const double ReferenceDisplacement = 1.0 / (AngularFrequency * AngularFrequency);

    /// <summary>The longest integration substep; a faster time scale (2×, 4×) takes several per tick.</summary>
    public const double MaxSubstep = 1.0 / 60;

    /// <summary>The brain inputs one accelerometer gives, in order: along and across its beam.</summary>
    public static IReadOnlyList<string> ReadingNames { get; } = ["along", "across"];

    /// <summary>The machine keys of its input ports (<see cref="BrainPorts"/>), in the order of <see cref="ReadingNames"/>. Never change one.</summary>
    public static IReadOnlyList<string> ChannelKeys { get; } = ["along", "across"];

    /// <summary>The proof mass at rest under a constant specific force.</summary>
    public static ProofMass Rest(Vector2D specificForceG) =>
        new(Divide(Negate(specificForceG), AngularFrequency * AngularFrequency), new Vector2D(0, 0));

    /// <summary>
    /// Advances the damped spring by <paramref name="dt"/> with the force held constant, in equal
    /// semi-implicit Euler substeps of at most <see cref="MaxSubstep"/> so it stays stable at any
    /// time scale.
    /// </summary>
    public static ProofMass Step(ProofMass state, Vector2D specificForceG, double dt)
    {
        if (!double.IsFinite(dt) || dt <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dt), "Time step must be finite and positive.");
        }

        var substeps = (int)Math.Ceiling(dt / MaxSubstep);
        var h = dt / substeps;
        for (var i = 0; i < substeps; i++)
        {
            state = Substep(state, specificForceG, h);
        }

        return state;
    }

    /// <summary>The two brain inputs, along and across: <c>tanh(-d / d_ref)</c>, so 1 g up reads tanh 1.</summary>
    public static Vector2D Reading(ProofMass state) =>
        new(
            Math.Tanh(-state.Displacement.X / ReferenceDisplacement),
            Math.Tanh(-state.Displacement.Y / ReferenceDisplacement));

    /// <summary>How far 1 g moves the drawn weight, as a share of its travel from the frame's centre (#576).</summary>
    public const double WeightTravelPerG = 0.5;

    /// <summary>
    /// Where the drawn weight sits (#576): the proof-mass displacement in units of the weight's
    /// travel from the frame's centre, clamped to -1…1 per axis. X is along the beam and Y is down
    /// the picture, so a level beam at rest gives (0, <see cref="WeightTravelPerG"/>).
    /// </summary>
    public static Vector2D WeightOffset(ProofMass state) =>
        new(
            Math.Clamp(state.Displacement.X / ReferenceDisplacement * WeightTravelPerG, -1, 1),
            Math.Clamp(-state.Displacement.Y / ReferenceDisplacement * WeightTravelPerG, -1, 1));

    /// <summary>The drawn weight at rest under gravity for a beam at <paramref name="beamRotation"/> (Build's pose).</summary>
    public static Vector2D RestWeightOffset(double beamRotation, int upSign) =>
        WeightOffset(Rest(ToSensorFrame(new Vector2D(0, -1), beamRotation, upSign)));

    /// <summary>
    /// Which local side of a beam (local +x from NodeA to NodeB) is "up" as built: local
    /// (0, UpSign) points up in the built pose (y grows downward). A vertical beam uses -1.
    /// </summary>
    public static int UpSign(Vector2D nodeA, Vector2D nodeB)
    {
        var dx = nodeB.X - nodeA.X;
        return dx < 0 ? 1 : -1;
    }

    /// <summary>
    /// The specific force, in g, on a point moving with world <paramref name="acceleration"/>
    /// (y down) under <paramref name="gravity"/>, both in world units per second squared: at rest
    /// it is (0, -1), 1 g up. The sim and Build's moving weight (#576) both start here.
    /// </summary>
    public static Vector2D SpecificForce(Vector2D acceleration, double gravity)
    {
        if (!double.IsFinite(gravity) || gravity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(gravity), "Gravity must be finite and positive.");
        }

        return new Vector2D(acceleration.X / gravity, (acceleration.Y - gravity) / gravity);
    }

    /// <summary>A world specific force as (along, up) in a beam's sensor frame at its current rotation.</summary>
    public static Vector2D ToSensorFrame(Vector2D worldSpecificForceG, double beamRotation, int upSign)
    {
        if (upSign is not (-1 or 1))
        {
            throw new ArgumentOutOfRangeException(nameof(upSign), "Up sign must be -1 or 1.");
        }

        if (!double.IsFinite(beamRotation))
        {
            throw new ArgumentOutOfRangeException(nameof(beamRotation), "Beam rotation must be finite.");
        }

        var cos = Math.Cos(beamRotation);
        var sin = Math.Sin(beamRotation);
        var localX = (worldSpecificForceG.X * cos) + (worldSpecificForceG.Y * sin);
        var localY = (-worldSpecificForceG.X * sin) + (worldSpecificForceG.Y * cos);
        return new Vector2D(localX * -upSign, localY * upSign);
    }

    private static ProofMass Substep(ProofMass state, Vector2D specificForceG, double h)
    {
        var spring = Multiply(state.Displacement, -(AngularFrequency * AngularFrequency));
        var damping = Multiply(state.Velocity, -(2 * DampingRatio * AngularFrequency));
        var acceleration = Add(Add(Negate(specificForceG), spring), damping);
        var velocity = Add(state.Velocity, Multiply(acceleration, h));
        return new ProofMass(Add(state.Displacement, Multiply(velocity, h)), velocity);
    }

    private static Vector2D Add(Vector2D a, Vector2D b) => new(a.X + b.X, a.Y + b.Y);

    private static Vector2D Divide(Vector2D value, double divisor) => new(value.X / divisor, value.Y / divisor);

    private static Vector2D Multiply(Vector2D value, double multiplier) => new(value.X * multiplier, value.Y * multiplier);

    private static Vector2D Negate(Vector2D value) => new(-value.X, -value.Y);
}
