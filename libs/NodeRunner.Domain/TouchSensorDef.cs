namespace NodeRunner.Domain;

/// <summary>
/// A Touch sensor: a sensor on one node that tells the network how close that node is to
/// touching the ground (<c>NodeRunner.Mechanics.Touch.Reading</c>). It sits on a node, not a
/// beam, since a node is a body of its own. It takes its own joint-part slot beside a Servo
/// and a Wheel, adds no radius and has no settings. Its one input is <see cref="NetworkPorts.TouchChannel"/>. See
/// docs/CREATURE_MODEL.md → "Touch sensor".
/// </summary>
public sealed record TouchSensorDef : IJointPartDef
{
    /// <summary>
    /// How far its cushion reaches outside its joint's drawn edge, in world units:
    /// how far it feels the ground. It adds no radius, no collider and no tap area.
    /// </summary>
    public const double Cushion = 5;

    public TouchSensorDef(int id, int nodeId, string? name = null)
    {
        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Touch sensor id must be positive.");
        }

        if (nodeId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nodeId), "Node id must be positive.");
        }

        Id = id;
        NodeId = nodeId;
        Name = name;
    }

    public int Id { get; }

    public int NodeId { get; }

    public string? Name { get; }

    PartKind IPartDef.PartKind => PartKind.TouchSensor;

    IPartDef IPartDef.WithName(string? name) => WithName(name);

    public TouchSensorDef WithName(string? name) => new(Id, NodeId, name);
}
