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

    /// <summary>A pose to reach, −1…1 with 0 the built pose (<see cref="PortSignals.PositionFromTarget"/>).</summary>
    Position,

    /// <summary>How much of the part's Strength setting to use this tick, 0…1.</summary>
    Strength,
}
