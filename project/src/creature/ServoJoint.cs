using Godot;
using NodeRunner.Domain;

namespace NodeRunner.Creature;

/// <summary>
/// A Servo (#452) between two sides at one joint (<see cref="ServoSide"/>): links, whose angle is
/// the direction to their far joint body and which turn by a force couple, or the joint's Wheel,
/// a one-sided link that turns with its body (#1118).
/// </summary>
public sealed class ServoJoint
{
    private readonly ServoSide _fixed;
    private readonly ServoSide _target;
    private Mechanics.ServoMotor _motor;
    private double _lastRelativeAngle;
    private double _continuousRelativeAngle;

    public ServoJoint(ServoDef definition, ServoSide fixedSide, ServoSide targetSide)
    {
        Definition = definition;
        _fixed = fixedSide;
        _target = targetSide;
        BuiltAngle = RelativeAngle;
        _lastRelativeAngle = BuiltAngle;
        _continuousRelativeAngle = BuiltAngle;
    }

    public ServoDef Definition { get; }

    public Vector2 JointPosition => _fixed.JointBody.GlobalPosition;

    public Vector2 JointLocalPosition => _fixed.JointBody.Position;

    public double BuiltAngle { get; }

    public double Speed => _target.AngularSpeed - _fixed.AngularSpeed;

    public double AngleInput => Mechanics.Servo.AngleInput(_continuousRelativeAngle, Definition, BuiltAngle);

    public double SpeedInput => Mechanics.Servo.SpeedInput(Speed, Definition.MaxSpeed);

    public double FixedRotation => -_fixed.Angle;

    public double TargetRelativeRotation => -RelativeAngle;

    public double BuiltRelativeRotation => -BuiltAngle;

    private double RelativeAngle => _target.Angle - _fixed.Angle;

    public void UpdatePhysicsState()
    {
        var current = RelativeAngle;
        _continuousRelativeAngle = Mechanics.Servo.UnwrapAngle(_continuousRelativeAngle, _lastRelativeAngle, current);
        _lastRelativeAngle = current;
    }

    /// <param name="position">The brain's angle output, −1 lower end … +1 upper end.</param>
    /// <param name="strength">The brain's strength output, 0…1 of its Max strength setting.</param>
    /// <param name="step">The physics step, in seconds.</param>
    public void Drive(double position, double strength, double step)
    {
        var angle = _continuousRelativeAngle;
        var speed = Speed;
        var effectiveInertia = EffectiveInertia();
        _motor = Mechanics.Servo.NextMotor(Definition, BuiltAngle, angle, speed, position, strength, _motor, step, effectiveInertia);
        // Godot's angular joints cannot uniformly limit Beam, Piston and Spring link directions.
        // The soft stop is explicit-step stable by capping its gains to the live effective inertia.
        var torque = _motor.Torque + Mechanics.Servo.EndStopTorque(Definition, BuiltAngle, angle, speed, effectiveInertia, step);
        _target.ApplyTorque(torque);
        _fixed.ApplyTorque(-torque);
    }

    private double EffectiveInertia()
    {
        var target = _target.EffectiveInertia;
        var fixedSide = _fixed.EffectiveInertia;
        return (target * fixedSide) / (target + fixedSide);
    }
}

/// <summary>What a Servo holds or turns at its joint: a link or the joint's Wheel (#1118).</summary>
public abstract class ServoSide
{
    protected ServoSide(RigidBody2D jointBody) => JointBody = jointBody;

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
public sealed class ServoLinkSide(RigidBody2D jointBody, RigidBody2D farBody) : ServoSide(jointBody)
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
public sealed class ServoWheelSide(RigidBody2D wheelBody) : ServoSide(wheelBody)
{
    public override double Angle => Mechanics.Servo.WheelAngle(JointBody.GlobalRotation);

    public override double AngularSpeed => Mechanics.Servo.WheelAngularSpeed(JointBody.AngularVelocity);

    public override double EffectiveInertia => JointBody.Inertia;

    public override void ApplyTorque(double torque) => JointBody.ApplyTorque((float)Mechanics.Servo.WheelBodyTorque(torque));
}
