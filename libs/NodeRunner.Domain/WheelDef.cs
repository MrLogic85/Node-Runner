namespace NodeRunner.Domain;

/// <summary>
/// A Wheel (#129): a passive joint part on one node, a disc that turns freely on it and rolls on the
/// ground. It has no brain ports and draws no power; a motor or brake on its joint drives it later
/// (#1068, #1070). Its <see cref="Radius"/> sets how big it is drawn and collides, and what it weighs
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

    /// <summary>A new Wheel's <see cref="Grip"/>: 80 %.</summary>
    public const double DefaultGrip = 0.8;

    public WheelDef(int id, int nodeId, string? name = null, double radius = DefaultRadius, double grip = DefaultGrip)
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

        if (!double.IsFinite(grip) || grip < 0 || grip > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(grip), "Wheel grip must be a share from 0 to 1.");
        }

        Id = id;
        NodeId = nodeId;
        Name = name;
        Radius = radius;
        Grip = grip;
    }

    public int Id { get; }

    public int NodeId { get; }

    public string? Name { get; }

    /// <summary>Its drawn and collision radius, in world units: its joint's radius while it is there.</summary>
    public double Radius { get; }

    /// <summary>How well its tyre holds the ground, a share from 0 (slides) to 1: its friction.</summary>
    public double Grip { get; }

    public WheelDef WithName(string? name) => new(Id, NodeId, name, Radius, Grip);

    public WheelDef WithSettings(double radius, double grip) => new(Id, NodeId, Name, radius, grip);
}
