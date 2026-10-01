using Godot;
using NodeRunner.Domain;

namespace NodeRunner.Creature;

/// <summary>
/// One camera on its beam (#575, #604, #594): three <see cref="RayCast2D"/> children at the beam's
/// midpoint, fanned around its aim by the Domain <see cref="CameraRays"/> so they turn with the beam.
/// They see the ground only and each writes its nearness: 0 with nothing in range, 1 at contact.
/// </summary>
public sealed class CameraSensor : IBeamSensor
{
    private const uint _groundMask = 1;

    private readonly RayCast2D[] _rays = new RayCast2D[CameraRays.RayCount];

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

    /// <summary>Where each ray ends as of the last physics step: its ground hit, or its full length.</summary>
    public IEnumerable<Vector2> GlobalRayEnds => _rays.Select(ray => ray.IsColliding()
        ? ray.GetCollisionPoint()
        : ray.ToGlobal(ray.TargetPosition));

    public string GroupKind => "Camera";

    public IReadOnlyList<string> ValueNames => CameraRays.RayNames;

    public void Read(double[] values, int startIndex, double dt)
    {
        for (var i = 0; i < _rays.Length; i++)
        {
            var ray = _rays[i];
            double? hitDistance = ray.IsColliding()
                ? ray.GlobalPosition.DistanceTo(ray.GetCollisionPoint())
                : null;
            values[startIndex + i] = CameraRays.Reading(hitDistance);
        }
    }

    // After ResetPose the rays still hold the last pose's hits until the next physics step,
    // so refresh them now and the first reading of a trial never sees the previous trial.
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
