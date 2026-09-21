using Godot;

namespace NodeRunner.Creature;

/// <summary>
/// Reads the sensor values a core exposes: three fixed rays (down, forward,
/// forward-down — all in the mounting beam's local frame), pitch (the
/// mounting beam's rotation), elevation (height above the world origin), and
/// speed (the mounting beam's linear speed). A core is a sensor package, NOT
/// the neural model — see docs/CREATURE_MODEL.md.
/// </summary>
public sealed class CoreSensors
{
    public const int ValueCount = 6;

    // Labels for the mapping display (issue #42), in the same order as
    // Read() writes values.
    public static readonly string[] ValueNames =
    {
        "Ray down", "Ray forward", "Ray forward-down", "Pitch", "Elevation", "Speed",
    };

    // Placeholder normalization scales for the prototype; tune once a real
    // arena size and creature speed range exist.
    private const double _elevationScale = 200.0;
    private const double _speedScale = 400.0;

    private readonly RigidBody2D _anchorBeam;
    private readonly RayCast2D _rayDown;
    private readonly RayCast2D _rayForward;
    private readonly RayCast2D _rayForwardDown;

    public CoreSensors(RigidBody2D anchorBeam, RayCast2D rayDown, RayCast2D rayForward, RayCast2D rayForwardDown)
    {
        _anchorBeam = anchorBeam;
        _rayDown = rayDown;
        _rayForward = rayForward;
        _rayForwardDown = rayForwardDown;
    }

    public void Read(double[] values, int startIndex)
    {
        values[startIndex + 0] = RayDistance(_rayDown);
        values[startIndex + 1] = RayDistance(_rayForward);
        values[startIndex + 2] = RayDistance(_rayForwardDown);
        values[startIndex + 3] = Mathf.Wrap(_anchorBeam.Rotation, -Mathf.Pi, Mathf.Pi) / Mathf.Pi;
        values[startIndex + 4] = Math.Clamp(-_anchorBeam.GlobalPosition.Y / _elevationScale, -1, 1);
        values[startIndex + 5] = Math.Clamp(_anchorBeam.LinearVelocity.Length() / _speedScale, 0, 1);
    }

    public void SetCollisionMask(uint collisionMask)
    {
        _rayDown.CollisionMask = collisionMask;
        _rayForward.CollisionMask = collisionMask;
        _rayForwardDown.CollisionMask = collisionMask;
    }

    // 1 = nothing within range, 0 = touching. A simple normalized distance
    // reading; ray count/placement is a 0.2.0 starting point (see
    // docs/CREATURE_MODEL.md future ideas).
    private static double RayDistance(RayCast2D ray)
    {
        if (!ray.IsColliding())
        {
            return 1;
        }

        var rayLength = ray.TargetPosition.Length();
        if (rayLength <= 0.0001f)
        {
            return 0;
        }

        return Math.Clamp(ray.GlobalPosition.DistanceTo(ray.GetCollisionPoint()) / rayLength, 0, 1);
    }
}
