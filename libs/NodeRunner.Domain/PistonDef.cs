namespace NodeRunner.Domain;

/// <summary>
/// A Piston (#451): a powered link between two nodes that pushes them apart or pulls them together
/// along the line between them. It is not a beam: it does not hold its length, so it adds no
/// rigidity. Its length as built is the distance between its nodes in the
/// drawing. The brain sets where it goes and how much of <see cref="Strength"/> it uses
/// (<c>Piston</c> in NodeRunner.Mechanics). See docs/CREATURE_MODEL.md.
/// </summary>
public sealed record PistonDef
{
    /// <summary>A new Piston's <see cref="Strength"/>: 150 N at 100 world units per metre.</summary>
    public const double DefaultStrength = 15000;

    /// <summary>A new Piston's <see cref="Stroke"/>: ±30% of its built length.</summary>
    public const double DefaultStroke = 0.3;

    /// <summary>A new Piston's <see cref="MaxSpeed"/>: 2 m/s at 100 world units per metre.</summary>
    public const double DefaultMaxSpeed = 200;

    public PistonDef(
        int id,
        int nodeA,
        int nodeB,
        string? name = null,
        double strength = DefaultStrength,
        double stroke = DefaultStroke,
        double maxSpeed = DefaultMaxSpeed)
    {
        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Piston id must be positive.");
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
            throw new ArgumentException("A piston must connect two different nodes.");
        }

        if (!double.IsFinite(strength) || strength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(strength), "Piston strength must be finite and positive.");
        }

        if (!double.IsFinite(stroke) || stroke <= 0 || stroke >= 1)
        {
            throw new ArgumentOutOfRangeException(nameof(stroke), "Piston stroke must be a share of its length between 0 and 1.");
        }

        if (!double.IsFinite(maxSpeed) || maxSpeed <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxSpeed), "Piston max speed must be finite and positive.");
        }

        Id = id;
        NodeA = nodeA;
        NodeB = nodeB;
        Name = name;
        Strength = strength;
        Stroke = stroke;
        MaxSpeed = maxSpeed;
    }

    public int Id { get; }

    public int NodeA { get; }

    public int NodeB { get; }

    public string? Name { get; }

    /// <summary>The most force the Piston can push or pull with, in world units (mass × units/s²).</summary>
    public double Strength { get; }

    /// <summary>How far it moves each way from its built length, as a share of that length: 0.3 is ±30%.</summary>
    public double Stroke { get; }

    /// <summary>The fastest it extends or retracts, in world units per second.</summary>
    public double MaxSpeed { get; }

    public PistonDef WithName(string? name) => new(Id, NodeA, NodeB, name, Strength, Stroke, MaxSpeed);

    public PistonDef WithSettings(double strength, double stroke, double maxSpeed) =>
        new(Id, NodeA, NodeB, Name, strength, stroke, maxSpeed);
}
