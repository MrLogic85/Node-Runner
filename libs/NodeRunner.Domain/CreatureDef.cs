using System.Collections.ObjectModel;

namespace NodeRunner.Domain;

/// <summary>
/// A drawn creature: nodes, the beams between them and the cores on them. Any drawing is a valid
/// <see cref="CreatureDef"/>, so an unfinished one can be saved; only its part references must point
/// at existing nodes. Whether it can be simulated and trained is checked before training
/// (<c>CreatureReadiness</c> in <c>NodeRunner.App</c>).
/// </summary>
public sealed record CreatureDef
{
    private readonly ReadOnlyCollection<NodeDef> _nodes;
    private readonly ReadOnlyCollection<BeamDef> _beams;
    private readonly ReadOnlyCollection<CoreDef> _cores;
    private readonly Dictionary<int, int> _nodeIndexById;
    private readonly Dictionary<int, int> _beamIndexById;
    private readonly Dictionary<int, int> _coreIndexById;

    public CreatureDef(IReadOnlyList<NodeDef> nodes, IReadOnlyList<BeamDef> beams, IReadOnlyList<CoreDef> cores)
        : this(nodes, beams, cores, nextPartId: null)
    {
    }

    [System.Text.Json.Serialization.JsonConstructor]
    public CreatureDef(IReadOnlyList<NodeDef> nodes, IReadOnlyList<BeamDef> beams, IReadOnlyList<CoreDef> cores, int nextPartId)
        : this(nodes, beams, cores, (int?)nextPartId)
    {
    }

    private CreatureDef(IReadOnlyList<NodeDef> nodes, IReadOnlyList<BeamDef> beams, IReadOnlyList<CoreDef> cores, int? nextPartId)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(beams);
        ArgumentNullException.ThrowIfNull(cores);

        var maxId = 0;
        var ids = new HashSet<int>();
        ValidatePartIds(nodes.Select(node => node.Id), ids, ref maxId);
        ValidatePartIds(beams.Select(beam => beam.Id), ids, ref maxId);
        ValidatePartIds(cores.Select(core => core.Id), ids, ref maxId);

        var resolvedNextPartId = nextPartId ?? maxId + 1;
        if (resolvedNextPartId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nextPartId), "Next part id must be positive.");
        }

        if (maxId >= resolvedNextPartId)
        {
            throw new ArgumentOutOfRangeException(nameof(nextPartId), "Every part id must be less than the next part id.");
        }

        var nodeIds = nodes.Select(node => node.Id).ToHashSet();
        foreach (var beam in beams)
        {
            ValidateNodeId(beam.NodeA, nodeIds);
            ValidateNodeId(beam.NodeB, nodeIds);
        }

        foreach (var core in cores)
        {
            ValidateNodeId(core.NodeId, nodeIds);
        }

        _nodes = Array.AsReadOnly(nodes.ToArray());
        _beams = Array.AsReadOnly(beams.ToArray());
        _cores = Array.AsReadOnly(cores.ToArray());
        NextPartId = resolvedNextPartId;
        _nodeIndexById = BuildIndex(_nodes, node => node.Id);
        _beamIndexById = BuildIndex(_beams, beam => beam.Id);
        _coreIndexById = BuildIndex(_cores, core => core.Id);
    }

    public IReadOnlyList<NodeDef> Nodes => _nodes;

    public IReadOnlyList<BeamDef> Beams => _beams;

    public IReadOnlyList<CoreDef> Cores => _cores;

    public int NextPartId { get; }

    public int NodeIndexOf(int nodeId) => IndexOf(_nodeIndexById, nodeId, "Node id must point to an existing node.");

    public int BeamIndexOf(int beamId) => IndexOf(_beamIndexById, beamId, "Beam id must point to an existing beam.");

    public int CoreIndexOf(int coreId) => IndexOf(_coreIndexById, coreId, "Core id must point to an existing core.");

    private static void ValidatePartIds(IEnumerable<int> partIds, HashSet<int> ids, ref int maxId)
    {
        foreach (var id in partIds)
        {
            if (!ids.Add(id))
            {
                throw new ArgumentException($"Part id {id} is used more than once.");
            }

            maxId = Math.Max(maxId, id);
        }
    }

    private static void ValidateNodeId(int nodeId, HashSet<int> nodeIds)
    {
        if (!nodeIds.Contains(nodeId))
        {
            throw new ArgumentOutOfRangeException(nameof(nodeId), "Node id must point to an existing node.");
        }
    }

    private static Dictionary<int, int> BuildIndex<T>(IReadOnlyList<T> items, Func<T, int> idOf)
    {
        var index = new Dictionary<int, int>(items.Count);
        for (var i = 0; i < items.Count; i++)
        {
            index[idOf(items[i])] = i;
        }

        return index;
    }

    private static int IndexOf(Dictionary<int, int> indexById, int id, string message)
    {
        if (!indexById.TryGetValue(id, out var index))
        {
            throw new ArgumentOutOfRangeException(nameof(id), message);
        }

        return index;
    }
}
