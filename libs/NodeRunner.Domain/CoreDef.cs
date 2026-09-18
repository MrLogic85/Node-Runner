namespace NodeRunner.Domain;

/// <summary>
/// A sensor package mounted on a node. A core is NOT the neural model — it is
/// a source of sensor readings (rays, pitch, elevation, speed) that feed into
/// the model, alongside the sensors produced by each node's motor
/// connections. See docs/CREATURE_MODEL.md.
/// </summary>
public sealed record CoreDef
{
    public CoreDef(int nodeIndex)
    {
        if (nodeIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nodeIndex), "Node index must be non-negative.");
        }

        NodeIndex = nodeIndex;
    }

    public int NodeIndex { get; }
}
