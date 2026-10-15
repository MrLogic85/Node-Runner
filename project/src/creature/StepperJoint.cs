using NodeRunner.Domain;

namespace NodeRunner.Creature;

/// <summary>
/// A Stepper (#1037) between the two <see cref="HingePartSides"/> at its joint: it holds the Target at
/// a notch against the Fixed side and, at the brain's frequency, steps it to the next notch the way the
/// frequency's sign says (<see cref="Mechanics.Stepper"/>). It holds every tick, with or without a brain:
/// while locked it welds the sides at the notch, and lets the weld go as it steps on. Where no weld
/// fits, when both sides slide, it holds by torque instead.
/// </summary>
public sealed class StepperJoint
{
    private readonly HingeWeld? _weld;
    private Mechanics.StepperState _state;

    /// <param name="definition">The Stepper's settings.</param>
    /// <param name="sides">The two sides it steps.</param>
    /// <param name="weld">Welds the sides, or null where no weld fits.</param>
    public StepperJoint(StepperDef definition, HingePartSides sides, HingeWeld? weld)
    {
        Definition = definition;
        Sides = sides;
        _weld = weld;
    }

    public StepperDef Definition { get; }

    public HingePartSides Sides { get; }

    /// <summary>Its state as of the last tick, which its visual shows (#1067).</summary>
    public Mechanics.StepperState State => _state;

    /// <summary>
    /// Whether its Fixed or Target beam has torn at either end (#962): it then lets go of its weld and
    /// drives nothing, and its brain inputs keep the values they last had (Creature.Tears).
    /// </summary>
    public bool IsPassive { get; private set; }

    public void GoPassive()
    {
        IsPassive = true;
        _weld?.Release();
    }

    /// <summary>The angle between the sides as its sine and cosine, as a Velocity motor's.</summary>
    public (double Sin, double Cos) AngleInputs => Mechanics.Stepper.AngleInputs(Sides.ContinuousAngle);

    /// <summary>Its tick as of the last physics step: 0 as it steps, rising to 1 just before its next (#1263).</summary>
    public double TickInput => Mechanics.Stepper.TickInput(_state);

    /// <param name="frequencyOutput">The brain's Frequency output, −1…1 of its Max frequency, counter-clockwise positive.</param>
    /// <param name="step">The physics step, in seconds.</param>
    public void Drive(double frequencyOutput, double step)
    {
        if (IsPassive)
        {
            return;
        }

        var angle = Sides.ContinuousAngle - Sides.BuiltAngle;
        _state = Mechanics.Stepper.Next(
            Definition,
            angle,
            Sides.Speed,
            frequencyOutput,
            _state,
            step,
            Sides.BrakeInertia,
            welded: _weld is not null);
        if (_weld is not null && _state.Locked != _weld.Welded)
        {
            if (_state.Locked)
            {
                // It catches the joint up to a tick past its notch, so the weld turns it back onto it (#1272).
                _weld.Weld(angle - Mechanics.Stepper.NotchAngle(_state.Notch, Definition));
            }
            else
            {
                _weld.Release();
            }
        }

        Sides.ApplyTorque(_state.Torque);
    }

    /// <summary>Holds its notch with no brain to step it.</summary>
    /// <param name="step">The physics step, in seconds.</param>
    public void Hold(double step) => Drive(0, step);
}
