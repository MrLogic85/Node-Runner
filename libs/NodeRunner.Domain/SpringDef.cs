namespace NodeRunner.Domain;

/// <summary>
/// A Spring (#453): a passive link between two nodes that pulls them back toward its rest length,
/// their distance in the drawing unless <see cref="Preload"/> presses it against a stop, and damps
/// how fast that distance changes. It limits movement without holding it rigid, has no brain ports and draws no power. Like a Piston it travels between a shortest and
/// a longest length, <see cref="Stroke"/> apart, and <see cref="Preload"/> says where in that travel
/// the drawn length sits; outside 0…1 it starts pressed against a stop (#835). See
/// <c>Spring</c> in NodeRunner.Mechanics and docs/CREATURE_MODEL.md.
/// </summary>
public sealed record SpringDef
{
    /// <summary>A new Spring's <see cref="Stiffness"/>: 400 N/m at 100 world units per metre.</summary>
    public const double DefaultStiffness = 400;

    /// <summary>The softest <see cref="Stiffness"/> Build offers.</summary>
    public const double SoftestStiffness = 50;

    /// <summary>The stiffest <see cref="Stiffness"/> Build offers: firm, yet stable on the lightest nodes (#787).</summary>
    public const double StiffestStiffness = 2000;

    /// <summary>A new Spring's <see cref="Damping"/>: 10 N·s/m.</summary>
    public const double DefaultDamping = 10;

    /// <summary>A new Spring's <see cref="Stroke"/>: it can be squeezed to half its longest length.</summary>
    public const double DefaultStroke = 1;

    /// <summary>A new Spring's <see cref="Preload"/>: drawn at its longest, like a car's suspension hanging free.</summary>
    public const double DefaultPreload = 1;

    /// <summary>The lowest <see cref="Preload"/>.</summary>
    public const double MinPreload = -0.5;

    /// <summary>The highest <see cref="Preload"/>.</summary>
    public const double MaxPreload = 2;

    public SpringDef(
        int id,
        int nodeA,
        int nodeB,
        string? name = null,
        double stiffness = DefaultStiffness,
        double damping = DefaultDamping,
        double stroke = DefaultStroke,
        double preload = DefaultPreload)
    {
        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Spring id must be positive.");
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
            throw new ArgumentException("A spring must connect two different nodes.");
        }

        if (!double.IsFinite(stiffness) || stiffness <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(stiffness), "Spring stiffness must be finite and positive.");
        }

        if (!double.IsFinite(damping) || damping < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(damping), "Spring damping must be finite and not negative.");
        }

        if (!double.IsFinite(stroke) || stroke <= 0 || stroke > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(stroke), "Spring stroke must be a share of its shortest length above 0 and at most 1.");
        }

        if (!double.IsFinite(preload) || preload < MinPreload || preload > MaxPreload)
        {
            throw new ArgumentOutOfRangeException(nameof(preload), $"Spring preload must be between {MinPreload} and {MaxPreload}.");
        }

        Id = id;
        NodeA = nodeA;
        NodeB = nodeB;
        Name = name;
        Stiffness = stiffness;
        Damping = damping;
        Stroke = stroke;
        Preload = preload;
    }

    public int Id { get; }

    public int NodeA { get; }

    public int NodeB { get; }

    public string? Name { get; }

    /// <summary>How hard it pulls back per unit stretched or squeezed, in world force units per world unit (N/m).</summary>
    public double Stiffness { get; }

    /// <summary>
    /// How hard it brakes the speed between its nodes, in world force units per world unit per second
    /// (N·s/m). It is a plain coefficient, not tuned to the mass it moves (#801): the same Damping
    /// bounces more on heavy nodes than on light ones.
    /// </summary>
    public double Damping { get; }

    /// <summary>How much it can grow from its shortest length, as a share of it: at most 1, so it can at most double.</summary>
    public double Stroke { get; }

    /// <summary>
    /// Where its rest length sits in its travel: 0 at its shortest, 1 at its longest. Inside 0…1 that
    /// is the drawn length. Outside it the travel stays where 0 or 1 puts it, the drawn length on
    /// that stop, and only the rest length moves past it, so the Spring starts pressed against it:
    /// above 1 it pushes its nodes apart against its longest, below 0 it pulls them in.
    /// </summary>
    public double Preload { get; }

    public SpringDef WithName(string? name) => new(Id, NodeA, NodeB, name, Stiffness, Damping, Stroke, Preload);

    public SpringDef WithSettings(double stiffness, double damping, double stroke, double preload) =>
        new(Id, NodeA, NodeB, Name, stiffness, damping, stroke, preload);
}
