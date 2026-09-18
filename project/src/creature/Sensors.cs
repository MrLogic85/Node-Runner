using Godot;

namespace NodeRunner.Creature;

public sealed class Sensors
{
    private const double _angularVelocityScale = 8.0;

    private readonly RigidBody2D[] _joints;
    private readonly float[] _restAngles;

    public Sensors(IReadOnlyList<RigidBody2D> joints)
    {
        ArgumentNullException.ThrowIfNull(joints);

        _joints = joints.ToArray();
        _restAngles = new float[_joints.Length];

        for (var i = 0; i < _joints.Length; i++)
        {
            _restAngles[i] = _joints[i].Rotation;
        }
    }

    public int Count => _joints.Length * 2;

    // Stable order for v0.1: for each joint in CreatureDef.Joints order,
    // emit angle-from-rest normalized by Pi, then angular velocity scaled to [-1, 1].
    public void Read(double[] values)
    {
        ArgumentNullException.ThrowIfNull(values);

        if (values.Length != Count)
        {
            throw new ArgumentException("Sensor buffer length must match the sensor count.", nameof(values));
        }

        var valueIndex = 0;
        for (var i = 0; i < _joints.Length; i++)
        {
            var joint = _joints[i];
            var angle = Mathf.Wrap(joint.Rotation - _restAngles[i], -Mathf.Pi, Mathf.Pi);
            values[valueIndex++] = angle / Mathf.Pi;
            values[valueIndex++] = Math.Clamp(joint.AngularVelocity / _angularVelocityScale, -1, 1);
        }
    }
}
