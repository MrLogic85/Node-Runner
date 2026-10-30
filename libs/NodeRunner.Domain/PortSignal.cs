namespace NodeRunner.Domain;

/// <summary>
/// What a <see cref="NetworkPort"/> carries. An input is a reading; an output says which
/// physical signal it drives, which fixes its activation and how a new port starts
/// (<see cref="PortSignals"/>).
/// </summary>
public enum PortSignal
{
    /// <summary>An input: a sensor or motor reading.</summary>
    Reading,

    /// <summary>A speed to chase, −1…1 of the maximum, as the Velocity motor's target.</summary>
    Velocity,

    /// <summary>
    /// A pose to reach, −1…1 with 0 the built pose, as a Servo's target angle
    /// (<c>OutputSignals.PositionFromTarget</c> in NodeRunner.Mechanics).
    /// </summary>
    Position,

    /// <summary>
    /// A length to reach, 0…1 of the part's travel: for a Piston 0 is its shortest and 1 its longest
    /// length, like its length input (<c>Piston.TargetLength</c> in NodeRunner.Mechanics).
    /// </summary>
    Length,

    /// <summary>How much of the part's Strength setting to use this tick, 0…1.</summary>
    Strength,

    /// <summary>
    /// How often to step and which way, −1…1 of the part's Max frequency, counter-clockwise positive, as a
    /// Stepper's. At 0 it does not step.
    /// </summary>
    Frequency,

    /// <summary>What a Memory cell is to remember, −1…1: the value its <see cref="Write"/> moves it towards.</summary>
    Remember,

    /// <summary>
    /// How much of the way a Memory cell moves to its <see cref="Remember"/> target this tick, 0…1
    /// (<c>NodeRunner.Mechanics.MemoryCell</c>). Not a <see cref="Strength"/>: it is a share of a distance, not of a force.
    /// </summary>
    Write,
}
