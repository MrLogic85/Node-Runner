namespace NodeRunner.Domain;

/// <summary>
/// A Stepper (#455, #1036): a joint motor on one node that works like a clock. It holds its Target side in
/// notches against its Fixed side and, at the rate the brain picks, lets go and pushes it on to the next notch.
/// Its sides follow the Servo's rules (#1118): a link at the joint or the joint's Wheel, not both the same,
/// either null once that part is deleted. It shares the joint's hinge-part slot with a Servo, a Velocity motor,
/// a Brake and an Angle sensor (#1044); see <see cref="JointMotor"/>. See docs/CREATURE_MODEL.md → "Stepper".
/// </summary>
public sealed record StepperDef : IJointMotorDef
{
    /// <summary>A new Stepper's <see cref="Strength"/>: 150 N·m in world torque units.</summary>
    public const double DefaultStrength = 1500000;

    /// <summary>A new Stepper's <see cref="StepsPerTurn"/>.</summary>
    public const int DefaultStepsPerTurn = 8;

    /// <summary>The fewest notches a Stepper's turn can have.</summary>
    public const int MinStepsPerTurn = 2;

    /// <summary>The most notches a Stepper's turn can have.</summary>
    public const int MaxStepsPerTurn = 24;

    /// <summary>A new Stepper's <see cref="MaxFrequency"/>: 2 steps a second.</summary>
    public const double DefaultMaxFrequency = 2;

    public StepperDef(
        int id,
        int nodeId,
        int? fixedLinkId = null,
        int? targetLinkId = null,
        string? name = null,
        double strength = DefaultStrength,
        int stepsPerTurn = DefaultStepsPerTurn,
        double maxFrequency = DefaultMaxFrequency)
    {
        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Stepper id must be positive.");
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
            throw new ArgumentException("A stepper's fixed and target links must be different.");
        }

        if (!double.IsFinite(strength) || strength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(strength), "Stepper strength must be finite and positive.");
        }

        if (stepsPerTurn is < MinStepsPerTurn or > MaxStepsPerTurn)
        {
            throw new ArgumentOutOfRangeException(nameof(stepsPerTurn), $"A stepper has {MinStepsPerTurn} to {MaxStepsPerTurn} steps per turn.");
        }

        if (!double.IsFinite(maxFrequency) || maxFrequency <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxFrequency), "Stepper max frequency must be finite and positive.");
        }

        Id = id;
        NodeId = nodeId;
        FixedLinkId = fixedLinkId;
        TargetLinkId = targetLinkId;
        Name = name;
        Strength = strength;
        StepsPerTurn = stepsPerTurn;
        MaxFrequency = maxFrequency;
    }

    public int Id { get; }

    public int NodeId { get; }

    /// <summary>The side the notches turn with: a link at the joint or the joint's Wheel (#1118), or null once deleted.</summary>
    public int? FixedLinkId { get; }

    /// <summary>The side stepped from notch to notch: a link at the joint or the joint's Wheel (#1118), or null once deleted.</summary>
    public int? TargetLinkId { get; }

    public string? Name { get; }

    /// <summary>Max strength: the most torque it pushes with while it steps, in world torque units.</summary>
    public double Strength { get; }

    /// <summary>How many notches make a full turn.</summary>
    public int StepsPerTurn { get; }

    /// <summary>The most steps it may take each second.</summary>
    public double MaxFrequency { get; }

    PartKind IPartDef.PartKind => PartKind.Stepper;

    public StepperDef WithName(string? name) => new(Id, NodeId, FixedLinkId, TargetLinkId, name, Strength, StepsPerTurn, MaxFrequency);

    public StepperDef WithSettings(double strength, int stepsPerTurn, double maxFrequency) =>
        new(Id, NodeId, FixedLinkId, TargetLinkId, Name, strength, stepsPerTurn, maxFrequency);

    /// <summary>The Stepper with new sides. It keeps its id, like a Servo (<see cref="ServoDef.WithLinks"/>).</summary>
    public StepperDef WithLinks(int? fixedLinkId, int? targetLinkId) =>
        new(Id, NodeId, fixedLinkId, targetLinkId, Name, Strength, StepsPerTurn, MaxFrequency);
}
