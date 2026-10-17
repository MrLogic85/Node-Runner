using NodeRunner.Domain;

namespace NodeRunner.Creature;

/// <summary>
/// A Brake between the two <see cref="HingePartSides"/> at its joint: friction that slows and
/// holds the Target's turning against the Fixed side, up to its Strength. It has no network ports, so
/// it holds every tick, with or without a network. A Locked one held by a weld (Creature.Welder) holds
/// through that joint instead.
/// </summary>
public sealed class BrakeJoint
{
    private readonly HingeWeld? _weld;
    private Mechanics.BrakeState? _state;

    public BrakeJoint(BrakeDef definition, HingePartSides sides, HingeWeld? weld)
    {
        Definition = definition;
        Sides = sides;
        _weld = weld;
    }

    public BrakeDef Definition { get; }

    public HingePartSides Sides { get; }

    /// <summary>Whether its Fixed or Target beam has torn at either end: it then holds nothing.</summary>
    public bool IsPassive { get; private set; }

    /// <summary>Stops holding, letting go of its weld at once, as a tear lets go of a pin (Creature.Tears).</summary>
    public void GoPassive()
    {
        IsPassive = true;
        _weld?.Release();
    }

    /// <param name="step">The physics step, in seconds.</param>
    public void Hold(double step)
    {
        if (IsPassive || _weld is not null)
        {
            return;
        }

        var state = Mechanics.Brake.Next(Sides.Speed, Definition.Strength, _state, step, Sides.BrakeInertia);
        Sides.ApplyTorque(state.Torque);
        _state = state;
    }
}
