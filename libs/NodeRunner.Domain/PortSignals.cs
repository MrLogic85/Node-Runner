namespace NodeRunner.Domain;

/// <summary>
/// The output conventions (#535): which activation each <see cref="PortSignal"/> uses and how a
/// newly added output starts. A new output has no incoming weight and an almost passive bias, so a
/// part added to a trained brain starts close to doing nothing and mutation can still teach it.
/// <list type="bullet">
/// <item>Velocity and position use <c>tanh</c>: −1…1, with 0 meaning stand still, a Servo's built
/// pose, or the middle of a Piston's travel (#870).</item>
/// <item>Strength uses <c>sigmoid</c>: 0…1 of the part's Strength setting, which stays the maximum.
/// A new strength output starts at bias −4, about 2% force, with no dead zone below it.</item>
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
        PortSignal.Velocity or PortSignal.Position => NeuronActivation.Tanh,
        PortSignal.Strength => NeuronActivation.Sigmoid,
        _ => throw new ArgumentOutOfRangeException(nameof(signal), signal, null),
    };

    /// <summary>The bias a newly added port starts with; its incoming weights start at 0.</summary>
    public static double PassiveBias(PortSignal signal) => signal switch
    {
        PortSignal.Strength => PassiveStrengthBias,
        PortSignal.Reading or PortSignal.Velocity or PortSignal.Position => 0,
        _ => throw new ArgumentOutOfRangeException(nameof(signal), signal, null),
    };
}
