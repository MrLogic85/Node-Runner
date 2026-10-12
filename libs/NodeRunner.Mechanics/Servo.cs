using NodeRunner.Domain;

namespace NodeRunner.Mechanics;

/// <summary>
/// The brain conventions and torque of a <see cref="ServoDef"/> (#452). Its angle is the direction
/// from the Servo joint to the target link's other joint minus the direction to the fixed link's
/// other joint, relative to the built angle; a Wheel side (#1118) stands in a mark on its tyre for
/// the other joint. The brain sees −1 at the lower end, 0 as built and
/// +1 at the upper end, and drives an angle target plus a strength share. The motor chases a
/// wanted speed towards that angle with <see cref="MotorLoop"/>, so it can hold a load up to its
/// strength. A Velocity motor (#454) reuses its side angles, speeds, couple and inertia.
/// </summary>
public static class Servo
{
    private const double _approachRate = 10;
    private const double _safeDampingFraction = 0.5;
    private const double _safeStiffnessFraction = 0.25;
    private const double _minimumInertia = 1e-6;
    public const double MinimumLeverArm = NodeDef.PlainJointRadius;

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

    /// <param name="speed">Angular speed in radians per second; counter-clockwise positive.</param>
    /// <param name="maxSpeed">The Servo's maximum angular speed in radians per second.</param>
    public static double SpeedInput(double speed, double maxSpeed) => Math.Tanh(speed / maxSpeed);

    /// <summary>The angle an angle output asks for.</summary>
    public static double TargetAngle(double position, ServoDef servo, double builtAngle = 0) =>
        OutputSignals.PositionFromTarget(position, LowerAngle(servo, builtAngle), builtAngle, UpperAngle(servo, builtAngle));

    /// <summary>
    /// The live direction of a Wheel side (#1118), a one-sided link that points along its own turn,
    /// from its body's rotation. Creature coordinates are y-down, so a body's rotation is
    /// clockwise-positive on screen, and model angles are counter-clockwise-positive.
    /// </summary>
    public static double WheelAngle(double bodyRotation) => -bodyRotation;

    /// <summary>A Wheel side's angular speed (#1118) from its body's, in the model's counter-clockwise-positive sense.</summary>
    public static double WheelAngularSpeed(double bodyAngularVelocity) => -bodyAngularVelocity;

    /// <summary>The body torque that turns a Wheel side (#1118) by a model, counter-clockwise-positive, <paramref name="torque"/>.</summary>
    public static double WheelBodyTorque(double torque) => -torque;

    /// <summary>The live direction from a Servo joint to a link's other joint, in radians.</summary>
    public static double LinkAngle(Vector2D joint, Vector2D far)
    {
        var dx = far.X - joint.X;
        var dy = far.Y - joint.Y;
        if (dx == 0 && dy == 0)
        {
            return 0;
        }

        // Model angles are counter-clockwise-positive on screen; creature coordinates are y-down.
        return Math.Atan2(-dy, dx);
    }

    /// <summary>How fast the direction from <paramref name="joint"/> to <paramref name="far"/> turns, in radians per second.</summary>
    public static double LinkAngularSpeed(Vector2D joint, Vector2D far, Vector2D jointVelocity, Vector2D farVelocity)
    {
        var radiusX = far.X - joint.X;
        var radiusY = far.Y - joint.Y;
        var lengthSquared = (radiusX * radiusX) + (radiusY * radiusY);
        if (lengthSquared <= double.Epsilon)
        {
            return 0;
        }

        var relativeVelocityX = farVelocity.X - jointVelocity.X;
        var relativeVelocityY = farVelocity.Y - jointVelocity.Y;
        // The usual 2D cross-product derivative is y-up. Negate it for y-down model coordinates.
        return -((radiusX * relativeVelocityY) - (radiusY * relativeVelocityX)) / lengthSquared;
    }

    /// <summary>The equal-and-opposite forces that apply <paramref name="torque"/> around a Servo joint through a link.</summary>
    public static ServoCouple CoupleForTorque(Vector2D joint, Vector2D far, double torque)
    {
        var radiusX = far.X - joint.X;
        var radiusY = far.Y - joint.Y;
        var length = Math.Sqrt((radiusX * radiusX) + (radiusY * radiusY));
        if (length <= double.Epsilon || torque == 0)
        {
            return new ServoCouple(new Vector2D(0, 0), new Vector2D(0, 0));
        }

        var leverArm = Math.Max(length, MinimumLeverArm);
        var scale = torque / (length * leverArm);
        // Positive torque is counter-clockwise on screen, so a link pointing right pushes upward.
        var forceOnFar = new Vector2D(radiusY * scale, -radiusX * scale);
        return new ServoCouple(new Vector2D(-forceOnFar.X, -forceOnFar.Y), forceOnFar);
    }

    /// <summary>
    /// How hard a link is to turn with <see cref="CoupleForTorque"/>: the two end masses' reduced mass
    /// times the link length times the clamped lever arm, so a vanishing link gives a vanishing inertia.
    /// </summary>
    public static double LinkInertia(Vector2D joint, Vector2D far, double jointMass, double farMass)
    {
        var dx = far.X - joint.X;
        var dy = far.Y - joint.Y;
        var length = Math.Sqrt((dx * dx) + (dy * dy));
        var jointSide = Math.Max(jointMass, double.Epsilon);
        var farSide = Math.Max(farMass, double.Epsilon);
        var reducedMass = jointSide * farSide / (jointSide + farSide);
        return Math.Max(reducedMass * length * Math.Max(length, MinimumLeverArm), _minimumInertia);
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
    /// The motor's next state from its last (<see cref="MotorLoop.Next"/>): it wants to close the gap
    /// to the target angle quickly, but no faster than its Max speed.
    /// <c>position</c> is the brain's angle output (−1 lower end … +1 upper end) and <c>strength</c>
    /// its share of Max strength (0…1).
    /// </summary>
    public static ServoMotor NextMotor(
        ServoDef servo,
        double builtAngle,
        double angle,
        double speed,
        double position,
        double strength,
        ServoMotor previous,
        double step,
        double effectiveInertia)
    {
        ArgumentNullException.ThrowIfNull(servo);
        var chosen = OutputSignals.StrengthFromOutput(strength, servo.Strength);
        var wantedSpeed = Math.Clamp((TargetAngle(position, servo, builtAngle) - angle) * _approachRate, -servo.MaxSpeed, servo.MaxSpeed);
        return MotorLoop.Next(wantedSpeed, speed, chosen, servo.RiseTime, previous, step, effectiveInertia);
    }

    /// <summary>Unwraps an angle reading around ±π into a continuous angle.</summary>
    public static double UnwrapAngle(double previousContinuous, double previousWrapped, double currentWrapped)
    {
        var delta = Math.Atan2(Math.Sin(currentWrapped - previousWrapped), Math.Cos(currentWrapped - previousWrapped));
        return previousContinuous + delta;
    }

}

/// <summary>A joint motor, a Servo or a Velocity motor, between steps: the torque it applied and the holding part it has gathered (<see cref="MotorLoop"/>).</summary>
public readonly record struct ServoMotor(double Torque, double Holding);

/// <summary>Equal-and-opposite forces for a Servo torque expressed as a force couple.</summary>
public readonly record struct ServoCouple(Vector2D ForceOnJoint, Vector2D ForceOnFar);

/// <summary>Stiffness and damping for a Servo soft end stop.</summary>
public readonly record struct ServoEndStopGains(double Stiffness, double Damping);
