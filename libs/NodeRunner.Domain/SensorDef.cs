namespace NodeRunner.Domain;

/// <summary>
/// A sensor part on a beam, referenced by the beam's stable id. It measures its own beam at the
/// midpoint; a beam holds one sensor of each <see cref="SensorKind"/>. See docs/CREATURE_MODEL.md.
/// </summary>
public sealed record SensorDef
{
    public SensorDef(int id, int beamId, SensorKind kind, string? name = null)
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

        Id = id;
        BeamId = beamId;
        Kind = kind;
        Name = name;
    }

    public int Id { get; }

    public int BeamId { get; }

    public SensorKind Kind { get; }

    public string? Name { get; }
}
