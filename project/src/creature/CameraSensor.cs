using Godot;
using NodeRunner.Domain;
using NodeRunner.Mechanics;

namespace NodeRunner.Creature;

/// <summary>
/// One camera on its beam (#575, #604, #594): three <see cref="RayCast2D"/> children at the beam's
/// midpoint, fanned around its aim by the Mechanics <see cref="CameraRays"/> so they turn with the beam.
/// They see the ground only; <see cref="CameraRays.Read"/> turns their hits into the brain inputs.
/// </summary>
public sealed class CameraSensor : IBeamSensor
{
    private const uint _groundMask = 1;

    private readonly RayCast2D[] _rays = new RayCast2D[CameraRays.RayCount];
    private readonly double?[] _hitDistances = new double?[CameraRays.RayCount];

    /// <param name="beamBody">The camera's beam; its +x runs along the beam, from node A to node B.</param>
    /// <param name="aim">The camera's aim relative to its beam (<see cref="SensorDef.Aim"/>).</param>
    public CameraSensor(RigidBody2D beamBody, double aim)
    {
        ArgumentNullException.ThrowIfNull(beamBody);

        for (var i = 0; i < _rays.Length; i++)
        {
            var target = CameraRays.LocalRayTarget(i, aim);
            _rays[i] = new RayCast2D
            {
                Name = $"CameraRay{i}",
                TargetPosition = new Vector2((float)target.X, (float)target.Y),
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

    public int ValueCount => BrainPorts.CameraChannels.Count;

    public void Read(double[] values, int startIndex, double dt)
    {
        for (var i = 0; i < _rays.Length; i++)
        {
            var ray = _rays[i];
            _hitDistances[i] = ray.IsColliding()
                ? ray.GlobalPosition.DistanceTo(ray.GetCollisionPoint())
                : null;
        }

        CameraRays.Read(_hitDistances, values.AsSpan(startIndex, ValueCount));
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
