namespace NodeRunner.Domain;

/// <summary>
/// The output conventions: which activation each <see cref="PortSignal"/> uses and how a
/// newly added output starts. A new output has no incoming weight and an almost passive bias, so a
/// part added to a trained network starts close to doing nothing; training explores it from there
/// (docs/TRAINING_LOOP.md → "New ports are explored").
/// <list type="bullet">
/// <item>Velocity, position, frequency and remember use <c>tanh</c>: −1…1, with 0 meaning stand still, a
/// Servo's built pose, a Stepper that does not step or a Memory cell's target of 0.</item>
/// <item>Length uses <c>sigmoid</c>: 0…1 of a Piston's travel, like its length input. A new
/// length output starts at bias 0, the middle of its travel.</item>
/// <item>Strength uses <c>sigmoid</c>: 0…1 of the part's Strength setting, which stays the maximum.
/// A new strength output starts at bias −4, about 2% force, with no dead zone below it.</item>
/// <item>Write uses <c>sigmoid</c>: 0…1 of the way a Memory cell moves to its target. A new write output
/// starts at bias −4 like a strength (docs/CREATURE_MODEL.md → "Memory cell").</item>
/// </list>
/// How an output's value becomes a physical demand is the sim's (<c>OutputSignals</c> in
/// NodeRunner.Mechanics). Stateless. See docs/CREATURE_MODEL.md.
/// </summary>
public static class PortSignals
{
    public const double PassiveStrengthBias = -4;

    public static NeuronActivation Activation(PortSignal signal) => signal switch
    {
        PortSignal.Reading => NeuronActivation.Identity,
        PortSignal.Velocity or PortSignal.Position or PortSignal.Frequency or PortSignal.Remember => NeuronActivation.Tanh,
        PortSignal.Length or PortSignal.Strength or PortSignal.Write => NeuronActivation.Sigmoid,
        _ => throw new ArgumentOutOfRangeException(nameof(signal), signal, null),
    };

    /// <summary>
    /// Whether an output of <paramref name="signal"/> drives a powered part, which moves the body. A Memory cell's
    /// outputs only feed the network back, so a creature with nothing else has nothing to train.
    /// </summary>
    public static bool IsPowered(PortSignal signal) => signal switch
    {
        PortSignal.Velocity or PortSignal.Position or PortSignal.Length or PortSignal.Strength or PortSignal.Frequency => true,
        PortSignal.Reading or PortSignal.Remember or PortSignal.Write => false,
        _ => throw new ArgumentOutOfRangeException(nameof(signal), signal, null),
    };

    /// <summary>Whether any of <paramref name="outputs"/> drives a powered part (<see cref="IsPowered"/>): whether there is anything to train.</summary>
    public static bool AnyPowered(IEnumerable<NetworkPort> outputs) => outputs.Any(port => IsPowered(port.Signal));

    /// <summary>The bias a newly added port starts with; its incoming weights start at 0.</summary>
    public static double PassiveBias(PortSignal signal) => signal switch
    {
        PortSignal.Strength or PortSignal.Write => PassiveStrengthBias,
        PortSignal.Reading or PortSignal.Velocity or PortSignal.Position or PortSignal.Length or PortSignal.Frequency or PortSignal.Remember => 0,
        _ => throw new ArgumentOutOfRangeException(nameof(signal), signal, null),
    };
}
