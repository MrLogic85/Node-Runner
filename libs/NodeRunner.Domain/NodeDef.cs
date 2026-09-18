namespace NodeRunner.Domain;

/// <summary>
/// A single attachment point on a creature. Physically it is where one or
/// more <see cref="BeamDef"/>s meet; it has no motion of its own beyond what
/// the beams attached to it do. See docs/CREATURE_MODEL.md.
/// </summary>
public sealed record NodeDef
{
    public NodeDef(Vector2D position, double radius)
    {
        if (!double.IsFinite(radius) || radius <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(radius), "Node radius must be finite and positive.");
        }

        Position = position;
        Radius = radius;
    }

    public Vector2D Position { get; }

    public double Radius { get; }
}
