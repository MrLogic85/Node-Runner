using NodeRunner.Domain;

namespace NodeRunner.Creature;

/// <summary>
/// A Stepper (#1037) between the two <see cref="HingePartSides"/> at its joint: it holds the Target at
/// a notch against the Fixed side and, at the brain's frequency, steps it to the next notch in the
/// brain's direction (<see cref="Mechanics.Stepper"/>). It holds every tick, with or without a brain.
/// </summary>
public sealed class StepperJoint
{
    private Mechanics.StepperState _state;

    public StepperJoint(StepperDef definition, HingePartSides sides)
    {
        Definition = definition;
        Sides = sides;
    }

    public StepperDef Definition { get; }

    public HingePartSides Sides { get; }

    /// <summary>The angle between the sides as its sine and cosine, as a Velocity motor's.</summary>
    public (double Sin, double Cos) AngleInputs => Mechanics.Stepper.AngleInputs(Sides.ContinuousAngle);

    /// <summary>The running frequency, 0…1 of its Max frequency, as of the last tick.</summary>
    public double FrequencyInput => Mechanics.Stepper.FrequencyInput(_state, Definition);

    /// <param name="frequencyOutput">The brain's Frequency output, 0…1 of its Max frequency.</param>
    /// <param name="powerOutput">The brain's Power output, −1…1 of its Max strength, counter-clockwise positive.</param>
    /// <param name="step">The physics step, in seconds.</param>
    public void Drive(double frequencyOutput, double powerOutput, double step)
    {
        _state = Mechanics.Stepper.Next(
            Definition,
            Sides.ContinuousAngle - Sides.BuiltAngle,
            Sides.Speed,
            frequencyOutput,
            powerOutput,
            _state,
            step,
            Sides.BrakeInertia);
        Sides.ApplyTorque(_state.Torque);
    }

    /// <summary>Holds its notch with no brain to step it.</summary>
    /// <param name="step">The physics step, in seconds.</param>
    public void Hold(double step) => Drive(0, 0, step);
}
