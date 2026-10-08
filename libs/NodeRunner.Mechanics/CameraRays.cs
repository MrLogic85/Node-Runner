using NodeRunner.Domain;

namespace NodeRunner.Mechanics;

/// <summary>
/// The camera's pure math (#575, #604, #594): three rays from its beam's midpoint, fanned
/// <see cref="Spread"/> apart around the camera's aim. The aim is an angle relative to its beam's
/// direction (from its first node to its second), so the camera turns with the beam. A placed
/// camera looks level, at the world's forward as built (<see cref="SensorDef.DefaultAim"/>, #622), which
/// makes its rays look forward-up, forward and forward-down. Rays are named symmetrically around the centre ray,
/// seen from the camera looking along them, and run left to right. Each ray reads how near the
/// ground is: 0 when nothing is in range, rising linearly to 1 at contact, and one more input says
/// whether any ray sees the ground at all (#1032). Stateless and shared
/// by the sim, Build and the sensor picture, like <see cref="Accelerometer"/>. See
/// docs/CREATURE_MODEL.md.
/// </summary>
public static class CameraRays
{
    public const int RayCount = 3;

    public const double RayLength = 220;

    /// <summary>The angle between neighbouring rays: 45°, so the fan spans 90°.</summary>
    public const double Spread = Math.PI / 4;


    /// <summary>The beam's direction in the world, from <paramref name="nodeA"/> to <paramref name="nodeB"/>; 0 for a beam of no length.</summary>
    public static double BeamAngle(Vector2D nodeA, Vector2D nodeB) =>
        nodeA == nodeB ? 0 : Math.Atan2(nodeB.Y - nodeA.Y, nodeB.X - nodeA.X);


    /// <summary>The aim that looks along <paramref name="worldAngle"/> on the beam from <paramref name="nodeA"/> to <paramref name="nodeB"/>.</summary>
    public static double AimAlong(double worldAngle, Vector2D nodeA, Vector2D nodeB)
    {
        if (!double.IsFinite(worldAngle))
        {
            throw new ArgumentOutOfRangeException(nameof(worldAngle), "World angle must be finite.");
        }

        return Wrap(worldAngle - BeamAngle(nodeA, nodeB));
    }

    /// <summary>
    /// The ray's target in its beam's local frame (+x along the beam), <see cref="RayLength"/>
    /// long: the centre ray along <paramref name="aim"/>, left1 and right1 <see cref="Spread"/>
    /// before and after it.
    /// </summary>
    public static Vector2D LocalRayTarget(int ray, double aim)
    {
        if (ray is < 0 or >= RayCount)
        {
            throw new ArgumentOutOfRangeException(nameof(ray), "Ray must be 0, 1 or 2.");
        }

        if (!double.IsFinite(aim))
        {
            throw new ArgumentOutOfRangeException(nameof(aim), "Aim must be finite.");
        }

        var angle = aim + ((ray - ((RayCount - 1) / 2.0)) * Spread);
        return new Vector2D(Math.Cos(angle) * RayLength, Math.Sin(angle) * RayLength);
    }

    /// <summary>
    /// One ray's brain input, its nearness: <c>1 − distance / <see cref="RayLength"/></c> clamped to
    /// 0–1, and 0 with no hit, so nothing in view adds nothing to the brain's weighted sum.
    /// </summary>
    public static double Reading(double? hitDistance) =>
        hitDistance is { } distance ? Math.Clamp(1 - (distance / RayLength), 0, 1) : 0;

    /// <summary>
    /// Writes the camera's brain inputs from each ray's <paramref name="hitDistances"/> (null where it
    /// sees nothing), in <see cref="BrainPorts.CameraChannels"/> order: each ray's <see cref="Reading"/>,
    /// then hit, 1 when any ray sees the ground and else 0. Hit tells far ground from none: a hit at
    /// the very end of a ray reads nearness 0 but hit 1 (#1032).
    /// </summary>
    public static void Read(ReadOnlySpan<double?> hitDistances, Span<double> values)
    {
        if (hitDistances.Length != RayCount)
        {
            throw new ArgumentException($"A camera has {RayCount} rays.", nameof(hitDistances));
        }

        var hit = false;
        for (var ray = 0; ray < RayCount; ray++)
        {
            values[ray] = Reading(hitDistances[ray]);
            hit |= hitDistances[ray] is not null;
        }

        values[RayCount] = hit ? 1 : 0;
    }

    /// <summary><paramref name="angle"/> in −π..π.</summary>
    public static double Wrap(double angle) => Math.IEEERemainder(angle, 2 * Math.PI);
}
