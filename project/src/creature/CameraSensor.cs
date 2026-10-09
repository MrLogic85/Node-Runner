using Godot;
using NodeRunner.Domain;
using NodeRunner.Mechanics;

namespace NodeRunner.Creature;

/// <summary>
/// One camera on its beam (#575, #604, #594, #578): a <see cref="RayCast2D"/> child per ray at the
/// beam's midpoint, fanned around its aim by the Mechanics <see cref="CameraRays"/> so they turn with
/// the beam. They see the ground only; <see cref="CameraRays.Read"/> turns their hits into the brain
/// inputs, relative to the camera's range.
/// </summary>
public sealed class CameraSensor : IBeamSensor
{
    private const uint _groundMask = 1;

    private readonly RayCast2D[] _rays;
    private readonly double?[] _hitDistances;
    private readonly double _range;

    /// <param name="beamBody">The camera's beam; its +x runs along the beam, from node A to node B.</param>
    /// <param name="camera">The camera: its aim relative to its beam, rays, spread and range (<see cref="SensorDef"/>).</param>
    public CameraSensor(RigidBody2D beamBody, SensorDef camera)
    {
        ArgumentNullException.ThrowIfNull(beamBody);
        ArgumentNullException.ThrowIfNull(camera);

        var targets = CameraRays.LocalRayTargets(camera, camera.Aim ?? 0);
        _rays = new RayCast2D[targets.Length];
        _hitDistances = new double?[targets.Length];
        _range = camera.Range!.Value;
        for (var i = 0; i < _rays.Length; i++)
        {
            _rays[i] = new RayCast2D
            {
                Name = $"CameraRay{i}",
                TargetPosition = new Vector2((float)targets[i].X, (float)targets[i].Y),
                CollisionMask = _groundMask,
                Enabled = true,
            };
            beamBody.AddChild(_rays[i]);
        }
    }

    /// <summary>Where each ray starts, in global space (the beam's midpoint).</summary>
    public Vector2 GlobalOrigin => _rays[0].GlobalPosition;

    /// <summary>Each ray's ground hit as of the last physics step, in global space; a ray that sees nothing has none.</summary>
    public IEnumerable<Vector2> GlobalHits => _rays.Where(ray => ray.IsColliding()).Select(ray => ray.GetCollisionPoint());

    // One reading per ray, then hit (BrainPorts.CameraChannels).
    public int ValueCount => _rays.Length + 1;

    public void Read(double[] values, int startIndex, double dt)
    {
        for (var i = 0; i < _rays.Length; i++)
        {
            var ray = _rays[i];
            _hitDistances[i] = ray.IsColliding()
                ? ray.GlobalPosition.DistanceTo(ray.GetCollisionPoint())
                : null;
        }

        CameraRays.Read(_hitDistances, _range, values.AsSpan(startIndex, ValueCount));
    }

    // A ray only casts on a physics step, so after ResetPose places the creature, cast now and
    // the first reading of a trial already sees the ground from the start pose.
    public void Reset()
    {
        foreach (var ray in _rays)
        {
            if (ray.IsInsideTree())
            {
                ray.ForceRaycastUpdate();
            }
        }
    }
}
