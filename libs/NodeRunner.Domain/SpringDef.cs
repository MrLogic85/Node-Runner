namespace NodeRunner.Domain;

/// <summary>
/// A Spring (#453): a passive link between two nodes that pulls them back toward their distance
/// in the drawing and damps how fast that distance changes. It limits movement without holding
/// it rigid, has no brain ports and draws no power. See <see cref="Spring"/> and
/// docs/CREATURE_MODEL.md.
/// </summary>
public sealed record SpringDef
{
    /// <summary>A new Spring's <see cref="Stiffness"/>: 400 N/m at 100 world units per metre.</summary>
    public const double DefaultStiffness = 400;

    /// <summary>A new Spring's <see cref="Damping"/>: 30% of the damping that stops it without a bounce.</summary>
    public const double DefaultDamping = 0.3;

    public SpringDef(
        int id,
        int nodeA,
        int nodeB,
        string? name = null,
        double stiffness = DefaultStiffness,
        double damping = DefaultDamping)
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

        if (!double.IsFinite(damping) || damping < 0 || damping > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(damping), "Spring damping must be a share between 0 and 1.");
        }

        Id = id;
        NodeA = nodeA;
        NodeB = nodeB;
        Name = name;
        Stiffness = stiffness;
        Damping = damping;
    }

    public int Id { get; }

    public int NodeA { get; }

    public int NodeB { get; }

    public string? Name { get; }

    /// <summary>How hard it pulls back per unit stretched or squeezed, in world force units per world unit (N/m).</summary>
    public double Stiffness { get; }

    /// <summary>
    /// How much it damps, as a share of the damping that stops its two nodes without a bounce:
    /// 0 bounces on, 1 settles without bouncing (<see cref="Spring.DampingCoefficient"/>).
    /// </summary>
    public double Damping { get; }

    public SpringDef WithName(string? name) => new(Id, NodeA, NodeB, name, Stiffness, Damping);

    public SpringDef WithSettings(double stiffness, double damping) =>
        new(Id, NodeA, NodeB, Name, stiffness, damping);
}
