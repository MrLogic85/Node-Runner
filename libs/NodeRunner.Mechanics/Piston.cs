using NodeRunner.Domain;

namespace NodeRunner.Mechanics;

/// <summary>
/// The brain conventions and force of a <see cref="PistonDef"/> (#451). Its ports
/// (<see cref="BrainPorts.PistonInputs"/>, <see cref="BrainPorts.PistonOutputs"/>) belong to the Piston part:
/// <list type="bullet">
/// <item>Length input: how far it is from its built length, −1…1 over its stroke; 0 is as built.</item>
/// <item>Speed input: <c>tanh(v / maxSpeed)</c>, extending positive.</item>
/// <item>Position output (<c>tanh</c>): the length to reach, −1 fully in, 0 built, +1 fully out.</item>
/// <item>Strength output (<c>sigmoid</c>): the share of its Strength setting it may use this tick.</item>
/// </list>
/// The force chases the target length at up to <see cref="PistonDef.MaxSpeed"/>, using at most the
/// chosen share of its Strength. Its end stops are not powered: they are a hard limit in the
/// sim (#701), so the force never needs more than the brain chose to stay inside its stroke.
/// Stateless and shared by the sim and tests.
/// </summary>
public static class Piston
{
    // How fast, per second of distance left, the piston wants to close on its target: it slows
    // as it arrives instead of overshooting at full speed.
    private const double _approachRate = 10;

    // The speed control's gains, as shares of the gain that would close a speed error in one step
    // on the pair alone. Together they place its poles at about 0.72 and 0.28: no sign flipping,
    // about 20% speed overshoot, and stable even when the load is many times the pair (#451 review).
    private const double _proportional = 0.8;

    private const double _integral = 0.2;

    public static double LengthInput(double length, double builtLength, double stroke) =>
        (length - builtLength) / (builtLength * stroke);

    /// <param name="speed">How fast its length grows, in world units per second; negative while it retracts.</param>
    /// <param name="maxSpeed">Its <see cref="PistonDef.MaxSpeed"/>.</param>
    public static double SpeedInput(double speed, double maxSpeed) => Math.Tanh(speed / maxSpeed);

    public static double ShortestLength(double builtLength, double stroke) => builtLength * (1 - stroke);

    public static double LongestLength(double builtLength, double stroke) => builtLength * (1 + stroke);

    /// <summary>The length a position output asks for (<see cref="OutputSignals.PositionFromTarget"/>).</summary>
    public static double TargetLength(double position, double builtLength, double stroke) =>
        OutputSignals.PositionFromTarget(position, ShortestLength(builtLength, stroke), builtLength, LongestLength(builtLength, stroke));

    /// <summary>
    /// One physics step of the Piston's speed control: the force pushing its two nodes apart this
    /// step (negative pulls them together), in world units, from its outputs, its current
    /// <paramref name="length"/> and <paramref name="speed"/>, and the <paramref name="previous"/>
    /// step's control.
    /// </summary>
    /// <remarks>
    /// A PI controller on its speed, in velocity form, with gains from <paramref name="pairMass"/>,
    /// the reduced mass of its two nodes (mA·mB / (mA + mB)) along its line. That is the lightest
    /// load it can ever move: beams and the ground only add to it, so the control stays stable
    /// on the lightest limb tip, where a gain from Strength alone would flip its force every step.
    /// The integral part lets it hold a load, such as the body's weight, at its target instead of
    /// sagging. Its force is capped by its Strength share, so it never winds up.
    /// </remarks>
    /// <param name="piston">The Piston and its settings.</param>
    /// <param name="builtLength">Its length as built, centre to centre.</param>
    /// <param name="length">Its length now.</param>
    /// <param name="speed">How fast its length grows, in world units per second.</param>
    /// <param name="position">The brain's position output, −1 fully in … +1 fully out.</param>
    /// <param name="strength">The brain's strength output, 0…1 of its Strength setting.</param>
    /// <param name="pairMass">The reduced mass of its two node bodies.</param>
    /// <param name="step">The physics step, in seconds.</param>
    /// <param name="previous">The last step's control; <c>default</c> at the start of a try.</param>
    public static PistonControl Step(
        PistonDef piston,
        double builtLength,
        double length,
        double speed,
        double position,
        double strength,
        double pairMass,
        double step,
        PistonControl previous)
    {
        ArgumentNullException.ThrowIfNull(piston);
        var target = TargetLength(position, builtLength, piston.Stroke);
        var wantedSpeed = Math.Clamp((target - length) * _approachRate, -piston.MaxSpeed, piston.MaxSpeed);
        var speedError = wantedSpeed - speed;
        var gain = pairMass / step;
        var force = previous.Force + (gain * ((_proportional * (speedError - previous.SpeedError)) + (_integral * speedError)));
        var limit = OutputSignals.StrengthFromOutput(strength, piston.Strength);
        return new PistonControl(Math.Clamp(force, -limit, limit), speedError);
    }
}

/// <summary>What a Piston's speed control carries from one physics step to the next (<see cref="Piston.Step"/>).</summary>
/// <param name="Force">The force it pushed with, in world units; negative pulled.</param>
/// <param name="SpeedError">How much slower than wanted it was extending, in world units per second.</param>
public readonly record struct PistonControl(double Force, double SpeedError);
