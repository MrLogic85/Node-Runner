using Godot;
using NodeRunner.Domain;
using NodeRunner.Mechanics;

namespace NodeRunner.Creature;

/// <summary>
/// One accelerometer on its beam: each physics tick it measures the beam's midpoint acceleration
/// (the body origin), turns it into specific force (at rest 1 g up), steps the Mechanics <see cref="Accelerometer"/> proof mass and
/// writes its two readings. <see cref="CurrentProofMass"/> is what the brain reads.
/// </summary>
public sealed class AccelerometerSensor : IBeamSensor
{
    private readonly RigidBody2D _beamBody;
    private readonly int _upSign;
    private readonly double _gravity;
    private Vector2 _previousMidpointVelocity;

    public AccelerometerSensor(RigidBody2D beamBody, int upSign, double gravity)
    {
        _beamBody = beamBody ?? throw new ArgumentNullException(nameof(beamBody));
        _upSign = upSign;
        _gravity = gravity > 0 && double.IsFinite(gravity)
            ? gravity
            : throw new ArgumentOutOfRangeException(nameof(gravity), "Gravity must be finite and positive.");
        Reset();
    }

    public ProofMass CurrentProofMass { get; private set; }

    public int UpSign => _upSign;

    public int ValueCount => BrainPorts.AccelerometerChannels.Count;

    public void Reset()
    {
        _previousMidpointVelocity = Vector2.Zero;
        CurrentProofMass = Accelerometer.Rest(RestSpecificForce());
    }

    public void Read(double[] values, int startIndex, double dt)
    {
        var velocity = _beamBody.LinearVelocity;
        var acceleration = (velocity - _previousMidpointVelocity) / (float)dt;
        _previousMidpointVelocity = velocity;

        var worldSpecificForce = Accelerometer.SpecificForce(new Vector2D(acceleration.X, acceleration.Y), _gravity);
        var sensorForce = Accelerometer.ToSensorFrame(worldSpecificForce, _beamBody.Rotation, _upSign);
        CurrentProofMass = Accelerometer.Step(CurrentProofMass, sensorForce, dt);
        var reading = Accelerometer.Reading(CurrentProofMass);
        values[startIndex + 0] = reading.X;
        values[startIndex + 1] = reading.Y;
    }

    private Vector2D RestSpecificForce() =>
        Accelerometer.ToSensorFrame(new Vector2D(0, -1), _beamBody.Rotation, _upSign);
}
