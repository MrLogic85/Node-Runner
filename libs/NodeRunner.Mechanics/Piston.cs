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
/// The force builds up over its <see cref="PistonDef.RiseTime"/> towards the target length and dies
/// away once it moves at the speed it wants, using at most the chosen share of its Strength. It comes
/// from the Piston's own settings and outputs only, not from the load it moves (#801). Its end stops
/// are not powered: they are a hard limit in the sim (#701), so the force never needs more than the
/// brain chose to stay inside its stroke. Pure: the caller keeps the force from one step to the next.
/// </summary>
public static class Piston
{
    // How fast, per second of distance left, the piston wants to close on its target: it slows
    // as it arrives instead of overshooting at full speed.
    private const double _approachRate = 10;

    // How sharply the speed it lacks steers the force's build-up: a tenth of Max speed short it
    // builds at tanh 0.3 ≈ 29% of its full rate, a third short at tanh 1 ≈ 76%.
    private const double _buildSharpness = 3;

    // How sharply the force dies once the piston has the speed it wants: a tenth of Max speed
    // short it keeps 73% a step, a tenth over it keeps 27%.
    private const double _speedSharpness = 10;

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
    /// The force pushing its two nodes apart this step (negative pulls them together), in world
    /// units, from the <paramref name="force"/> it pushed with last step, its outputs, its current
    /// <paramref name="length"/> and <paramref name="speed"/>.
    /// </summary>
    /// <remarks>
    /// The owner's formula (#801). With the target force <c>tF</c> the strength output's share of
    /// its Strength and the speed it lacks <c>dV</c> = wanted speed − speed:
    /// <c>F(N+1) = clamp((F(N) + dT · tF / riseTime · tanh(3 · dV / MaxSpeed)) · σ(10 · d · dV / MaxSpeed), ±tF)</c>,
    /// where <c>d</c> is the sign of the raised force. The wanted speed is the distance left times
    /// the approach rate, capped at MaxSpeed, so it slows as it arrives. The force builds up to full
    /// within its rise time while the piston lacks speed, and the gate makes it die away once the
    /// piston moves as fast as it wants in the direction it pushes. It is built from the speed it
    /// lacks, not the distance left, so it brakes before the target rather than swinging past it.
    /// It knows nothing of the mass it moves: a force that builds up rather than jumps does not
    /// flip every step, but a light load at the shortest rise time and a low MaxSpeed can still
    /// swing around its target. The user's settings, the brain and fitness (#546) handle that.
    /// </remarks>
    /// <param name="piston">The Piston and its settings.</param>
    /// <param name="builtLength">Its length as built, centre to centre.</param>
    /// <param name="length">Its length now.</param>
    /// <param name="speed">How fast its length grows, in world units per second.</param>
    /// <param name="position">The brain's position output, −1 fully in … +1 fully out.</param>
    /// <param name="strength">The brain's strength output, 0…1 of its Strength setting.</param>
    /// <param name="force">The force it pushed with last step; 0 as built.</param>
    /// <param name="step">The step length, in seconds.</param>
    public static double NextForce(
        PistonDef piston,
        double builtLength,
        double length,
        double speed,
        double position,
        double strength,
        double force,
        double step)
    {
        ArgumentNullException.ThrowIfNull(piston);
        var targetForce = OutputSignals.StrengthFromOutput(strength, piston.Strength);
        var distanceLeft = TargetLength(position, builtLength, piston.Stroke) - length;
        var wantedSpeed = Math.Clamp(distanceLeft * _approachRate, -piston.MaxSpeed, piston.MaxSpeed);
        var speedLacking = wantedSpeed - speed;
        var raised = force + step * targetForce / piston.RiseTime * Math.Tanh(_buildSharpness * speedLacking / piston.MaxSpeed);
        var gate = Sigmoid(_speedSharpness * Math.Sign(raised) * speedLacking / piston.MaxSpeed);
        return Math.Clamp(raised * gate, -targetForce, targetForce);
    }

    private static double Sigmoid(double x) => 1 / (1 + Math.Exp(-x));
}
