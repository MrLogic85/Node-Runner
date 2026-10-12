using NodeRunner.Domain;

namespace NodeRunner.Mechanics;

/// <summary>
/// How every joint motor, a Servo (#452) or a Velocity motor (#454), measures and turns its two
/// sides. A link side's direction runs from the motor's joint to the link's other joint, and it
/// turns by an equal and opposite force couple; a Wheel side (#1118) is a one-sided link that
/// points along its own turn, as a mark on its tyre would, and turns by a body torque.
/// </summary>
public static class MotorSideGeometry
{
    private const double _minimumInertia = 1e-6;
    public const double MinimumLeverArm = NodeDef.PlainJointRadius;

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

    /// <summary>The live direction from a motor's joint to a link's other joint, in radians.</summary>
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

    /// <summary>The equal-and-opposite forces that apply <paramref name="torque"/> around a motor's joint through a link.</summary>
    public static MotorCouple CoupleForTorque(Vector2D joint, Vector2D far, double torque)
    {
        var radiusX = far.X - joint.X;
        var radiusY = far.Y - joint.Y;
        var length = Math.Sqrt((radiusX * radiusX) + (radiusY * radiusY));
        if (length <= double.Epsilon || torque == 0)
        {
            return new MotorCouple(new Vector2D(0, 0), new Vector2D(0, 0));
        }

        var leverArm = Math.Max(length, MinimumLeverArm);
        var scale = torque / (length * leverArm);
        // Positive torque is counter-clockwise on screen, so a link pointing right pushes upward.
        var forceOnFar = new Vector2D(radiusY * scale, -radiusX * scale);
        return new MotorCouple(new Vector2D(-forceOnFar.X, -forceOnFar.Y), forceOnFar);
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

    /// <summary>Unwraps an angle reading around ±π into a continuous angle.</summary>
    public static double UnwrapAngle(double previousContinuous, double previousWrapped, double currentWrapped)
    {
        var delta = Math.Atan2(Math.Sin(currentWrapped - previousWrapped), Math.Cos(currentWrapped - previousWrapped));
        return previousContinuous + delta;
    }
}

/// <summary>Equal-and-opposite forces that apply a joint motor's torque through a link.</summary>
public readonly record struct MotorCouple(Vector2D ForceOnJoint, Vector2D ForceOnFar);
