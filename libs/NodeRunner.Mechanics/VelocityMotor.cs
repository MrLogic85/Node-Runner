using NodeRunner.Domain;

namespace NodeRunner.Mechanics;

/// <summary>
/// The brain conventions and torque of a <see cref="VelocityMotorDef"/> (#454). Its angle is a
/// Servo's: the target side's direction minus the fixed side's, a Wheel side read from a mark on its
/// tyre, so it measures them with <see cref="Servo"/>'s side helpers. It has no end stops and can
/// turn round and round, so the brain sees the angle as its sine and cosine, which never jump. The
/// brain drives a speed and a strength share, and the motor chases that speed with
/// <see cref="MotorLoop"/>, so it can keep a speed against a load up to its strength.
/// </summary>
public static class VelocityMotor
{
    /// <summary>The angle inputs: the sine and cosine of the turn from <paramref name="builtAngle"/>, (0, 1) as built.</summary>
    public static (double Sin, double Cos) AngleInputs(double angle, double builtAngle = 0)
    {
        var delta = angle - builtAngle;
        return (Math.Sin(delta), Math.Cos(delta));
    }

    /// <param name="speed">Angular speed in radians per second; counter-clockwise positive.</param>
    /// <param name="maxSpeed">The motor's maximum angular speed in radians per second.</param>
    public static double SpeedInput(double speed, double maxSpeed) => Servo.SpeedInput(speed, maxSpeed);

    /// <summary>The speed a speed output asks for, in radians per second: −1…1 of Max speed, counter-clockwise positive.</summary>
    public static double WantedSpeed(double output, VelocityMotorDef motor)
    {
        ArgumentNullException.ThrowIfNull(motor);
        return Math.Clamp(output, -1, 1) * motor.MaxSpeed;
    }

    /// <summary>
    /// The motor's next state from its last (<see cref="MotorLoop.Next"/>), chasing the speed the
    /// brain chose. <c>speedOutput</c> is the brain's speed output (−1…1 of Max speed) and
    /// <c>strength</c> its share of Max strength (0…1).
    /// </summary>
    public static ServoMotor NextMotor(
        VelocityMotorDef motor,
        double speed,
        double speedOutput,
        double strength,
        ServoMotor previous,
        double step,
        double effectiveInertia)
    {
        ArgumentNullException.ThrowIfNull(motor);
        var chosen = OutputSignals.StrengthFromOutput(strength, motor.Strength);
        return MotorLoop.Next(WantedSpeed(speedOutput, motor), speed, chosen, motor.RiseTime, previous, step, effectiveInertia);
    }
}
