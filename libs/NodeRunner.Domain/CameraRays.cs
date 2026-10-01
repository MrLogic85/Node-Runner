namespace NodeRunner.Domain;

/// <summary>
/// The camera's pure math (#575, #604): three fixed rays from its beam's midpoint, aimed forward,
/// forward-down and down in the world as built and turning with the beam after that. Rays are named
/// symmetrically around the centre ray, seen from the camera looking along them, and run left to
/// right. Each ray reads how near the ground is: 0 when nothing is in range, rising linearly to 1
/// at contact. Stateless and shared by the sim and the sensor picture, like
/// <see cref="Accelerometer"/>. See docs/CREATURE_MODEL.md.
/// </summary>
public static class CameraRays
{
    public const int RayCount = 3;

    public const double RayLength = 220;

    public static IReadOnlyList<string> RayNames { get; } = ["left 1", "centre", "right 1"];

    // World directions as built, in RayNames order; y grows downward, forward is +x.
    private static readonly Vector2D[] _builtDirections =
    [
        new(1, 0),
        new(Math.Sqrt(0.5), Math.Sqrt(0.5)),
        new(0, 1),
    ];

    /// <summary>
    /// The ray's target in its beam's local frame, so that at <paramref name="builtRotation"/> it points
    /// along its built world direction, <see cref="RayLength"/> long.
    /// </summary>
    public static Vector2D LocalRayTarget(int ray, double builtRotation)
    {
        if (ray is < 0 or >= RayCount)
        {
            throw new ArgumentOutOfRangeException(nameof(ray), "Ray must be 0, 1 or 2.");
        }

        if (!double.IsFinite(builtRotation))
        {
            throw new ArgumentOutOfRangeException(nameof(builtRotation), "Built rotation must be finite.");
        }

        var direction = _builtDirections[ray];
        var cos = Math.Cos(builtRotation);
        var sin = Math.Sin(builtRotation);
        return new Vector2D(
            ((direction.X * cos) + (direction.Y * sin)) * RayLength,
            ((-direction.X * sin) + (direction.Y * cos)) * RayLength);
    }

    /// <summary>
    /// One ray's brain input, its nearness: <c>1 − distance / <see cref="RayLength"/></c> clamped to
    /// 0–1, and 0 with no hit, so nothing in view adds nothing to the brain's weighted sum.
    /// </summary>
    public static double Reading(double? hitDistance) =>
        hitDistance is { } distance ? Math.Clamp(1 - (distance / RayLength), 0, 1) : 0;
}
