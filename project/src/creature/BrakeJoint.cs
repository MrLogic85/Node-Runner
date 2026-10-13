using NodeRunner.Domain;

namespace NodeRunner.Creature;

/// <summary>
/// A Brake (#1040) between the two <see cref="HingePartSides"/> at its joint: friction that slows and
/// holds the Target's turning against the Fixed side, up to its Strength. It has no brain ports, so
/// it holds every tick, with or without a brain.
/// </summary>
public sealed class BrakeJoint
{
    private Mechanics.BrakeState? _state;

    public BrakeJoint(BrakeDef definition, HingePartSides sides)
    {
        Definition = definition;
        Sides = sides;
    }

    public BrakeDef Definition { get; }

    public HingePartSides Sides { get; }

    /// <param name="step">The physics step, in seconds.</param>
    public void Hold(double step)
    {
        var state = Mechanics.Brake.Next(Sides.Speed, Definition.Strength, _state, step, Sides.BrakeInertia);
        Sides.ApplyTorque(state.Torque);
        _state = state;
    }
}
