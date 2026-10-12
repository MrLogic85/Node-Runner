using Godot;
using NodeRunner.Domain;

namespace NodeRunner.Creature;

/// <summary>
/// A joint motor's two sides (<see cref="IJointMotorDef"/>, a Servo or a Velocity motor, #454):
/// links, whose angle is the direction to their far joint body and which turn by a force couple,
/// or the joint's Wheel, a one-sided link that turns with its body (#1118). It measures the
/// Target's angle and speed against the Fixed side's, unwrapped across turns, and turns them
/// apart by an equal and opposite torque.
/// </summary>
public sealed class MotorSides
{
    private readonly MotorSide _fixed;
    private readonly MotorSide _target;
    private double _lastRelativeAngle;

    public MotorSides(MotorSide fixedSide, MotorSide targetSide)
    {
        _fixed = fixedSide;
        _target = targetSide;
        BuiltAngle = RelativeAngle;
        _lastRelativeAngle = BuiltAngle;
        ContinuousAngle = BuiltAngle;
    }

    public Vector2 JointPosition => _fixed.JointBody.GlobalPosition;

    public Vector2 JointLocalPosition => _fixed.JointBody.Position;

    /// <summary>The Target's angle from the Fixed side as built, in radians.</summary>
    public double BuiltAngle { get; }

    /// <summary>The Target's angle from the Fixed side, counted on across whole turns (<see cref="UpdatePhysicsState"/>).</summary>
    public double ContinuousAngle { get; private set; }

    /// <summary>The Target's turning speed relative to the Fixed side, in rad/s.</summary>
    public double Speed => _target.AngularSpeed - _fixed.AngularSpeed;

    /// <summary>The two sides' reduced inertia: what the motor's torque turns.</summary>
    public double EffectiveInertia
    {
        get
        {
            var target = _target.EffectiveInertia;
            var fixedSide = _fixed.EffectiveInertia;
            return (target * fixedSide) / (target + fixedSide);
        }
    }

    public double FixedRotation => -_fixed.Angle;

    public double TargetRelativeRotation => -RelativeAngle;

    public double BuiltRelativeRotation => -BuiltAngle;

    private double RelativeAngle => _target.Angle - _fixed.Angle;

    /// <summary>Unwraps the live angle once per physics tick, before the brain reads it.</summary>
    public void UpdatePhysicsState()
    {
        var current = RelativeAngle;
        ContinuousAngle = Mechanics.Servo.UnwrapAngle(ContinuousAngle, _lastRelativeAngle, current);
        _lastRelativeAngle = current;
    }

    /// <summary>Turns the Target by <paramref name="torque"/> and the Fixed side by its opposite.</summary>
    public void ApplyTorque(double torque)
    {
        _target.ApplyTorque(torque);
        _fixed.ApplyTorque(-torque);
    }
}

/// <summary>What a motor holds or turns at its joint: a link or the joint's Wheel (#1118).</summary>
public abstract class MotorSide
{
    protected MotorSide(RigidBody2D jointBody) => JointBody = jointBody;

    public RigidBody2D JointBody { get; }

    /// <summary>Its live direction, counter-clockwise-positive, in radians.</summary>
    public abstract double Angle { get; }

    public abstract double AngularSpeed { get; }

    public abstract double EffectiveInertia { get; }

    public abstract void ApplyTorque(double torque);

    protected static Vector2D ToVector(Vector2 value) => new(value.X, value.Y);

    protected static Vector2 ToGodot(Vector2D value) => new((float)value.X, (float)value.Y);
}

/// <summary>A link side: its direction runs from the joint body to the link's far joint body.</summary>
public sealed class MotorLinkSide(RigidBody2D jointBody, RigidBody2D farBody) : MotorSide(jointBody)
{
    private readonly RigidBody2D _farBody = farBody;

    public override double Angle => Mechanics.Servo.LinkAngle(ToVector(JointBody.GlobalPosition), ToVector(_farBody.GlobalPosition));

    public override double AngularSpeed => Mechanics.Servo.LinkAngularSpeed(
        ToVector(JointBody.GlobalPosition),
        ToVector(_farBody.GlobalPosition),
        ToVector(JointBody.LinearVelocity),
        ToVector(_farBody.LinearVelocity));

    public override double EffectiveInertia => Mechanics.Servo.LinkInertia(
        ToVector(JointBody.GlobalPosition),
        ToVector(_farBody.GlobalPosition),
        JointBody.Mass,
        _farBody.Mass);

    public override void ApplyTorque(double torque)
    {
        var couple = Mechanics.Servo.CoupleForTorque(ToVector(JointBody.GlobalPosition), ToVector(_farBody.GlobalPosition), torque);
        JointBody.ApplyCentralForce(ToGodot(couple.ForceOnJoint));
        _farBody.ApplyCentralForce(ToGodot(couple.ForceOnFar));
    }
}

/// <summary>
/// The joint's Wheel as a side (#1118): a one-sided link pointing along its own turn. The Wheel is
/// the joint body set free to turn (Creature.Wheels), so it turns by a plain torque on that body.
/// </summary>
public sealed class MotorWheelSide(RigidBody2D wheelBody) : MotorSide(wheelBody)
{
    public override double Angle => Mechanics.Servo.WheelAngle(JointBody.GlobalRotation);

    public override double AngularSpeed => Mechanics.Servo.WheelAngularSpeed(JointBody.AngularVelocity);

    public override double EffectiveInertia => JointBody.Inertia;

    public override void ApplyTorque(double torque) => JointBody.ApplyTorque((float)Mechanics.Servo.WheelBodyTorque(torque));
}
