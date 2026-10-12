namespace NodeRunner.Domain;

/// <summary>
/// A Velocity motor (#454): a powered joint part on one node that turns its Target side against
/// its Fixed side at the speed the brain chooses, with no end stops. Its sides follow the Servo's
/// rules (#1118): a link at the joint or the joint's Wheel, not both the same, either null once
/// that part is deleted. It shares the joint's motor-or-brake slot with a Servo and a Brake (#1044); see
/// <see cref="JointMotor"/>. Players know it as the Motor.
/// </summary>
public sealed record VelocityMotorDef : IJointMotorDef
{
    /// <summary>
    /// A new Velocity motor's <see cref="Strength"/>: 40 N·m at 100 world units per metre, less
    /// than a Servo's, so a Motor-driven creature struggles up hills.
    /// </summary>
    public const double DefaultStrength = 400000;

    /// <summary>A new Velocity motor's <see cref="MaxSpeed"/>: 60 rpm, like a Servo.</summary>
    public const double DefaultMaxSpeed = ServoDef.DefaultMaxSpeed;

    /// <summary>A new Velocity motor's <see cref="RiseTime"/>: 0.2 s, like a Servo.</summary>
    public const double DefaultRiseTime = ServoDef.DefaultRiseTime;

    public VelocityMotorDef(
        int id,
        int nodeId,
        int? fixedLinkId = null,
        int? targetLinkId = null,
        string? name = null,
        double strength = DefaultStrength,
        double maxSpeed = DefaultMaxSpeed,
        double riseTime = DefaultRiseTime)
    {
        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Velocity motor id must be positive.");
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
            throw new ArgumentException("A velocity motor's fixed and target links must be different.");
        }

        if (!double.IsFinite(strength) || strength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(strength), "Velocity motor strength must be finite and positive.");
        }

        if (!double.IsFinite(maxSpeed) || maxSpeed <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxSpeed), "Velocity motor max speed must be finite and positive.");
        }

        if (!double.IsFinite(riseTime) || riseTime <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(riseTime), "Velocity motor rise time must be finite and positive.");
        }

        Id = id;
        NodeId = nodeId;
        FixedLinkId = fixedLinkId;
        TargetLinkId = targetLinkId;
        Name = name;
        Strength = strength;
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

    /// <summary>The most torque the motor can apply, in world torque units.</summary>
    public double Strength { get; }

    /// <summary>The fastest speed the brain can choose, in radians per second.</summary>
    public double MaxSpeed { get; }

    /// <summary>How long, in seconds, its torque takes to build up to the strength the brain chose.</summary>
    public double RiseTime { get; }

    CreatureElementKind IMotorOrBrakeDef.Kind => CreatureElementKind.VelocityMotor;

    public VelocityMotorDef WithName(string? name) => new(Id, NodeId, FixedLinkId, TargetLinkId, name, Strength, MaxSpeed, RiseTime);

    public VelocityMotorDef WithSettings(double strength, double maxSpeed, double riseTime) =>
        new(Id, NodeId, FixedLinkId, TargetLinkId, Name, strength, maxSpeed, riseTime);

    /// <summary>The motor with new sides under <paramref name="newId"/>: a side change gives it a new id, like a Servo.</summary>
    public VelocityMotorDef WithLinks(int? fixedLinkId, int? targetLinkId, int newId) =>
        new(newId, NodeId, fixedLinkId, targetLinkId, Name, Strength, MaxSpeed, RiseTime);
}
