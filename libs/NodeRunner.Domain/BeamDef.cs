namespace NodeRunner.Domain;

/// <summary>
/// A rigid, fixed-length connection between two nodes. A beam never changes
/// length; a creature moves by rotating beams relative to each other at the
/// nodes they share (see <see cref="MotorTopology"/>). See
/// docs/CREATURE_MODEL.md.
/// </summary>
public sealed record BeamDef
{
    public BeamDef(int nodeA, int nodeB)
    {
        if (nodeA < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nodeA), "Node index must be non-negative.");
        }

        if (nodeB < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nodeB), "Node index must be non-negative.");
        }

        if (nodeA == nodeB)
        {
            throw new ArgumentException("A beam must connect two different nodes.");
        }

        NodeA = nodeA;
        NodeB = nodeB;
    }

    public int NodeA { get; }

    public int NodeB { get; }
}
