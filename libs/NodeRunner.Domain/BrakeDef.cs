using System.Text.Json.Serialization;

namespace NodeRunner.Domain;

/// <summary>
/// A Brake (#1039): a passive joint part on one node that resists its Target side turning against
/// its Fixed side, up to its <see cref="Strength"/>. It fills the joint's hinge-part slot and
/// takes its sides by the Servo's rules (<see cref="IHingePartDef"/>), but it is no joint motor:
/// it has no brain ports and draws no power, so it is not in <see cref="CreatureDef.JointMotors"/>.
/// </summary>
public sealed record BrakeDef : IHingePartDef
{
    /// <summary>A new Brake's <see cref="Strength"/>: 200 N·m in world torque units.</summary>
    public const double DefaultStrength = 2000000;

    /// <summary>The <see cref="Strength"/> of a <see cref="Locked"/> Brake.</summary>
    public const double LockedStrength = double.PositiveInfinity;

    public BrakeDef(
        int id,
        int nodeId,
        int? fixedLinkId = null,
        int? targetLinkId = null,
        string? name = null,
        double strength = DefaultStrength)
    {
        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Brake id must be positive.");
        }

        if (nodeId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nodeId), "Node id must be positive.");
        }

        if (fixedLinkId is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fixedLinkId), "Link id must be positive when present.");
        }

        if (targetLinkId is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(targetLinkId), "Link id must be positive when present.");
        }

        if (fixedLinkId is { } fixedId && targetLinkId == fixedId)
        {
            throw new ArgumentException("A brake's fixed and target links must be different.");
        }

        if (double.IsNaN(strength) || strength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(strength), "Brake strength must be positive, or infinite when locked.");
        }

        Id = id;
        NodeId = nodeId;
        FixedLinkId = fixedLinkId;
        TargetLinkId = targetLinkId;
        Name = name;
        Strength = strength;
    }

    public int Id { get; }

    public int NodeId { get; }

    /// <summary>The side held: a link at the joint or the joint's Wheel (#1118), or null once deleted.</summary>
    public int? FixedLinkId { get; }

    /// <summary>The side braked against it: a link at the joint or the joint's Wheel (#1118), or null once deleted.</summary>
    public int? TargetLinkId { get; }

    public string? Name { get; }

    /// <summary>
    /// The most torque the Brake resists with, in world torque units; infinite when it is
    /// <see cref="Locked"/>, saved as <c>"Infinity"</c>.
    /// </summary>
    [JsonNumberHandling(JsonNumberHandling.AllowNamedFloatingPointLiterals)]
    public double Strength { get; }

    /// <summary>Whether the two sides cannot turn against each other at all: an infinite <see cref="Strength"/>.</summary>
    [JsonIgnore]
    public bool Locked => double.IsPositiveInfinity(Strength);

    CreatureElementKind IHingePartDef.Kind => CreatureElementKind.Brake;

    public BrakeDef WithName(string? name) => new(Id, NodeId, FixedLinkId, TargetLinkId, name, Strength);

    public BrakeDef WithStrength(double strength) => new(Id, NodeId, FixedLinkId, TargetLinkId, Name, strength);

    /// <summary>The Brake with new sides. It keeps its id, like a Servo (<see cref="ServoDef.WithLinks"/>).</summary>
    public BrakeDef WithLinks(int? fixedLinkId, int? targetLinkId) =>
        new(Id, NodeId, fixedLinkId, targetLinkId, Name, Strength);
}
