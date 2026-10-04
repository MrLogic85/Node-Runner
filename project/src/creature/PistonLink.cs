using Godot;
using NodeRunner.Domain;
using NodeRunner.Mechanics;

namespace NodeRunner.Creature;

/// <summary>
/// A Piston between two node bodies (#451): each physics tick it pushes them apart or pulls them
/// together along the line between them with <see cref="Mechanics.Piston.Step"/>. It only applies the
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

    public double LengthInput => Mechanics.Piston.LengthInput(Length, BuiltLength, Definition.Stroke);

    public double SpeedInput => Mechanics.Piston.SpeedInput(Speed, Definition.MaxSpeed);

    private PistonControl _control;

    // The reduced mass of its two nodes along its line: the lightest load it moves (see Mechanics.Piston.Step).
    private double PairMass => NodeA.Mass * NodeB.Mass / (NodeA.Mass + NodeB.Mass);

    private Vector2 Axis => (NodeB.GlobalPosition - NodeA.GlobalPosition).Normalized();

    /// <param name="position">The brain's position output, −1 fully in … +1 fully out.</param>
    /// <param name="strength">The brain's strength output, 0…1 of its Strength setting.</param>
    /// <param name="step">The physics step, in seconds.</param>
    public void Drive(double position, double strength, double step)
    {
        _control = Mechanics.Piston.Step(Definition, BuiltLength, Length, Speed, position, strength, PairMass, step, _control);
        var push = Axis * (float)_control.Force;
        NodeB.ApplyCentralForce(push);
        NodeA.ApplyCentralForce(-push);
    }

    /// <summary>Forgets the speed control's history, for a new try from the built pose.</summary>
    public void Reset() => _control = default;
}
