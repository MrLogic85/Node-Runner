using NodeRunner.Domain;

namespace NodeRunner.Mechanics;

/// <summary>
/// How every hinge part (<see cref="IHingePartDef"/>) measures
/// and turns its two sides. A link side's direction runs from the hinge part's joint to the link's other joint, and it
/// turns by an equal and opposite force couple; a Wheel side is a one-sided link that
/// points along its own turn, as a mark on its tyre would, and turns by a body torque.
/// </summary>
public static class HingePartSideGeometry
{
    private const double _minimumInertia = 1e-6;
    public const double MinimumLeverArm = NodeDef.PlainJointRadius;

    /// <summary>
    /// The live direction of a Wheel side, a one-sided link that points along its own turn,
    /// from its body's rotation. Creature coordinates are y-down, so a body's rotation is
    /// clockwise-positive on screen, and model angles are counter-clockwise-positive.
    /// </summary>
    public static double WheelAngle(double bodyRotation) => -bodyRotation;

    /// <summary>A Wheel side's angular speed from its body's, in the model's counter-clockwise-positive sense.</summary>
    public static double WheelAngularSpeed(double bodyAngularVelocity) => -bodyAngularVelocity;

    /// <summary>The body torque that turns a Wheel side by a model, counter-clockwise-positive, <paramref name="torque"/>.</summary>
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

    /// <summary>
    /// The unit direction, in creature coordinates, of the groove that welds a hinge part's sides at
    /// <paramref name="overshoot"/> radians short of their angle: the line from the joint
    /// through where the held point, a point on the held side now at <paramref name="far"/>, will be
    /// once the target side has turned back by it. The groove runs on the other side, so when the held
    /// side is the fixed one it turns the other way.
    /// </summary>
    public static Vector2D WeldGrooveDirection(Vector2D joint, Vector2D far, double overshoot, bool heldIsTarget)
    {
        var angle = LinkAngle(joint, far) - (heldIsTarget ? overshoot : -overshoot);
        return new Vector2D(Math.Cos(angle), -Math.Sin(angle));
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

    /// <summary>
    /// How hard two links that meet at one joint are to turn apart: the inertia the torque
    /// between them works against, with the joint's mass shared by both rather than counted for
    /// each, as two <see cref="LinkInertia"/> in series would; that reads a straight joint twice as
    /// heavy. It treats the three joints as free masses and anything else on them only makes them
    /// harder to turn, so it never reads heavier than the joint is.
    /// </summary>
    public static double SharedJointInertia(
        Vector2D joint,
        Vector2D fixedFar,
        Vector2D targetFar,
        double jointMass,
        double fixedFarMass,
        double targetFarMass)
    {
        var onTarget = CoupleForTorque(joint, targetFar, 1);
        var onFixed = CoupleForTorque(joint, fixedFar, -1);
        var jointSide = Math.Max(jointMass, double.Epsilon);
        var jointPush = new Vector2D(
            (onTarget.ForceOnJoint.X + onFixed.ForceOnJoint.X) / jointSide,
            (onTarget.ForceOnJoint.Y + onFixed.ForceOnJoint.Y) / jointSide);
        var targetPush = Scaled(onTarget.ForceOnFar, 1 / Math.Max(targetFarMass, double.Epsilon));
        var fixedPush = Scaled(onFixed.ForceOnFar, 1 / Math.Max(fixedFarMass, double.Epsilon));

        // Turning speed is linear in the end speeds, so the same reading turns pushes into the turn's speed-up.
        var turn = LinkAngularSpeed(joint, targetFar, jointPush, targetPush) - LinkAngularSpeed(joint, fixedFar, jointPush, fixedPush);
        return turn > 0 && double.IsFinite(turn) ? Math.Max(1 / turn, _minimumInertia) : _minimumInertia;
    }

    private static Vector2D Scaled(Vector2D value, double factor) => new(value.X * factor, value.Y * factor);

    /// <summary>Unwraps an angle reading around ±π into a continuous angle.</summary>
    public static double UnwrapAngle(double previousContinuous, double previousWrapped, double currentWrapped)
    {
        var delta = Math.Atan2(Math.Sin(currentWrapped - previousWrapped), Math.Cos(currentWrapped - previousWrapped));
        return previousContinuous + delta;
    }
}

/// <summary>Equal-and-opposite forces that apply a joint motor's torque through a link.</summary>
public readonly record struct MotorCouple(Vector2D ForceOnJoint, Vector2D ForceOnFar);
