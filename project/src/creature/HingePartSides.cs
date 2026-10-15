using Godot;
using NodeRunner.Domain;

namespace NodeRunner.Creature;

/// <summary>
/// A hinge part's two sides (<see cref="IHingePartDef"/>): a Servo's, a Velocity motor's (#454), a Brake's (#1040),
/// an Angle sensor's (#1204) or a Stepper's (#1037). They are links, whose angle is the direction to their far joint body and which
/// turn by a force couple, or the joint's Wheel, a one-sided link that turns with its body (#1118). It measures the
/// Target's angle and speed against the Fixed side's, unwrapped across turns, and turns them
/// apart by an equal and opposite torque.
/// </summary>
public sealed class HingePartSides
{
    private readonly HingePartSide _fixed;
    private readonly HingePartSide _target;
    private double _lastRelativeAngle;

    public HingePartSides(HingePartSide fixedSide, HingePartSide targetSide)
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

    /// <summary>
    /// What a Brake's torque turns (#1040): <see cref="EffectiveInertia"/>, but two links share their
    /// joint's mass (<see cref="Mechanics.HingePartSideGeometry.SharedJointInertia"/>), so it never reads
    /// heavier than the joint is. A Brake reading it heavier would chatter.
    /// </summary>
    public double BrakeInertia => _fixed is HingePartLinkSide fixedLink && _target is HingePartLinkSide targetLink
        ? fixedLink.SharedInertia(targetLink)
        : EffectiveInertia;

    /// <summary>The Fixed side's direction as a Godot rotation, clockwise-positive.</summary>
    public double FixedRotation => -_fixed.Angle;

    /// <summary>The Target side's direction as a Godot rotation: a Wheel's is its body's own.</summary>
    public double TargetRotation => -_target.Angle;

    public double TargetRelativeRotation => -RelativeAngle;

    public double BuiltRelativeRotation => -BuiltAngle;

    private double RelativeAngle => _target.Angle - _fixed.Angle;

    /// <summary>Unwraps the live angle once per physics tick, before the network reads it.</summary>
    public void UpdatePhysicsState()
    {
        var current = RelativeAngle;
        ContinuousAngle = Mechanics.HingePartSideGeometry.UnwrapAngle(ContinuousAngle, _lastRelativeAngle, current);
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
public abstract class HingePartSide
{
    protected HingePartSide(RigidBody2D jointBody) => JointBody = jointBody;

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
public sealed class HingePartLinkSide(RigidBody2D jointBody, RigidBody2D farBody) : HingePartSide(jointBody)
{
    private readonly RigidBody2D _farBody = farBody;

    public override double Angle => Mechanics.HingePartSideGeometry.LinkAngle(ToVector(JointBody.GlobalPosition), ToVector(_farBody.GlobalPosition));

    public override double AngularSpeed => Mechanics.HingePartSideGeometry.LinkAngularSpeed(
        ToVector(JointBody.GlobalPosition),
        ToVector(_farBody.GlobalPosition),
        ToVector(JointBody.LinearVelocity),
        ToVector(_farBody.LinearVelocity));

    public override double EffectiveInertia => Mechanics.HingePartSideGeometry.LinkInertia(
        ToVector(JointBody.GlobalPosition),
        ToVector(_farBody.GlobalPosition),
        JointBody.Mass,
        _farBody.Mass);

    /// <summary>This side and <paramref name="target"/> turned apart at their shared joint (<see cref="HingePartSides.BrakeInertia"/>).</summary>
    public double SharedInertia(HingePartLinkSide target) => Mechanics.HingePartSideGeometry.SharedJointInertia(
        ToVector(JointBody.GlobalPosition),
        ToVector(_farBody.GlobalPosition),
        ToVector(target._farBody.GlobalPosition),
        JointBody.Mass,
        _farBody.Mass,
        target._farBody.Mass);

    public override void ApplyTorque(double torque)
    {
        var couple = Mechanics.HingePartSideGeometry.CoupleForTorque(ToVector(JointBody.GlobalPosition), ToVector(_farBody.GlobalPosition), torque);
        JointBody.ApplyCentralForce(ToGodot(couple.ForceOnJoint));
        _farBody.ApplyCentralForce(ToGodot(couple.ForceOnFar));
    }
}

/// <summary>
/// The joint's Wheel as a side (#1118): a one-sided link pointing along its own turn. The Wheel is
/// the joint body set free to turn (Creature.Wheels), so it turns by a plain torque on that body.
/// </summary>
public sealed class HingePartWheelSide(RigidBody2D wheelBody) : HingePartSide(wheelBody)
{
    public override double Angle => Mechanics.HingePartSideGeometry.WheelAngle(JointBody.GlobalRotation);

    public override double AngularSpeed => Mechanics.HingePartSideGeometry.WheelAngularSpeed(JointBody.AngularVelocity);

    public override double EffectiveInertia => JointBody.Inertia;

    public override void ApplyTorque(double torque) => JointBody.ApplyTorque((float)Mechanics.HingePartSideGeometry.WheelBodyTorque(torque));
}
