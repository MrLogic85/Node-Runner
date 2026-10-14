namespace NodeRunner.Domain;

/// <summary>
/// What a <see cref="BrainPort"/> carries (#535). An input is a reading; an output says which
/// physical signal it drives, which fixes its activation and how a new port starts
/// (<see cref="PortSignals"/>).
/// </summary>
public enum PortSignal
{
    /// <summary>An input: a sensor or motor reading.</summary>
    Reading,

    /// <summary>A speed to chase, −1…1 of the maximum, as the Velocity motor's target (#454).</summary>
    Velocity,

    /// <summary>
    /// A pose to reach, −1…1 with 0 the built pose, as a Servo's target angle
    /// (<c>OutputSignals.PositionFromTarget</c> in NodeRunner.Mechanics).
    /// </summary>
    Position,

    /// <summary>
    /// A length to reach, 0…1 of the part's travel: for a Piston 0 is its shortest and 1 its longest
    /// length, like its length input (<c>Piston.TargetLength</c> in NodeRunner.Mechanics, #1129).
    /// </summary>
    Length,

    /// <summary>How much of the part's Strength setting to use this tick, 0…1.</summary>
    Strength,

    /// <summary>How often to step, 0…1 of the part's Max frequency, as a Stepper's (#1036).</summary>
    Frequency,

    /// <summary>
    /// How hard to push and which way, −1…1 of the part's Max strength, counter-clockwise positive, as a
    /// Stepper's (#1036). At 0 it pushes with no force but still lets go.
    /// </summary>
    Power,
}
