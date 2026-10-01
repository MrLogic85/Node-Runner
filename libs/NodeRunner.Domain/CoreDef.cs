namespace NodeRunner.Domain;

/// <summary>
/// A sensor package mounted on a node. A core is NOT the neural model — it is
/// a source of sensor readings (rays, pitch, elevation, speed) that feed into
/// the model, alongside the sensors produced by each node's motor
/// connections. See docs/CREATURE_MODEL.md.
/// </summary>
public sealed record CoreDef
{
    public CoreDef(int id, int nodeId, string? name = null)
    {
        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Core id must be positive.");
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
}
