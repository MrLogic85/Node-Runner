namespace NodeRunner.Domain;

/// <summary>
/// The output conventions (#535): which activation each <see cref="PortSignal"/> uses and how a
/// newly added output starts. A new output has no incoming weight and an almost passive bias, so a
/// part added to a trained brain starts close to doing nothing and mutation can still teach it.
/// <list type="bullet">
/// <item>Velocity and position use <c>tanh</c>: −1…1, with 0 meaning stand still or the built pose.</item>
/// <item>Strength uses <c>sigmoid</c>: 0…1 of the part's Strength setting, which stays the maximum.
/// A new strength output starts at bias −4, about 2% force, with no dead zone below it.</item>
/// </list>
/// Stateless, like <see cref="JointMotor"/>. See docs/CREATURE_MODEL.md.
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

    /// <summary>
    /// The pose a position <paramref name="target"/> asks for, mapped piecewise so 0 is always the
    /// built pose even when it is off-centre: −1…0 spans <paramref name="min"/>…<paramref name="built"/>
    /// and 0…1 spans <paramref name="built"/>…<paramref name="max"/>. For a Piston, −1 is fully in and
    /// +1 fully out.
    /// </summary>
    public static double PositionFromTarget(double target, double min, double built, double max)
    {
        if (!(min <= built && built <= max))
        {
            throw new ArgumentOutOfRangeException(nameof(built), built, "The built pose must lie within min…max.");
        }

        var clamped = Math.Clamp(target, -1, 1);
        return clamped < 0
            ? built + (clamped * (built - min))
            : built + (clamped * (max - built));
    }

    /// <summary>The force a strength output asks for: its 0…1 share of the part's Strength setting.</summary>
    public static double StrengthFromOutput(double output, double strengthSetting) =>
        Math.Clamp(output, 0, 1) * strengthSetting;
}
