using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

/// <summary>Where an Accelerometer's beam is this frame in Build: its midpoint, rotation and built up side.</summary>
public readonly record struct SensorPose(int SensorId, Vector2D Midpoint, double Rotation, int UpSign);

/// <summary>
/// Moves each Accelerometer's weight in Build as its beam is dragged or turned (#576). Every frame
/// the canvas passes each beam's pose; the midpoint's acceleration plus gravity drives the same
/// <see cref="Accelerometer"/> proof mass the sim uses, so the weight swings and then settles. A
/// sensor seen for the first time, or whose beam's up side flipped, starts at rest.
/// </summary>
public sealed class BuildSensorMotion
{
    /// <summary>Below this share of the weight's travel, a weight with its beam at rest counts as settled.</summary>
    private const double _settled = 0.002;

    /// <summary>
    /// How quickly the beam's velocity follows the finger, in seconds. Touch moves arrive out of
    /// step with frames, so the raw velocity flickers; smoothing it keeps the weight from shaking.
    /// </summary>
    private const double _velocitySmoothing = 0.05;

    /// <summary>A smoothed velocity below this, in canvas units per second, counts as stopped.</summary>
    private const double _stopped = 1;

    private readonly Dictionary<int, State> _states = [];

    /// <summary>
    /// Steps every weight by <paramref name="dt"/> seconds and forgets sensors not in
    /// <paramref name="poses"/>. True while any weight or beam is still moving, so the canvas
    /// knows to draw again.
    /// </summary>
    public bool Advance(IReadOnlyList<SensorPose> poses, double dt, double gravity)
    {
        ArgumentNullException.ThrowIfNull(poses);
        if (!double.IsFinite(dt) || dt <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dt), "Time step must be finite and positive.");
        }

        var moving = false;
        var seen = new HashSet<int>();
        foreach (var pose in poses)
        {
            seen.Add(pose.SensorId);
            if (!_states.TryGetValue(pose.SensorId, out var state) || state.UpSign != pose.UpSign)
            {
                _states[pose.SensorId] = AtRest(pose);
                continue;
            }

            var follow = 1 - Math.Exp(-dt / _velocitySmoothing);
            var velocity = Lerp(state.Velocity, Divide(Subtract(pose.Midpoint, state.Midpoint), dt), follow);
            if (Math.Abs(velocity.X) < _stopped && Math.Abs(velocity.Y) < _stopped && pose.Midpoint == state.Midpoint)
            {
                velocity = default;
            }

            var acceleration = Divide(Subtract(velocity, state.Velocity), dt);
            var force = Accelerometer.ToSensorFrame(Accelerometer.SpecificForce(acceleration, gravity), pose.Rotation, pose.UpSign);
            var proofMass = Accelerometer.Step(state.ProofMass, force, dt);
            var rest = Accelerometer.Rest(Accelerometer.ToSensorFrame(new Vector2D(0, -1), pose.Rotation, pose.UpSign));
            var still = velocity == default && IsNear(proofMass, rest);
            _states[pose.SensorId] = new State(pose.Midpoint, velocity, pose.UpSign, still ? rest : proofMass);
            moving |= !still;
        }

        foreach (var gone in _states.Keys.Where(id => !seen.Contains(id)).ToArray())
        {
            _states.Remove(gone);
        }

        return moving;
    }

    /// <summary>Where the sensor's drawn weight sits (<see cref="Accelerometer.WeightOffset"/>), or null before it is first seen.</summary>
    public Vector2D? WeightOffset(int sensorId) =>
        _states.TryGetValue(sensorId, out var state) ? Accelerometer.WeightOffset(state.ProofMass) : null;

    private static State AtRest(SensorPose pose) => new(
        pose.Midpoint,
        default,
        pose.UpSign,
        Accelerometer.Rest(Accelerometer.ToSensorFrame(new Vector2D(0, -1), pose.Rotation, pose.UpSign)));

    private static bool IsNear(ProofMass state, ProofMass rest)
    {
        var limit = _settled * Accelerometer.ReferenceDisplacement;
        var speedLimit = limit * Accelerometer.AngularFrequency;
        return Math.Abs(state.Displacement.X - rest.Displacement.X) < limit
            && Math.Abs(state.Displacement.Y - rest.Displacement.Y) < limit
            && Math.Abs(state.Velocity.X) < speedLimit
            && Math.Abs(state.Velocity.Y) < speedLimit;
    }

    private static Vector2D Lerp(Vector2D from, Vector2D to, double weight) =>
        new(from.X + ((to.X - from.X) * weight), from.Y + ((to.Y - from.Y) * weight));

    private static Vector2D Subtract(Vector2D a, Vector2D b) => new(a.X - b.X, a.Y - b.Y);

    private static Vector2D Divide(Vector2D value, double divisor) => new(value.X / divisor, value.Y / divisor);

    private readonly record struct State(Vector2D Midpoint, Vector2D Velocity, int UpSign, ProofMass ProofMass);
}
