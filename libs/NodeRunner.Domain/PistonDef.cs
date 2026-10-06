namespace NodeRunner.Domain;

/// <summary>
/// A Piston (#451): a powered link between two nodes that pushes them apart or pulls them together
/// along the line between them. It is not a beam: it does not hold its length, so it adds no
/// rigidity. Its length as built is the distance between its nodes in the drawing. Like a real
/// cylinder, the gap between its joints' edges grows from its shortest by <see cref="Stroke"/> of
/// that, so it can at most double (#835); <see cref="Start"/> says where in that travel the drawn
/// length sits (#870). The brain sets where it goes and how much of <see cref="Strength"/> it uses
/// (<c>Piston</c> in NodeRunner.Mechanics). See docs/CREATURE_MODEL.md.
/// </summary>
public sealed record PistonDef
{
    /// <summary>A new Piston's <see cref="Strength"/>: 150 N at 100 world units per metre.</summary>
    public const double DefaultStrength = 15000;

    /// <summary>A new Piston's <see cref="Stroke"/>: the gap between its joints' edges grows by half its shortest.</summary>
    public const double DefaultStroke = 0.5;

    /// <summary>A new Piston's <see cref="Start"/>: drawn halfway between its shortest and longest length.</summary>
    public const double DefaultStart = 0.5;

    /// <summary>A new Piston's <see cref="MaxSpeed"/>: 2 m/s at 100 world units per metre.</summary>
    public const double DefaultMaxSpeed = 200;

    /// <summary>A new Piston's <see cref="RiseTime"/>: 0.2 s (#801).</summary>
    public const double DefaultRiseTime = 0.2;

    public PistonDef(
        int id,
        int nodeA,
        int nodeB,
        string? name = null,
        double strength = DefaultStrength,
        double stroke = DefaultStroke,
        double start = DefaultStart,
        double maxSpeed = DefaultMaxSpeed,
        double riseTime = DefaultRiseTime)
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

        if (!double.IsFinite(stroke) || stroke <= 0 || stroke > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(stroke), "Piston stroke must be a share of its shortest length above 0 and at most 1.");
        }

        if (!double.IsFinite(start) || start < 0 || start > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(start), "Piston start must be between 0 and 1.");
        }

        if (!double.IsFinite(maxSpeed) || maxSpeed <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxSpeed), "Piston max speed must be finite and positive.");
        }

        if (!double.IsFinite(riseTime) || riseTime <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(riseTime), "Piston rise time must be finite and positive.");
        }

        Id = id;
        NodeA = nodeA;
        NodeB = nodeB;
        Name = name;
        Strength = strength;
        Stroke = stroke;
        MaxSpeed = maxSpeed;
        RiseTime = riseTime;
        Start = start;
    }

    public int Id { get; }

    public int NodeA { get; }

    public int NodeB { get; }

    public string? Name { get; }

    /// <summary>The most force the Piston can push or pull with, in world units (mass × units/s²).</summary>
    public double Strength { get; }

    /// <summary>How much the gap between its joints' edges can grow, as a share of its shortest: 1 doubles it (#835).</summary>
    public double Stroke { get; }

    /// <summary>Where its drawn length sits in its travel: 0 is its shortest length, 1 its longest.</summary>
    public double Start { get; }

    /// <summary>The fastest it extends or retracts, in world units per second.</summary>
    public double MaxSpeed { get; }

    /// <summary>How long, in seconds, its force takes to build up to the strength the brain chose (#801).</summary>
    public double RiseTime { get; }

    public PistonDef WithName(string? name) => new(Id, NodeA, NodeB, name, Strength, Stroke, Start, MaxSpeed, RiseTime);

    public PistonDef WithSettings(double strength, double stroke, double start, double maxSpeed, double riseTime) =>
        new(Id, NodeA, NodeB, Name, strength, stroke, start, maxSpeed, riseTime);
}
