using NodeRunner.Domain;

namespace NodeRunner.Mechanics;

/// <summary>
/// The brain conventions and torque of a <see cref="ServoDef"/> (#452). Its angle is the direction
/// from the Servo joint to the target link's other joint minus the direction to the fixed link's
/// other joint, relative to the built angle; a Wheel side (#1118) stands in a mark on its tyre for
/// the other joint. The brain sees −1 at the lower end, 0 as built and
/// +1 at the upper end, and drives an angle target plus a strength share. The motor chases a
/// wanted speed towards that angle with <see cref="MotorLoop"/>, so it can hold a load up to its
/// strength. It measures and turns its sides with <see cref="MotorSideGeometry"/>, as a Velocity
/// motor (#454) does.
/// </summary>
public static class Servo
{
    private const double _approachRate = 10;
    private const double _safeDampingFraction = 0.5;
    private const double _safeStiffnessFraction = 0.25;

    public static double LowerAngle(ServoDef servo, double builtAngle = 0) =>
        builtAngle - (servo.Start * servo.Range);

    public static double UpperAngle(ServoDef servo, double builtAngle = 0) =>
        builtAngle + ((1 - servo.Start) * servo.Range);

    /// <summary>The angle input, −1 lower end … 0 built … +1 upper end.</summary>
    public static double AngleInput(double angle, ServoDef servo, double builtAngle = 0)
    {
        ArgumentNullException.ThrowIfNull(servo);
        var delta = angle - builtAngle;
        if (delta == 0)
        {
            return 0;
        }

        if (delta < 0)
        {
            var lowerSpan = servo.Start * servo.Range;
            return lowerSpan <= 0 ? -1 : Math.Clamp(delta / lowerSpan, -1, 0);
        }

        var upperSpan = (1 - servo.Start) * servo.Range;
        return upperSpan <= 0 ? 1 : Math.Clamp(delta / upperSpan, 0, 1);
    }

    /// <summary>The angle an angle output asks for.</summary>
    public static double TargetAngle(double position, ServoDef servo, double builtAngle = 0) =>
        OutputSignals.PositionFromTarget(position, LowerAngle(servo, builtAngle), builtAngle, UpperAngle(servo, builtAngle));

    /// <summary>
    /// The speed an angle output asks for at <paramref name="angle"/>, in radians per second: it closes
    /// the gap to <see cref="TargetAngle"/> quickly, but no faster than the Servo's Max speed.
    /// </summary>
    public static double WantedSpeed(double position, ServoDef servo, double builtAngle, double angle)
    {
        ArgumentNullException.ThrowIfNull(servo);
        return Math.Clamp((TargetAngle(position, servo, builtAngle) - angle) * _approachRate, -servo.MaxSpeed, servo.MaxSpeed);
    }

    /// <summary>A stable damped restoring torque outside a Servo's range ends, capped for explicit integration.</summary>
    public static double EndStopTorque(ServoDef servo, double builtAngle, double angle, double speed, double effectiveInertia, double step)
    {
        ArgumentNullException.ThrowIfNull(servo);
        var gains = StableEndStopGains(servo, effectiveInertia, step);
        return EndStopTorque(angle, speed, LowerAngle(servo, builtAngle), UpperAngle(servo, builtAngle), gains.Stiffness, gains.Damping);
    }

    /// <summary>The Servo's desired end-stop gains capped to conservative explicit-step stability limits.</summary>
    public static ServoEndStopGains StableEndStopGains(ServoDef servo, double effectiveInertia, double step)
    {
        ArgumentNullException.ThrowIfNull(servo);
        MotorLoop.RequirePositive(effectiveInertia, nameof(effectiveInertia), "Effective inertia");
        MotorLoop.RequirePositive(step, nameof(step), "Step");

        var desiredStiffness = 4 * servo.Strength / servo.Range;
        var desiredDamping = servo.Strength / servo.MaxSpeed;
        var safeStiffness = _safeStiffnessFraction * effectiveInertia / (step * step);
        var safeDamping = _safeDampingFraction * effectiveInertia / step;
        return new ServoEndStopGains(Math.Min(desiredStiffness, safeStiffness), Math.Min(desiredDamping, safeDamping));
    }

    /// <summary>A damped restoring torque outside <paramref name="lower"/>..<paramref name="upper"/>, zero inside.</summary>
    public static double EndStopTorque(double angle, double speed, double lower, double upper, double stiffness, double damping)
    {
        if (angle < lower)
        {
            return ((lower - angle) * stiffness) - (speed * damping);
        }

        if (angle > upper)
        {
            return ((upper - angle) * stiffness) - (speed * damping);
        }

        return 0;
    }

    /// <summary>
    /// The motor's next state from its last (<see cref="MotorLoop.Next"/>), chasing its
    /// <see cref="WantedSpeed"/>.
    /// <c>position</c> is the brain's angle output (−1 lower end … +1 upper end) and <c>strength</c>
    /// its share of Max strength (0…1).
    /// </summary>
    public static MotorState NextMotor(
        ServoDef servo,
        double builtAngle,
        double angle,
        double speed,
        double position,
        double strength,
        MotorState previous,
        double step,
        double effectiveInertia)
    {
        ArgumentNullException.ThrowIfNull(servo);
        var chosen = OutputSignals.StrengthFromOutput(strength, servo.Strength);
        return MotorLoop.Next(WantedSpeed(position, servo, builtAngle, angle), speed, chosen, servo.RiseTime, previous, step, effectiveInertia);
    }
}

/// <summary>Stiffness and damping for a Servo soft end stop.</summary>
public readonly record struct ServoEndStopGains(double Stiffness, double Damping);
