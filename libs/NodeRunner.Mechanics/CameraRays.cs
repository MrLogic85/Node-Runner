using NodeRunner.Domain;

namespace NodeRunner.Mechanics;

/// <summary>
/// The camera's pure math (#575, #604, #594, #578): its rays leave its beam's midpoint, evenly
/// spaced around its aim, the outer ones <see cref="SensorDef.Spread"/> apart, and reach
/// <see cref="SensorDef.Range"/>. The aim is an angle relative to its beam's direction (from its
/// first node to its second), so the camera turns with the beam. A placed camera looks level, at
/// the world's forward as built (<see cref="SensorDef.DefaultAim"/>, #622), so its default three
/// rays look forward-up, forward and forward-down. Rays are named symmetrically around the centre
/// ray, seen from the camera looking along them, and run left to right. Each ray reads how near the
/// ground is relative to its range: 0 when nothing is in range, rising linearly to 1 at contact,
/// and one more input says whether any ray sees the ground at all (#1032). Stateless and shared by
/// the sim, Build and the sensor picture, like <see cref="Accelerometer"/>. See
/// docs/CREATURE_MODEL.md.
/// </summary>
public static class CameraRays
{
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
    /// The angle of ray <paramref name="ray"/> (0 is the leftmost) from the aim, for
    /// <paramref name="rays"/> rays whose outer ones are <paramref name="spread"/> apart: evenly
    /// spaced and symmetric, so the centre ray is the aim. One ray has no spread.
    /// </summary>
    public static double RayAngle(int ray, int rays, double spread)
    {
        if (!SensorDef.IsRayCount(rays))
        {
            throw new ArgumentOutOfRangeException(nameof(rays), $"A camera has an odd ray count from 1 to {SensorDef.MaxRays}.");
        }

        if (ray < 0 || ray >= rays)
        {
            throw new ArgumentOutOfRangeException(nameof(ray), "Ray must be one of the camera's rays.");
        }

        if (!double.IsFinite(spread))
        {
            throw new ArgumentOutOfRangeException(nameof(spread), "Spread must be finite.");
        }

        return rays == 1 ? 0 : (ray - ((rays - 1) / 2.0)) * spread / (rays - 1);
    }

    /// <summary>
    /// The ray's target in its beam's local frame (+x along the beam): <paramref name="range"/> along
    /// <paramref name="aim"/> turned by <see cref="RayAngle"/>.
    /// </summary>
    public static Vector2D LocalRayTarget(int ray, double aim, int rays, double spread, double range)
    {
        if (!double.IsFinite(aim))
        {
            throw new ArgumentOutOfRangeException(nameof(aim), "Aim must be finite.");
        }

        if (!double.IsFinite(range) || range <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(range), "Range must be finite and positive.");
        }

        var angle = aim + RayAngle(ray, rays, spread);
        return new Vector2D(Math.Cos(angle) * range, Math.Sin(angle) * range);
    }

    /// <summary>
    /// <paramref name="camera"/>'s ray targets in its beam's local frame, left to right, with
    /// <paramref name="aim"/>: its own, or the default aim Build gives a Camera without one.
    /// </summary>
    public static Vector2D[] LocalRayTargets(SensorDef camera, double aim)
    {
        ArgumentNullException.ThrowIfNull(camera);
        if (camera is not { Kind: SensorKind.Camera, Rays: { } rays, Spread: { } spread, Range: { } range })
        {
            throw new ArgumentException("Only a Camera has rays.", nameof(camera));
        }

        var targets = new Vector2D[rays];
        for (var ray = 0; ray < rays; ray++)
        {
            targets[ray] = LocalRayTarget(ray, aim, rays, spread, range);
        }

        return targets;
    }

    /// <summary>
    /// One ray's brain input, its nearness relative to the camera's <paramref name="range"/>:
    /// <c>1 − distance / range</c> clamped to 0–1, and 0 with no hit, so nothing in view adds nothing
    /// to the brain's weighted sum.
    /// </summary>
    public static double Reading(double? hitDistance, double range) =>
        hitDistance is { } distance ? Math.Clamp(1 - (distance / range), 0, 1) : 0;

    /// <summary>
    /// Writes the camera's brain inputs from each ray's <paramref name="hitDistances"/>, left to right
    /// (null where it sees nothing), in <see cref="BrainPorts.CameraChannels"/> order: each ray's
    /// <see cref="Reading"/> within <paramref name="range"/>, then hit, 1 when any ray sees the ground
    /// and else 0. Hit tells far ground from none: a hit at the very end of a ray reads nearness 0 but
    /// hit 1 (#1032).
    /// </summary>
    public static void Read(ReadOnlySpan<double?> hitDistances, double range, Span<double> values)
    {
        var rays = hitDistances.Length;
        if (!SensorDef.IsRayCount(rays))
        {
            throw new ArgumentException($"A camera has an odd ray count from 1 to {SensorDef.MaxRays}.", nameof(hitDistances));
        }

        if (values.Length != rays + 1)
        {
            throw new ArgumentException("A camera writes one value per ray, then hit.", nameof(values));
        }

        var hit = false;
        for (var ray = 0; ray < rays; ray++)
        {
            values[ray] = Reading(hitDistances[ray], range);
            hit |= hitDistances[ray] is not null;
        }

        values[rays] = hit ? 1 : 0;
    }

    /// <summary><paramref name="angle"/> in −π..π.</summary>
    public static double Wrap(double angle) => Math.IEEERemainder(angle, 2 * Math.PI);
}
