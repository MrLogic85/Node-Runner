namespace NodeRunner.Domain;

/// <summary>
/// What fills a joint's one "hinge part" slot (#1044): a joint motor (<see cref="IJointMotorDef"/>),
/// a <see cref="BrakeDef"/> or an <see cref="AngleSensorDef"/>. It has a Fixed and a Target side, each a
/// link at the joint or the joint's Wheel (#1118), or null once that part is deleted.
/// </summary>
public interface IHingePartDef
{
    int Id { get; }

    int NodeId { get; }

    int? FixedLinkId { get; }

    int? TargetLinkId { get; }

    /// <summary>Which kind of part this is, as it is selected: a Servo, a Velocity motor, a Brake or an Angle sensor.</summary>
    CreatureElementKind Kind { get; }
}
