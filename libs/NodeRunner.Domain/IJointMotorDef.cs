namespace NodeRunner.Domain;

/// <summary>
/// A joint motor: a Servo (#452) or a Velocity motor (#454). It sits in its joint's one "motor or
/// brake" slot (#1044) and turns its Target side against its Fixed side, each a link at the joint
/// or the joint's Wheel (#1118), or null once that part is deleted. See <see cref="JointMotor"/>.
/// </summary>
public interface IJointMotorDef
{
    int Id { get; }

    int NodeId { get; }

    int? FixedLinkId { get; }

    int? TargetLinkId { get; }

    /// <summary>Which kind of motor this is, as it is selected.</summary>
    CreatureElementKind Kind { get; }
}
