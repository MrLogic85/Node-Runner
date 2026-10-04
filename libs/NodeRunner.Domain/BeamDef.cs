namespace NodeRunner.Domain;

/// <summary>
/// A rigid, fixed-length connection between two nodes. A beam never changes
/// length; beams turn freely at the nodes they share unless a closed triangle
/// locks them (see <c>RigidTriangles</c> in NodeRunner.Mechanics). See docs/CREATURE_MODEL.md.
/// </summary>
public sealed record BeamDef
{
    public BeamDef(int id, int nodeA, int nodeB, string? name = null)
    {
        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Beam id must be positive.");
        }

        if (nodeA <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nodeA), "Node id must be positive.");
        }

        if (nodeB <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nodeB), "Node id must be positive.");
        }

        if (nodeA == nodeB)
        {
            throw new ArgumentException("A beam must connect two different nodes.");
        }

        Id = id;
        NodeA = nodeA;
        NodeB = nodeB;
        Name = name;
    }

    public int Id { get; }

    public int NodeA { get; }

    public int NodeB { get; }

    public string? Name { get; }
}
