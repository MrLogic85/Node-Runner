namespace NodeRunner.Domain;

/// <summary>
/// A single attachment point on a creature. Physically it is where one or
/// more <see cref="BeamDef"/>s meet; it has no motion of its own beyond what
/// the beams attached to it do. See docs/CREATURE_MODEL.md.
/// </summary>
public sealed record NodeDef
{
    public NodeDef(int id, Vector2D position, double radius, string? name = null)
    {
        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Node id must be positive.");
        }

        if (!double.IsFinite(radius) || radius <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(radius), "Node radius must be finite and positive.");
        }

        Id = id;
        Position = position;
        Radius = radius;
        Name = name;
    }

    public int Id { get; }

    public Vector2D Position { get; }

    public double Radius { get; }

    public string? Name { get; }
}
