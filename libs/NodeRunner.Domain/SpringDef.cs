namespace NodeRunner.Domain;

/// <summary>
/// A Spring (#453): a passive link between two nodes that pulls them back toward its rest length,
/// their distance in the drawing unless <see cref="CoilLength"/> presses it against a stop, and damps
/// how fast that distance changes. It limits movement without holding it rigid, has no brain ports and draws no power. Like a Piston it travels between a shortest and
/// a longest length, <see cref="Stroke"/> apart, and <see cref="CoilLength"/> moves its rest length
/// evenly across that travel and half its drawn gap, between its joints' edges, past each stop, where it starts pressed
/// against that stop (#835). See
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

    /// <summary>A new Spring's <see cref="Stroke"/>: the gap between its joints' edges can be squeezed to half its longest.</summary>
    public const double DefaultStroke = 1;

    /// <summary>
    /// A new Spring's <see cref="CoilLength"/>: at its <see cref="DefaultStroke"/>, its travel is half its
    /// drawn gap between its joints' edges, so its rest length is on its longest stop two thirds of the way along, drawn
    /// there like a car's suspension hanging free.
    /// </summary>
    public const double DefaultCoilLength = 2.0 / 3;

    /// <summary>The lowest <see cref="CoilLength"/>: its rest length half its drawn gap short of its shortest stop.</summary>
    public const double MinCoilLength = 0;

    /// <summary>The highest <see cref="CoilLength"/>: its rest length half its drawn gap past its longest stop.</summary>
    public const double MaxCoilLength = 1;

    public SpringDef(
        int id,
        int nodeA,
        int nodeB,
        string? name = null,
        double stiffness = DefaultStiffness,
        double damping = DefaultDamping,
        double stroke = DefaultStroke,
        double coilLength = DefaultCoilLength)
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
            throw new ArgumentOutOfRangeException(nameof(stroke), "Spring stroke must be above 0 and at most 1.");
        }

        if (!double.IsFinite(coilLength) || coilLength < MinCoilLength || coilLength > MaxCoilLength)
        {
            throw new ArgumentOutOfRangeException(nameof(coilLength), $"Spring coil length must be between {MinCoilLength} and {MaxCoilLength}.");
        }

        Id = id;
        NodeA = nodeA;
        NodeB = nodeB;
        Name = name;
        Stiffness = stiffness;
        Damping = damping;
        Stroke = stroke;
        CoilLength = coilLength;
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

    /// <summary>
    /// Its travel, as a Piston's of this Stroke drawn where its rest length is, kept within its stops
    /// (#974): drawn gap × s / (1 + at·s), at 0 on its shortest stop and 1 on its longest, the gap being
    /// between its joints' edges (#835). Above 0 and at most 1.
    /// </summary>
    public double Stroke { get; }

    /// <summary>
    /// Where its rest length sits, evenly from half its drawn gap, between its joints' edges, short of
    /// its shortest stop at 0 to half that gap past its longest at 1. Between its stops that is the drawn length,
    /// and the stops sit round it as a Piston's round its Start position, so its travel is longer the
    /// nearer its shortest stop (#974). Past a stop they stay, the drawn length on that stop, and the
    /// Spring starts pressed against it: past its longest it pushes its nodes apart, past its
    /// shortest it pulls them in. A new Stroke keeps the Coil length, so its stops may move past or
    /// off its rest length.
    /// </summary>
    public double CoilLength { get; }

    public SpringDef WithName(string? name) => new(Id, NodeA, NodeB, name, Stiffness, Damping, Stroke, CoilLength);

    public SpringDef WithSettings(double stiffness, double damping, double stroke, double coilLength) =>
        new(Id, NodeA, NodeB, Name, stiffness, damping, stroke, coilLength);
}
