using Godot;
using NodeRunner.Domain;

namespace NodeRunner.Creature;

/// <summary>
/// A Piston between two node bodies (#451): each physics tick it pushes them apart or pulls them
/// together along the line between them with <see cref="Mechanics.Piston.NextForce"/>, which
/// builds up over its rise time, so it keeps the force it pushed with last step. It only applies the
/// force: inside its stroke nothing holds its length, so it adds no rigidity. Its end stops (#701),
/// a hidden cylinder body and groove, are built by <c>Creature.CreateEndStops</c>. Its brain conventions
/// are <see cref="Mechanics.Piston"/>'s — see docs/CREATURE_MODEL.md.
/// </summary>
public sealed class PistonLink
{
    public PistonLink(PistonDef definition, RigidBody2D nodeA, RigidBody2D nodeB)
    {
        Definition = definition;
        NodeA = nodeA;
        NodeB = nodeB;
        BuiltLength = Math.Max(nodeA.Position.DistanceTo(nodeB.Position), 1f);
    }

    // The force it pushed with last step. Creature.ResetPose builds a fresh link each trial (#798),
    // so a run starts from rest.
    private double _force;

    public PistonDef Definition { get; }

    public RigidBody2D NodeA { get; }

    public RigidBody2D NodeB { get; }

    /// <summary>The distance between its nodes as built.</summary>
    public double BuiltLength { get; }

    public double Length => NodeA.GlobalPosition.DistanceTo(NodeB.GlobalPosition);

    /// <summary>How fast its length grows, in world units per second; negative while it retracts.</summary>
    public double Speed
    {
        get
        {
            var axis = Axis;
            return (NodeB.LinearVelocity - NodeA.LinearVelocity).Dot(axis);
        }
    }

    public double LengthInput => Mechanics.Piston.LengthInput(Definition, BuiltLength, Length);

    public double SpeedInput => Mechanics.Piston.SpeedInput(Speed, Definition.MaxSpeed);

    private Vector2 Axis => (NodeB.GlobalPosition - NodeA.GlobalPosition).Normalized();

    /// <param name="position">The brain's position output, −1 fully in … +1 fully out.</param>
    /// <param name="strength">The brain's strength output, 0…1 of its Strength setting.</param>
    /// <param name="step">The physics step, in seconds.</param>
    public void Drive(double position, double strength, double step)
    {
        _force = Mechanics.Piston.NextForce(Definition, BuiltLength, Length, Speed, position, strength, _force, step);
        var push = Axis * (float)_force;
        NodeB.ApplyCentralForce(push);
        NodeA.ApplyCentralForce(-push);
    }
}
