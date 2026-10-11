namespace NodeRunner.Domain;

/// <summary>
/// A Touch sensor (#665): a sensor on one node that tells the brain whether that node touches the
/// ground. It is the first sensor that does not sit on a beam, since a node is a body of its own
/// (#127). It sits next to the node's joint parts and takes no joint-part slot, adds no radius and
/// has no settings. Its one input is <see cref="BrainPorts.TouchChannel"/>. See
/// docs/CREATURE_MODEL.md → "Touch sensor".
/// </summary>
public sealed record TouchSensorDef
{
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

    public TouchSensorDef WithName(string? name) => new(Id, NodeId, name);
}
