using System.Collections.ObjectModel;

namespace NodeRunner.Domain;

/// <summary>
/// A drawn creature: nodes, the beams between them and the cores on them. Any drawing is a valid
/// <see cref="CreatureDef"/>, so an unfinished one can be saved; only its part indices must point
/// at existing nodes. Whether it can be simulated and trained is checked before training
/// (<c>CreatureReadiness</c> in <c>NodeRunner.App</c>).
/// </summary>
public sealed record CreatureDef
{
    private readonly ReadOnlyCollection<NodeDef> _nodes;
    private readonly ReadOnlyCollection<BeamDef> _beams;
    private readonly ReadOnlyCollection<CoreDef> _cores;

    public CreatureDef(IReadOnlyList<NodeDef> nodes, IReadOnlyList<BeamDef> beams, IReadOnlyList<CoreDef> cores)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(beams);
        ArgumentNullException.ThrowIfNull(cores);

        foreach (var beam in beams)
        {
            ValidateNodeIndex(beam.NodeA, nodes.Count);
            ValidateNodeIndex(beam.NodeB, nodes.Count);
        }

        foreach (var core in cores)
        {
            ValidateNodeIndex(core.NodeIndex, nodes.Count);
        }

        _nodes = Array.AsReadOnly(nodes.ToArray());
        _beams = Array.AsReadOnly(beams.ToArray());
        _cores = Array.AsReadOnly(cores.ToArray());
    }

    public IReadOnlyList<NodeDef> Nodes => _nodes;

    public IReadOnlyList<BeamDef> Beams => _beams;

    public IReadOnlyList<CoreDef> Cores => _cores;

    private static void ValidateNodeIndex(int index, int nodeCount)
    {
        if (index >= nodeCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "Node index must point to an existing node.");
        }
    }
}
