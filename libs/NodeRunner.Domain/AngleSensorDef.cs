namespace NodeRunner.Domain;

/// <summary>
/// An Angle sensor (#1201, #1204): a joint sensor on one node that tells the brain the angle between
/// its Fixed and Target sides and how fast it changes (<c>NodeRunner.Mechanics.AngleSensor</c>). It fills
/// the joint's motor-or-brake slot and takes its sides by the Servo's rules (<see cref="IMotorOrBrakeDef"/>),
/// but it is no joint motor: it has inputs only and applies no force, so it is not in
/// <see cref="CreatureDef.JointMotors"/>. It has no settings. See docs/CREATURE_MODEL.md → "Angle sensor".
/// </summary>
public sealed record AngleSensorDef : IMotorOrBrakeDef
{
    public AngleSensorDef(int id, int nodeId, int? fixedLinkId = null, int? targetLinkId = null, string? name = null)
    {
        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Angle sensor id must be positive.");
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
            throw new ArgumentException("An angle sensor's fixed and target links must be different.");
        }

        Id = id;
        NodeId = nodeId;
        FixedLinkId = fixedLinkId;
        TargetLinkId = targetLinkId;
        Name = name;
    }

    public int Id { get; }

    public int NodeId { get; }

    /// <summary>The side measured from: a link at the joint or the joint's Wheel (#1118), or null once deleted.</summary>
    public int? FixedLinkId { get; }

    /// <summary>The side whose angle from the Fixed side it reads: a link at the joint or the joint's Wheel (#1118), or null once deleted.</summary>
    public int? TargetLinkId { get; }

    public string? Name { get; }

    CreatureElementKind IMotorOrBrakeDef.Kind => CreatureElementKind.AngleSensor;

    public AngleSensorDef WithName(string? name) => new(Id, NodeId, FixedLinkId, TargetLinkId, name);

    /// <summary>The sensor with new sides. It keeps its id, like a Servo (<see cref="ServoDef.WithLinks"/>).</summary>
    public AngleSensorDef WithLinks(int? fixedLinkId, int? targetLinkId) => new(Id, NodeId, fixedLinkId, targetLinkId, Name);
}
