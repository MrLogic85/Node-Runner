namespace NodeRunner.Domain;

/// <summary>
/// A Memory cell: one number, <c>m</c>, the network writes one tick and reads back the next. It sits on a
/// beam as a beam sensor does but reads nothing from it, and has no settings. Its ports are
/// <see cref="NetworkPorts.MemoryCellInputs"/> and <see cref="NetworkPorts.MemoryCellOutputs"/>; <c>m</c> itself is never saved
/// (<c>NodeRunner.Mechanics.MemoryCell</c>). See docs/CREATURE_MODEL.md → "Memory cell".
/// </summary>
public sealed record MemoryCellDef : IBeamPartDef
{
    public MemoryCellDef(int id, int beamId, string? name = null)
    {
        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Memory cell id must be positive.");
        }

        if (beamId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(beamId), "Beam id must be positive.");
        }

        Id = id;
        BeamId = beamId;
        Name = name;
    }

    public int Id { get; }

    public int BeamId { get; }

    public string? Name { get; }

    PartKind IPartDef.PartKind => PartKind.MemoryCell;

    IPartDef IPartDef.WithName(string? name) => WithName(name);

    public MemoryCellDef WithName(string? name) => new(Id, BeamId, name);
}
