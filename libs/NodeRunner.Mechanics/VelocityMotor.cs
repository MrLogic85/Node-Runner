using NodeRunner.Domain;

namespace NodeRunner.Mechanics;

/// <summary>
/// The network conventions and torque of a <see cref="VelocityMotorDef"/>. Its angle is the
/// one between its sides: the target side's direction minus the fixed side's, a Wheel side
/// read from a mark on its tyre, both measured with <see cref="HingePartSideGeometry"/>. It has no end stops and can
/// turn round and round, so the network sees the angle as its sine and cosine, which never jump. The
/// network drives a speed and a strength share, and the motor chases that speed with
/// <see cref="MotorLoop"/>, so it can keep a speed against a load up to its strength.
/// </summary>
public static class VelocityMotor
{
    /// <summary>The angle inputs: the sine and cosine of the angle between the sides, so (1, 0) with Target a quarter turn counter-clockwise from Fixed.</summary>
    public static (double Sin, double Cos) AngleInputs(double angle) => (Math.Sin(angle), Math.Cos(angle));

    /// <summary>The speed a speed output asks for, in radians per second: −1…1 of Max speed, counter-clockwise positive.</summary>
    public static double WantedSpeed(double output, VelocityMotorDef motor)
    {
        ArgumentNullException.ThrowIfNull(motor);
        return Math.Clamp(output, -1, 1) * motor.MaxSpeed;
    }

    /// <summary>
    /// The motor's next state from its last (<see cref="MotorLoop.Next"/>), chasing the speed the
    /// network chose. <c>speedOutput</c> is the network's speed output (−1…1 of Max speed) and
    /// <c>strength</c> its share of Max strength (0…1).
    /// </summary>
    public static MotorState NextMotor(
        VelocityMotorDef motor,
        double speed,
        double speedOutput,
        double strength,
        MotorState previous,
        double step,
        double effectiveInertia)
    {
        ArgumentNullException.ThrowIfNull(motor);
        var chosen = OutputSignals.StrengthFromOutput(strength, motor.Strength);
        return MotorLoop.Next(WantedSpeed(speedOutput, motor), speed, chosen, motor.RiseTime, previous, step, effectiveInertia);
    }
}
