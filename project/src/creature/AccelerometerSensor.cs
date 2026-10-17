using Godot;
using NodeRunner.Domain;
using NodeRunner.Mechanics;

namespace NodeRunner.Creature;

/// <summary>
/// One accelerometer on its beam: each physics tick it measures the acceleration where its picture
/// sits on the beam body (<see cref="SensorPicture.Centre"/>), turns it into specific force (at rest 1 g up), steps the Mechanics <see cref="Accelerometer"/> proof mass and
/// writes its two readings. <see cref="CurrentProofMass"/> is what the network reads.
/// </summary>
public sealed class AccelerometerSensor : IBeamSensor
{
    private readonly RigidBody2D _beamBody;
    private readonly Vector2 _at;
    private readonly int _upSign;
    private readonly double _gravity;
    private Vector2 _previousVelocity;

    /// <param name="beamBody">The beam it sits on, turning about its origin.</param>
    /// <param name="at">Where its picture sits on the beam body, in the body's space.</param>
    /// <param name="upSign">Which side of the beam is its up, +1 or −1.</param>
    /// <param name="gravity">The world's gravity, for the 1 g it feels at rest.</param>
    public AccelerometerSensor(RigidBody2D beamBody, Vector2 at, int upSign, double gravity)
    {
        _beamBody = beamBody ?? throw new ArgumentNullException(nameof(beamBody));
        _at = at;
        _upSign = upSign;
        _gravity = gravity > 0 && double.IsFinite(gravity)
            ? gravity
            : throw new ArgumentOutOfRangeException(nameof(gravity), "Gravity must be finite and positive.");
        Reset();
    }

    public ProofMass CurrentProofMass { get; private set; }

    public int UpSign => _upSign;

    public int ValueCount => NetworkPorts.AccelerometerChannels.Count;

    public void Reset()
    {
        _previousVelocity = Vector2.Zero;
        CurrentProofMass = Accelerometer.Rest(RestSpecificForce());
    }

    public void Read(double[] values, int startIndex, double dt)
    {
        var state = PhysicsServer2D.BodyGetDirectState(_beamBody.GetRid());
        var velocity = state.GetVelocityAtLocalPosition(_beamBody.GlobalTransform.BasisXform(_at));
        var acceleration = (velocity - _previousVelocity) / (float)dt;
        _previousVelocity = velocity;

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
