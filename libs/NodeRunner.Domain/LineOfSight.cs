namespace NodeRunner.Domain;

/// <summary>
/// The LOS sensor's pure math (#575): three fixed rays from its beam's midpoint, aimed down,
/// forward and forward-down in the world as built and turning with the beam after that. Each ray
/// reads 1 when nothing is in range and 0 at contact. Stateless and shared by the sim and the
/// sensor picture, like <see cref="Accelerometer"/>. See docs/CREATURE_MODEL.md.
/// </summary>
public static class LineOfSight
{
    public const int RayCount = 3;

    public const double RayLength = 220;

    public static IReadOnlyList<string> RayNames { get; } = ["down", "forward", "forward-down"];

    // World directions as built; y grows downward, forward is +x.
    private static readonly Vector2D[] _builtDirections =
    [
        new(0, 1),
        new(1, 0),
        new(Math.Sqrt(0.5), Math.Sqrt(0.5)),
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

    /// <summary>One ray's brain input: the hit distance over <see cref="RayLength"/>, or 1 with no hit.</summary>
    public static double Reading(double? hitDistance) =>
        hitDistance is { } distance ? Math.Clamp(distance / RayLength, 0, 1) : 1;
}
