using Godot;

namespace NodeRunner.Creature;

public sealed class Sensors
{
    private const double _angularVelocityScale = 8.0;
    private const double _oscillatorFrequencyHz = 1.8;

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

    public int Count => (_joints.Length * 2) + 2;

    // Stable order for 0.1.0: first a sin/cos oscillator clock, then for each
    // joint in CreatureDef.Joints order, angle-from-rest and angular velocity.
    public void Read(double[] values, double elapsedSeconds)
    {
        ArgumentNullException.ThrowIfNull(values);

        if (values.Length != Count)
        {
            throw new ArgumentException("Sensor buffer length must match the sensor count.", nameof(values));
        }

        var phase = elapsedSeconds * Math.Tau * _oscillatorFrequencyHz;
        values[0] = Math.Sin(phase);
        values[1] = Math.Cos(phase);

        var valueIndex = 2;
        for (var i = 0; i < _joints.Length; i++)
        {
            var joint = _joints[i];
            var angle = Mathf.Wrap(joint.Rotation - _restAngles[i], -Mathf.Pi, Mathf.Pi);
            values[valueIndex++] = angle / Mathf.Pi;
            values[valueIndex++] = Math.Clamp(joint.AngularVelocity / _angularVelocityScale, -1, 1);
        }
    }
}
