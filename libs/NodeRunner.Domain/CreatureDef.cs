using System.Collections.ObjectModel;

namespace NodeRunner.Domain;

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

        if (nodes.Count == 0)
        {
            throw new ArgumentException("A creature needs at least one node.", nameof(nodes));
        }

        foreach (var beam in beams)
        {
            ValidateNodeIndex(beam.NodeA, nodes.Count);
            ValidateNodeIndex(beam.NodeB, nodes.Count);
        }

        foreach (var core in cores)
        {
            ValidateNodeIndex(core.NodeIndex, nodes.Count);
        }

        var beamCountPerNode = new int[nodes.Count];
        foreach (var beam in beams)
        {
            beamCountPerNode[beam.NodeA]++;
            beamCountPerNode[beam.NodeB]++;
        }

        for (var i = 0; i < beamCountPerNode.Length; i++)
        {
            if (beamCountPerNode[i] == 0)
            {
                throw new ArgumentException(
                    $"Node {i} has no beams attached. A node with no beams is just a loose point and cannot be simulated.",
                    nameof(beams));
            }
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
