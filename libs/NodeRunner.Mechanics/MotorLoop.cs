namespace NodeRunner.Mechanics;

/// <summary>
/// The speed loop every joint motor, a Servo (#452) or a Velocity motor (#454), drives its torque
/// with. Each asks for a wanted speed its own way and hands the rest to <see cref="Next"/>, and
/// both give the brain the speed they turn at the same way (<see cref="SpeedInput"/>).
/// </summary>
public static class MotorLoop
{
    // Both gains scale with the estimated inertia. A real inertia far above the estimate (a planted
    // leg) slows the loop, and a holding part that gathers too fast then makes it swing: these keep
    // it settling from about 3x too light down to about 30x too heavy.
    private const double _pushGain = 0.6;
    private const double _holdingGain = 0.004;

    /// <summary>The speed input, −1…1 and 0 at rest, saturating softly past Max speed.</summary>
    /// <param name="speed">Angular speed in radians per second; counter-clockwise positive.</param>
    /// <param name="maxSpeed">The motor's maximum angular speed in radians per second.</param>
    public static double SpeedInput(double speed, double maxSpeed) => Math.Tanh(speed / maxSpeed);

    /// <summary>
    /// The motor's next state from its last, chasing <paramref name="wantedSpeed"/> from
    /// <paramref name="speed"/>, both in radians per second. The push part closes most of the gap in
    /// one step (capped by <paramref name="effectiveInertia"/> so an explicit step cannot overshoot).
    /// The holding part slowly gathers what a steady load needs; it stops gathering while the motor
    /// already gives all it can, so it does not wind up. Torque never passes
    /// <paramref name="chosenTorque"/>, builds at most that much per <paramref name="riseTime"/> and drops at once.
    /// </summary>
    public static MotorState Next(
        double wantedSpeed,
        double speed,
        double chosenTorque,
        double riseTime,
        MotorState previous,
        double step,
        double effectiveInertia)
    {
        RequirePositive(effectiveInertia, nameof(effectiveInertia), "Effective inertia");
        RequirePositive(step, nameof(step), "Step");

        var speedLacking = wantedSpeed - speed;
        var torquePerSpeed = effectiveInertia / step;
        var push = _pushGain * torquePerSpeed * speedLacking;
        var holding = Math.Clamp(previous.Holding + (_holdingGain * torquePerSpeed * speedLacking), -chosenTorque, chosenTorque);
        var torque = Rise(previous.Torque, Math.Clamp(holding + push, -chosenTorque, chosenTorque), step * chosenTorque / riseTime);
        var shortfall = holding + push - torque;
        var heldBack = shortfall != 0 && Math.Sign(shortfall) == Math.Sign(speedLacking);
        return new MotorState(torque, heldBack ? Math.Clamp(previous.Holding, -chosenTorque, chosenTorque) : holding);
    }

    internal static void RequirePositive(double value, string name, string label)
    {
        if (!double.IsFinite(value) || value <= 0)
        {
            throw new ArgumentOutOfRangeException(name, $"{label} must be finite and positive.");
        }
    }

    private static double Rise(double torque, double wanted, double maxRise)
    {
        if (wanted == 0 || (Math.Sign(wanted) == Math.Sign(torque) && Math.Abs(wanted) <= Math.Abs(torque)))
        {
            return wanted;
        }

        var from = Math.Sign(wanted) == Math.Sign(torque) ? torque : 0;
        return from + Math.Clamp(wanted - from, -maxRise, maxRise);
    }
}

/// <summary>A joint motor between steps: the torque it applied and the holding part it has gathered (<see cref="MotorLoop"/>).</summary>
public readonly record struct MotorState(double Torque, double Holding);
