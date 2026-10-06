using NodeRunner.Domain;

namespace NodeRunner.Mechanics;

/// <summary>
/// The brain conventions and torque of a <see cref="ServoDef"/> (#452). Its angle is the direction
/// from the Servo joint to the target link's other joint minus the direction to the fixed link's
/// other joint, relative to the built angle. The brain sees −1 at the lower end, 0 as built and
/// +1 at the upper end, and drives an angle target plus a strength share. The motor chases the
/// wanted speed with a push part and a holding part, so it can hold a load up to its strength.
/// </summary>
public static class Servo
{
    private const double _approachRate = 10;
    private const double _safeDampingFraction = 0.5;
    private const double _safeStiffnessFraction = 0.25;
    // Both gains scale with the estimated inertia. A real inertia far above the estimate (a planted
    // leg) slows the loop, and a holding part that gathers too fast then makes it swing: these keep
    // it settling from about 3x too light down to about 30x too heavy.
    private const double _pushGain = 0.6;
    private const double _holdingGain = 0.004;
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
        RequirePositive(effectiveInertia, nameof(effectiveInertia), "Effective inertia");
        RequirePositive(step, nameof(step), "Step");

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
    /// The motor's next state from its last. The push part closes most of the gap to the wanted speed
    /// in one step (capped by <paramref name="effectiveInertia"/> so an explicit step cannot overshoot).
    /// The holding part slowly gathers what a steady load needs; it stops gathering while the motor
    /// already gives all it can, so it does not wind up. Torque builds at most the chosen strength per
    /// Rise time and drops at once.
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
        RequirePositive(effectiveInertia, nameof(effectiveInertia), "Effective inertia");
        RequirePositive(step, nameof(step), "Step");

        var chosen = OutputSignals.StrengthFromOutput(strength, servo.Strength);
        var wantedSpeed = Math.Clamp((TargetAngle(position, servo, builtAngle) - angle) * _approachRate, -servo.MaxSpeed, servo.MaxSpeed);
        var speedLacking = wantedSpeed - speed;
        var torquePerSpeed = effectiveInertia / step;
        var push = _pushGain * torquePerSpeed * speedLacking;
        var holding = Math.Clamp(previous.Holding + (_holdingGain * torquePerSpeed * speedLacking), -chosen, chosen);
        var torque = Rise(previous.Torque, Math.Clamp(holding + push, -chosen, chosen), step * chosen / servo.RiseTime);
        var shortfall = holding + push - torque;
        var heldBack = shortfall != 0 && Math.Sign(shortfall) == Math.Sign(speedLacking);
        return new ServoMotor(torque, heldBack ? Math.Clamp(previous.Holding, -chosen, chosen) : holding);
    }

    /// <summary>Unwraps an angle reading around ±π into a continuous angle.</summary>
    public static double UnwrapAngle(double previousContinuous, double previousWrapped, double currentWrapped)
    {
        var delta = Math.Atan2(Math.Sin(currentWrapped - previousWrapped), Math.Cos(currentWrapped - previousWrapped));
        return previousContinuous + delta;
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

    private static void RequirePositive(double value, string name, string label)
    {
        if (!double.IsFinite(value) || value <= 0)
        {
            throw new ArgumentOutOfRangeException(name, $"{label} must be finite and positive.");
        }
    }
}

/// <summary>A Servo motor between steps: the torque it applied and the holding part it has gathered.</summary>
public readonly record struct ServoMotor(double Torque, double Holding);

/// <summary>Equal-and-opposite forces for a Servo torque expressed as a force couple.</summary>
public readonly record struct ServoCouple(Vector2D ForceOnJoint, Vector2D ForceOnFar);

/// <summary>Stiffness and damping for a Servo soft end stop.</summary>
public readonly record struct ServoEndStopGains(double Stiffness, double Damping);
