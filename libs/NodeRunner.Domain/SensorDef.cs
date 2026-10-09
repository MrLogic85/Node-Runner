namespace NodeRunner.Domain;

/// <summary>
/// A sensor part on a beam, referenced by the beam's stable id. It measures its own beam at the
/// midpoint; a beam holds one sensor. A Camera also has an <see cref="Aim"/>, and its
/// <see cref="Rays"/>, <see cref="Spread"/> and <see cref="Range"/> (#578). See
/// docs/CREATURE_MODEL.md.
/// </summary>
public sealed record SensorDef
{
    /// <summary>A new Camera's <see cref="Rays"/>.</summary>
    public const int DefaultRays = 3;

    /// <summary>The most <see cref="Rays"/> a Camera has: its rays are named up to far left and far right.</summary>
    public const int MaxRays = 5;

    /// <summary>A new Camera's <see cref="Spread"/>: 90°, so three rays are 45° apart.</summary>
    public const double DefaultSpread = Math.PI / 2;

    /// <summary>A new Camera's <see cref="Range"/>, in world units: 2.2 m.</summary>
    public const double DefaultRange = 220;

    /// <summary>
    /// A Camera without <paramref name="rays"/>, <paramref name="spread"/> or <paramref name="range"/>
    /// gets the defaults, today's fan; other kinds have none.
    /// </summary>
    public SensorDef(
        int id,
        int beamId,
        SensorKind kind,
        string? name = null,
        double? aim = null,
        int? rays = null,
        double? spread = null,
        double? range = null)
    {
        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Sensor id must be positive.");
        }

        if (beamId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(beamId), "Beam id must be positive.");
        }

        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), "Sensor kind must be defined.");
        }

        var camera = kind == SensorKind.Camera;
        if (aim is { } angle && (!camera || !double.IsFinite(angle)))
        {
            throw new ArgumentOutOfRangeException(nameof(aim), "Only a Camera has an aim, and it must be finite.");
        }

        if (rays is { } count && (!camera || !IsRayCount(count)))
        {
            throw new ArgumentOutOfRangeException(nameof(rays), $"Only a Camera has rays: an odd count from 1 to {MaxRays}.");
        }

        if (spread is { } fan && (!camera || !double.IsFinite(fan) || fan < 0 || fan > Math.PI))
        {
            throw new ArgumentOutOfRangeException(nameof(spread), "Only a Camera has a spread, from 0 to a half turn.");
        }

        if (range is { } reach && (!camera || !double.IsFinite(reach) || reach <= 0))
        {
            throw new ArgumentOutOfRangeException(nameof(range), "Only a Camera has a range, finite and positive.");
        }

        Id = id;
        BeamId = beamId;
        Kind = kind;
        Name = name;
        Aim = aim;
        Rays = camera ? rays ?? DefaultRays : null;
        Spread = camera ? spread ?? DefaultSpread : null;
        Range = camera ? range ?? DefaultRange : null;
    }

    public int Id { get; }

    public int BeamId { get; }

    public SensorKind Kind { get; }

    public string? Name { get; }

    /// <summary>
    /// A Camera's aim (#594): the angle of its centre ray from its beam's direction, in radians
    /// (the ray math is <c>CameraRays</c> in NodeRunner.Mechanics). A <see cref="CreatureDef"/> gives a
    /// Camera without one <see cref="DefaultAim"/> from its beam's built pose; other kinds have none.
    /// </summary>
    public double? Aim { get; }

    /// <summary>
    /// A Camera's ray count (#578): 1, 3 or 5, odd so its centre ray is its aim. It decides the
    /// Camera's brain ports (<see cref="BrainPorts"/>). Null for other kinds.
    /// </summary>
    public int? Rays { get; }

    /// <summary>A Camera's angle between its outer rays, in radians, its rays evenly spaced around its aim. Null for other kinds.</summary>
    public double? Spread { get; }

    /// <summary>How far a Camera's rays reach, in world units; its readings are relative to it. Null for other kinds.</summary>
    public double? Range { get; }

    public SensorDef WithName(string? name) => new(Id, BeamId, Kind, name, Aim, Rays, Spread, Range);

    public SensorDef WithAim(double aim) => new(Id, BeamId, Kind, Name, aim, Rays, Spread, Range);

    public SensorDef WithRays(int rays) => new(Id, BeamId, Kind, Name, Aim, rays, Spread, Range);

    public SensorDef WithSpread(double spread) => new(Id, BeamId, Kind, Name, Aim, Rays, spread, Range);

    public SensorDef WithRange(double range) => new(Id, BeamId, Kind, Name, Aim, Rays, Spread, range);

    /// <summary>The same sensor on <paramref name="beamId"/> with <paramref name="aim"/>, keeping its other settings.</summary>
    public SensorDef OnBeam(int beamId, double? aim) => new(Id, beamId, Kind, Name, aim, Rays, Spread, Range);

    /// <summary>Whether a Camera can have <paramref name="rays"/> rays: an odd count from 1 to <see cref="MaxRays"/>.</summary>
    public static bool IsRayCount(int rays) => rays is >= 1 and <= MaxRays && rays % 2 == 1;

    /// <summary>
    /// A new Camera's aim (#622): level, at the world's forward (+x), on the beam from
    /// <paramref name="nodeA"/> to <paramref name="nodeB"/> as built, in −π..π.
    /// </summary>
    public static double DefaultAim(Vector2D nodeA, Vector2D nodeB) =>
        nodeA == nodeB ? 0 : Math.IEEERemainder(-Math.Atan2(nodeB.Y - nodeA.Y, nodeB.X - nodeA.X), 2 * Math.PI);
}
