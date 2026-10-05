using NodeRunner.Domain;

namespace NodeRunner.Mechanics;

/// <summary>
/// The brain conventions and torque of a <see cref="ServoDef"/> (#452). Its angle is the direction
/// from the Servo joint to the target link's other joint minus the direction to the fixed link's
/// other joint, relative to the built angle. The brain sees −1 at the lower end, 0 as built and
/// +1 at the upper end, and drives an angle target plus a strength share, using the same build-up
/// formula as <see cref="Piston"/>.
/// </summary>
public static class Servo
{
    private const double _approachRate = 10;
    private const double _buildSharpness = 3;
    private const double _speedSharpness = 10;

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

    /// <summary>The live direction from a Servo joint to a link's other joint, in radians.</summary>
    public static double LinkAngle(Vector2D joint, Vector2D far)
    {
        var dx = far.X - joint.X;
        var dy = far.Y - joint.Y;
        if (dx == 0 && dy == 0)
        {
            return 0;
        }

        return Math.Atan2(dy, dx);
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
        return ((radiusX * relativeVelocityY) - (radiusY * relativeVelocityX)) / lengthSquared;
    }

    /// <summary>
    /// The equal-and-opposite forces that apply <paramref name="torque"/> around a Servo joint
    /// through a link. Positive torque turns the far joint toward the link's
    /// perpendicular direction.
    /// </summary>
    public static ServoCouple CoupleForTorque(Vector2D joint, Vector2D far, double torque)
    {
        var radiusX = far.X - joint.X;
        var radiusY = far.Y - joint.Y;
        var length = Math.Sqrt((radiusX * radiusX) + (radiusY * radiusY));
        if (length <= double.Epsilon || torque == 0)
        {
            return new ServoCouple(new Vector2D(0, 0), new Vector2D(0, 0));
        }

        var scale = torque / (length * length);
        var forceOnFar = new Vector2D(-radiusY * scale, radiusX * scale);
        return new ServoCouple(new Vector2D(-forceOnFar.X, -forceOnFar.Y), forceOnFar);
    }

    /// <summary>A damped restoring torque outside the Servo's range ends.</summary>
    public static double EndStopTorque(ServoDef servo, double builtAngle, double angle, double speed)
    {
        ArgumentNullException.ThrowIfNull(servo);
        var stiffness = 4 * servo.Strength / servo.Range;
        var damping = servo.Strength / servo.MaxSpeed;
        return EndStopTorque(angle, speed, LowerAngle(servo, builtAngle), UpperAngle(servo, builtAngle), stiffness, damping);
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
    /// The torque applied to the target link this step (the fixed link gets the opposite torque),
    /// from the torque used last step.
    /// </summary>
    public static double NextTorque(
        ServoDef servo,
        double builtAngle,
        double angle,
        double speed,
        double position,
        double strength,
        double torque,
        double step)
    {
        ArgumentNullException.ThrowIfNull(servo);
        var targetTorque = OutputSignals.StrengthFromOutput(strength, servo.Strength);
        var angleLeft = TargetAngle(position, servo, builtAngle) - angle;
        var wantedSpeed = Math.Clamp(angleLeft * _approachRate, -servo.MaxSpeed, servo.MaxSpeed);
        var speedLacking = wantedSpeed - speed;
        var raised = torque + step * targetTorque / servo.RiseTime * Math.Tanh(_buildSharpness * speedLacking / servo.MaxSpeed);
        var gate = Sigmoid(_speedSharpness * Math.Sign(raised) * speedLacking / servo.MaxSpeed);
        return Math.Clamp(raised * gate, -targetTorque, targetTorque);
    }

    private static double Sigmoid(double x) => 1 / (1 + Math.Exp(-x));
}

/// <summary>Equal-and-opposite forces for a Servo torque expressed as a force couple.</summary>
public readonly record struct ServoCouple(Vector2D ForceOnJoint, Vector2D ForceOnFar);
