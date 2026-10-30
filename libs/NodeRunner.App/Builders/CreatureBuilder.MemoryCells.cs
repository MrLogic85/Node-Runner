using System.Diagnostics.CodeAnalysis;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Builders;

/// <summary>
/// The Memory cells: network parts on a beam, placed and removed as a beam sensor is. A beam holds one
/// part, a sensor or a Memory cell (<see cref="CanMountOnBeam"/>).
/// </summary>
public sealed partial class CreatureBuilder
{
    /// <summary>Why a Memory cell dropped on anything but a beam is refused.</summary>
    public static UiText MemoryCellsGoOnABeamReason { get; } = UiText.Plain("Memory cells go on a beam");

    private readonly List<MemoryCellDef> _memoryCells = [];

    public IReadOnlyList<MemoryCellDef> MemoryCells => _memoryCells;

    /// <summary>Every part on a beam, as <see cref="CreatureDef.BeamParts"/>: the sensors, then the Memory cells.</summary>
    public IReadOnlyList<IBeamPartDef> BeamParts => [.. _sensors, .. _memoryCells];

    /// <summary>Whether a Memory cell can sit on <paramref name="beamId"/>; if not, <paramref name="reason"/> says why.</summary>
    public bool CanAddMemoryCell(int beamId, [NotNullWhen(false)] out UiText? reason)
    {
        if (!HasBeam(beamId))
        {
            reason = MemoryCellsGoOnABeamReason;
            return false;
        }

        return CanMountOnBeam(beamId, movingPartId: null, out reason);
    }

    /// <summary>Adds a Memory cell on a beam and returns its id; see <see cref="CanAddMemoryCell"/>.</summary>
    public int AddMemoryCell(int beamId)
    {
        if (!CanAddMemoryCell(beamId, out var reason))
        {
            throw new ArgumentException(reason.Message);
        }

        var id = AllocatePartId();
        _memoryCells.Add(new MemoryCellDef(id, beamId));
        return id;
    }

    public int MemoryCellIndexOf(int memoryCellId) => IndexOf(_memoryCells, memoryCellId, nameof(memoryCellId), "Memory cell id must point to an existing memory cell.");
}
