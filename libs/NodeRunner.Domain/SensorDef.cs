namespace NodeRunner.Domain;

/// <summary>
/// A sensor part on a beam, referenced by the beam's stable id. It measures its own beam at the
/// midpoint; a beam holds one sensor. A Camera also has an <see cref="Aim"/>. See
/// docs/CREATURE_MODEL.md.
/// </summary>
public sealed record SensorDef
{
    public SensorDef(int id, int beamId, SensorKind kind, string? name = null, double? aim = null)
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

        if (aim is { } angle && (kind != SensorKind.Camera || !double.IsFinite(angle)))
        {
            throw new ArgumentOutOfRangeException(nameof(aim), "Only a Camera has an aim, and it must be finite.");
        }

        Id = id;
        BeamId = beamId;
        Kind = kind;
        Name = name;
        Aim = aim;
    }

    public int Id { get; }

    public int BeamId { get; }

    public SensorKind Kind { get; }

    public string? Name { get; }

    /// <summary>
    /// A Camera's aim (#594): the angle of its centre ray from its beam's direction, in radians
    /// (see <see cref="CameraRays"/>). A <see cref="CreatureDef"/> gives a Camera without one
    /// <see cref="CameraRays.DefaultAim"/> from its beam's built pose; other kinds have none.
    /// </summary>
    public double? Aim { get; }

    public SensorDef WithBeam(int beamId) => new(Id, beamId, Kind, Name, Aim);

    public SensorDef WithName(string? name) => new(Id, BeamId, Kind, name, Aim);

    public SensorDef WithAim(double aim) => new(Id, BeamId, Kind, Name, aim);
}
