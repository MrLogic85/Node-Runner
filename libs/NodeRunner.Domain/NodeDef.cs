namespace NodeRunner.Domain;

/// <summary>
/// A single attachment point on a creature. Physically it is where one or
/// more <see cref="BeamDef"/>s meet; it has no motion of its own beyond what
/// the beams attached to it do. See docs/CREATURE_MODEL.md.
/// </summary>
public sealed record NodeDef
{
    /// <summary>
    /// A plain joint's radius (#626): it is drawn and collides at this size. Its look is in
    /// docs/UI_DIRECTION.md.
    /// </summary>
    public const double PlainJointRadius = 15;

    public static double RadiusWithServo(bool hasServo) => hasServo ? ServoDef.JointRadius : PlainJointRadius;

    public NodeDef(int id, Vector2D position, string? name = null)
    {
        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Node id must be positive.");
        }

        Id = id;
        Position = position;
        Name = name;
    }

    public int Id { get; }

    public Vector2D Position { get; }

    public string? Name { get; }
}
