using Godot;
using NodeRunner.Domain;

namespace NodeRunner.Creature;

public sealed class Muscle
{
    private const float _maxLengthOffset = 0.35f;

    private readonly DampedSpringJoint2D _joint;
    private readonly float _baseRestLength;

    public Muscle(MuscleDef definition, DampedSpringJoint2D joint)
    {
        Definition = definition;
        _joint = joint;
        _baseRestLength = joint.RestLength;
    }

    public MuscleDef Definition { get; }

    public void ApplyTarget(double target)
    {
        var clamped = (float)Math.Clamp(target, -1, 1);
        _joint.RestLength = _baseRestLength * (1 + (clamped * _maxLengthOffset));
    }
}
