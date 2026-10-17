namespace NodeRunner.Domain;

/// <summary>
/// A Rubber: a passive part on one node that makes it grip the ground better
/// (<c>NodeRunner.Mechanics.Rubber</c>). It takes its own joint-part slot beside a Servo, a Wheel
/// and a Touch sensor, adds no radius, weight or network ports, and has no settings. See
/// docs/CREATURE_MODEL.md → "Rubber".
/// </summary>
public sealed record RubberDef : IJointPartDef
{
    /// <summary>How far its skin reaches outside its joint's drawn edge, in world units. It adds no collider and no tap area.</summary>
    public const double Skin = 3;

    public RubberDef(int id, int nodeId, string? name = null)
    {
        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Rubber id must be positive.");
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

    PartKind IPartDef.PartKind => PartKind.Rubber;

    public RubberDef WithName(string? name) => new(Id, NodeId, name);
}
