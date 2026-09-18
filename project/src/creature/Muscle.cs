using Godot;
using NodeRunner.Domain;

namespace NodeRunner.Creature;

public sealed class Muscle
{
    private const float _maxLengthOffset = 0.65f;
    private const float _bendForceScale = 5.0f;

    private readonly DampedSpringJoint2D _joint;
    private readonly RigidBody2D _jointA;
    private readonly RigidBody2D _jointB;
    private readonly float _baseRestLength;
    private readonly float _bendDirection;
    private readonly float _maxBendForce;

    public Muscle(
        MuscleDef definition,
        DampedSpringJoint2D joint,
        RigidBody2D jointA,
        RigidBody2D jointB,
        float bendDirection,
        float maxBendForce)
    {
        Definition = definition;
        _joint = joint;
        _jointA = jointA;
        _jointB = jointB;
        _baseRestLength = joint.RestLength;
        _bendDirection = bendDirection;
        _maxBendForce = maxBendForce;
    }

    public MuscleDef Definition { get; }

    public void ApplyTarget(double target)
    {
        var clamped = (float)Math.Clamp(target, -1, 1);
        _joint.RestLength = _baseRestLength * (1 + (clamped * _maxLengthOffset));

        var axis = _jointB.GlobalPosition - _jointA.GlobalPosition;
        if (axis.LengthSquared() <= 0.0001f)
        {
            return;
        }

        var normal = new Vector2(-axis.Y, axis.X).Normalized();
        var force = normal * (clamped * _maxBendForce * _bendForceScale * _bendDirection);
        _jointA.ApplyCentralForce(force);
        _jointB.ApplyCentralForce(-force);
    }
}
