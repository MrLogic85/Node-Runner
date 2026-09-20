namespace NodeRunner.Domain;

/// <summary>
/// Three nodes whose connecting beams form a closed, rigid triangle. Node
/// indices are stored in sorted order so the same triangle has one identity
/// regardless of which beam pair discovered it.
/// </summary>
public sealed record RigidTriangleDef
{
    public RigidTriangleDef(int nodeA, int nodeB, int nodeC)
    {
        if (nodeA < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nodeA), "Node index must be non-negative.");
        }

        if (nodeB < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nodeB), "Node index must be non-negative.");
        }

        if (nodeC < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nodeC), "Node index must be non-negative.");
        }

        if (nodeA == nodeB || nodeA == nodeC || nodeB == nodeC)
        {
            throw new ArgumentException("A rigid triangle must contain three distinct nodes.");
        }

        var nodes = new[] { nodeA, nodeB, nodeC };
        Array.Sort(nodes);
        NodeA = nodes[0];
        NodeB = nodes[1];
        NodeC = nodes[2];
    }

    public int NodeA { get; }

    public int NodeB { get; }

    public int NodeC { get; }
}
