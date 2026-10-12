namespace NodeRunner.Domain;

/// <summary>
/// What fills a joint's one "motor or brake" slot (#1044): a joint motor (<see cref="IJointMotorDef"/>)
/// or a <see cref="BrakeDef"/>. It has a Fixed and a Target side, each a link at the joint or the
/// joint's Wheel (#1118), or null once that part is deleted.
/// </summary>
public interface IMotorOrBrakeDef
{
    int Id { get; }

    int NodeId { get; }

    int? FixedLinkId { get; }

    int? TargetLinkId { get; }

    /// <summary>Which kind of part this is, as it is selected: a Servo, a Velocity motor or a Brake.</summary>
    CreatureElementKind Kind { get; }
}
