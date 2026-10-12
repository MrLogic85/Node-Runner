namespace NodeRunner.Domain;

/// <summary>
/// A Wheel (#129): a passive joint part on one node, a disc that turns freely on it and rolls on the
/// ground. It has no brain ports and draws no power; a Servo or Velocity motor on its joint can drive
/// it (#1118, #454), a brake later (#1070). Its <see cref="Radius"/> sets how big it is drawn and collides, and what it weighs
/// (<c>NodeRunner.Mechanics.Wheel.Mass</c>). See docs/CREATURE_MODEL.md → "Wheel".
/// </summary>
public sealed record WheelDef
{
    /// <summary>The smallest <see cref="Radius"/>: 0.4 m at 100 world units per metre.</summary>
    public const double MinRadius = 40;

    /// <summary>The largest <see cref="Radius"/>: 1 m.</summary>
    public const double MaxRadius = 100;

    /// <summary>A new Wheel's <see cref="Radius"/>: the smallest.</summary>
    public const double DefaultRadius = MinRadius;

    public WheelDef(int id, int nodeId, string? name = null, double radius = DefaultRadius)
    {
        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Wheel id must be positive.");
        }

        if (nodeId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nodeId), "Node id must be positive.");
        }

        if (!double.IsFinite(radius) || radius < MinRadius || radius > MaxRadius)
        {
            throw new ArgumentOutOfRangeException(nameof(radius), $"Wheel radius must be between {MinRadius} and {MaxRadius}.");
        }

        Id = id;
        NodeId = nodeId;
        Name = name;
        Radius = radius;
    }

    public int Id { get; }

    public int NodeId { get; }

    public string? Name { get; }

    /// <summary>Its drawn and collision radius, in world units: its joint's radius while it is there.</summary>
    public double Radius { get; }

    public WheelDef WithName(string? name) => new(Id, NodeId, name, Radius);

    public WheelDef WithSettings(double radius) => new(Id, NodeId, Name, radius);
}
