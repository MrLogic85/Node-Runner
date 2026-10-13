namespace NodeRunner.Domain;

/// <summary>
/// A Servo (#452): a powered joint part on one node. It holds one side fixed and turns another
/// side relative to it. A side is a link touching its joint, or the Wheel on its joint, a one-sided
/// link whose direction is its own turn (#1118). Either reference may be missing after that part is
/// deleted; readiness then blocks training until the player picks a replacement or deletes the Servo.
/// Its joint radius and side rules are <see cref="JointMotor"/>'s.
/// </summary>
public sealed record ServoDef : IJointMotorDef
{
    /// <summary>A new Servo's <see cref="Strength"/>: 50 N·m at 100 world units per metre.</summary>
    public const double DefaultStrength = 500000;

    /// <summary>A new Servo's <see cref="Range"/>: 180° total swing.</summary>
    public const double DefaultRange = Math.PI;

    /// <summary>A new Servo's <see cref="Start"/>: the built angle sits midway between the stops.</summary>
    public const double DefaultStart = 0.5;

    /// <summary>A new Servo's <see cref="MaxSpeed"/>: 60 rpm, one turn a second.</summary>
    public const double DefaultMaxSpeed = Math.Tau;

    /// <summary>A new Servo's <see cref="RiseTime"/>: 0.2 s (#801), like a Piston.</summary>
    public const double DefaultRiseTime = 0.2;

    public ServoDef(
        int id,
        int nodeId,
        int? fixedLinkId = null,
        int? targetLinkId = null,
        string? name = null,
        double strength = DefaultStrength,
        double range = DefaultRange,
        double start = DefaultStart,
        double maxSpeed = DefaultMaxSpeed,
        double riseTime = DefaultRiseTime)
    {
        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Servo id must be positive.");
        }

        if (nodeId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nodeId), "Node id must be positive.");
        }

        if (fixedLinkId is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fixedLinkId), "Link id must be positive when present.");
        }

        if (targetLinkId is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(targetLinkId), "Link id must be positive when present.");
        }

        if (fixedLinkId is { } fixedId && targetLinkId == fixedId)
        {
            throw new ArgumentException("A servo's fixed and target links must be different.");
        }

        if (!double.IsFinite(strength) || strength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(strength), "Servo strength must be finite and positive.");
        }

        if (!double.IsFinite(range) || range < Degrees(20) || range > Math.Tau)
        {
            throw new ArgumentOutOfRangeException(nameof(range), "Servo range must be between 20° and 360°.");
        }

        if (!double.IsFinite(start) || start < 0 || start > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(start), "Servo start position must be a share from 0 to 1.");
        }

        if (!double.IsFinite(maxSpeed) || maxSpeed <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxSpeed), "Servo max speed must be finite and positive.");
        }

        if (!double.IsFinite(riseTime) || riseTime <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(riseTime), "Servo rise time must be finite and positive.");
        }

        Id = id;
        NodeId = nodeId;
        FixedLinkId = fixedLinkId;
        TargetLinkId = targetLinkId;
        Name = name;
        Strength = strength;
        Range = range;
        Start = start;
        MaxSpeed = maxSpeed;
        RiseTime = riseTime;
    }

    public int Id { get; }

    public int NodeId { get; }

    /// <summary>The side held still: a link at the joint or the joint's Wheel (#1118), or null once deleted.</summary>
    public int? FixedLinkId { get; }

    /// <summary>The side turned: a link at the joint or the joint's Wheel (#1118), or null once deleted.</summary>
    public int? TargetLinkId { get; }

    public string? Name { get; }

    /// <summary>The most torque the Servo can apply, in world torque units.</summary>
    public double Strength { get; }

    /// <summary>The total angular swing between the range ends, in radians.</summary>
    public double Range { get; }

    /// <summary>Where the built angle sits in the range, 0 at the lower end and 1 at the upper.</summary>
    public double Start { get; }

    /// <summary>The fastest target angular speed, in radians per second.</summary>
    public double MaxSpeed { get; }

    /// <summary>How long, in seconds, its torque takes to build up to the strength the brain chose.</summary>
    public double RiseTime { get; }

    PartKind IPartDef.PartKind => PartKind.Servo;

    public ServoDef WithName(string? name) => new(Id, NodeId, FixedLinkId, TargetLinkId, name, Strength, Range, Start, MaxSpeed, RiseTime);

    public ServoDef WithSettings(double strength, double range, double start, double maxSpeed, double riseTime) =>
        new(Id, NodeId, FixedLinkId, TargetLinkId, Name, strength, range, start, maxSpeed, riseTime);

    /// <summary>
    /// The Servo with new sides. It keeps its id, and so its brain ports and training, through every
    /// side change (<c>docs/CREATURE_MODEL.md</c> → "Editing identity rules").
    /// </summary>
    public ServoDef WithLinks(int? fixedLinkId, int? targetLinkId) =>
        new(Id, NodeId, fixedLinkId, targetLinkId, Name, Strength, Range, Start, MaxSpeed, RiseTime);

    private static double Degrees(double value) => value * Math.PI / 180;
}
