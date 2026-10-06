using Godot;
using NodeRunner.Domain;

namespace NodeRunner.Creature;

/// <summary>
/// A Servo (#452) between two links at one joint. Godot represents all Servo links uniformly here:
/// the link angle is the direction from the Servo joint body to the link's far joint body, and
/// torque is applied as a force couple on those two bodies.
/// </summary>
public sealed class ServoJoint
{
    private readonly ServoLinkSide _fixed;
    private readonly ServoLinkSide _target;
    private Mechanics.ServoMotor _motor;
    private double _lastRelativeAngle;
    private double _continuousRelativeAngle;

    public ServoJoint(ServoDef definition, ServoLinkSide fixedLink, ServoLinkSide targetLink)
    {
        Definition = definition;
        _fixed = fixedLink;
        _target = targetLink;
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

public sealed class ServoLinkSide
{
    private readonly RigidBody2D _farBody;

    public ServoLinkSide(RigidBody2D jointBody, RigidBody2D farBody)
    {
        JointBody = jointBody;
        _farBody = farBody;
    }

    public RigidBody2D JointBody { get; }

    public double Angle => Mechanics.Servo.LinkAngle(ToVector(JointBody.GlobalPosition), ToVector(_farBody.GlobalPosition));

    public double AngularSpeed => Mechanics.Servo.LinkAngularSpeed(
        ToVector(JointBody.GlobalPosition),
        ToVector(_farBody.GlobalPosition),
        ToVector(JointBody.LinearVelocity),
        ToVector(_farBody.LinearVelocity));

    public double EffectiveInertia => Mechanics.Servo.LinkInertia(
        ToVector(JointBody.GlobalPosition),
        ToVector(_farBody.GlobalPosition),
        JointBody.Mass,
        _farBody.Mass);

    public void ApplyTorque(double torque)
    {
        var couple = Mechanics.Servo.CoupleForTorque(ToVector(JointBody.GlobalPosition), ToVector(_farBody.GlobalPosition), torque);
        JointBody.ApplyCentralForce(ToGodot(couple.ForceOnJoint));
        _farBody.ApplyCentralForce(ToGodot(couple.ForceOnFar));
    }

    private static Vector2D ToVector(Vector2 value) => new(value.X, value.Y);

    private static Vector2 ToGodot(Vector2D value) => new((float)value.X, (float)value.Y);
}
