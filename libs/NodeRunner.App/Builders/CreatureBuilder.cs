using NodeRunner.App.Lifecycle;
using NodeRunner.Domain;

namespace NodeRunner.App.Builders;

/// <summary>
/// Mutable, in-progress creature anatomy driven by Build-mode UI
/// (0.3.0). Add/move/remove nodes, beams, and cores here; <see cref="Build"/>
/// returns the drawing as an immutable <see cref="CreatureDef"/> for saving, and
/// <see cref="TryBuild"/> returns it only once it can be simulated. See
/// docs/CREATURE_MODEL.md for the vocabulary and docs/ROADMAP.md 0.3.0 for
/// the feature this supports.
///
/// Lives in `NodeRunner.App`, not `NodeRunner.Domain`: this is mutable
/// business logic (add/remove/cascade), which
/// `libs/NodeRunner.Domain/AGENTS.md` explicitly reserves for
/// <c>MotorTopology</c> only.
/// </summary>
public sealed class CreatureBuilder
{
    private readonly List<NodeDef> _nodes = [];
    private readonly List<BeamDef> _beams = [];
    private readonly List<CoreDef> _cores = [];
    private int _nextPartId = 1;

    public CreatureBuilder()
    {
    }

    public CreatureBuilder(CreatureDef creature)
    {
        ArgumentNullException.ThrowIfNull(creature);
        _nodes.AddRange(creature.Nodes);
        _beams.AddRange(creature.Beams);
        _cores.AddRange(creature.Cores);
        _nextPartId = creature.NextPartId;
    }

    public IReadOnlyList<NodeDef> Nodes => _nodes;

    public IReadOnlyList<BeamDef> Beams => _beams;

    public IReadOnlyList<CoreDef> Cores => _cores;

    public int NextPartId => _nextPartId;

    /// <summary>Adds a node and returns its id.</summary>
    public int AddNode(Vector2D position, double radius)
    {
        var id = AllocatePartId();
        _nodes.Add(new NodeDef(id, position, radius));
        return id;
    }

    /// <summary>Moves an existing node to a new position, keeping its radius.</summary>
    public void MoveNode(int nodeId, Vector2D position)
    {
        var nodeIndex = NodeIndexOf(nodeId);
        var node = _nodes[nodeIndex];
        _nodes[nodeIndex] = new NodeDef(node.Id, position, node.Radius, node.Name);
    }

    /// <summary>
    /// Removes a node, cascading to every beam and core that referenced it.
    /// </summary>
    public void RemoveNode(int nodeId)
    {
        var nodeIndex = NodeIndexOf(nodeId);

        _beams.RemoveAll(beam => beam.NodeA == nodeId || beam.NodeB == nodeId);
        _cores.RemoveAll(core => core.NodeId == nodeId);
        _nodes.RemoveAt(nodeIndex);
    }

    /// <summary>
    /// Adds a beam between two distinct, existing nodes and returns its
    /// id. Throws if either node id is invalid, the nodes are the
    /// same, or a beam between them already exists.
    /// </summary>
    public int AddBeam(int nodeIdA, int nodeIdB)
    {
        ValidateNodeId(nodeIdA);
        ValidateNodeId(nodeIdB);

        if (nodeIdA == nodeIdB)
        {
            throw new ArgumentException("A beam must connect two different nodes.");
        }

        if (_beams.Any(beam => IsSamePair(beam, nodeIdA, nodeIdB)))
        {
            throw new ArgumentException($"A beam already connects node {nodeIdA} and node {nodeIdB}.");
        }

        var id = AllocatePartId();
        _beams.Add(new BeamDef(id, nodeIdA, nodeIdB));
        return id;
    }

    /// <summary>Whether <see cref="AddBeam"/> would accept this pair: two distinct, existing nodes not yet joined.</summary>
    public bool CanAddBeam(int nodeIdA, int nodeIdB) =>
        HasNode(nodeIdA)
        && HasNode(nodeIdB)
        && nodeIdA != nodeIdB
        && !_beams.Any(beam => IsSamePair(beam, nodeIdA, nodeIdB));

    /// <summary>Removes a beam by id.</summary>
    public void RemoveBeam(int beamId)
    {
        var beamIndex = BeamIndexOf(beamId);
        _beams.RemoveAt(beamIndex);
    }

    /// <summary>Adds a core mounted on an existing node and returns its id.</summary>
    public int AddCore(int nodeId)
    {
        ValidateNodeId(nodeId);
        var id = AllocatePartId();
        _cores.Add(new CoreDef(id, nodeId));
        return id;
    }

    /// <summary>Removes a core by id.</summary>
    public void RemoveCore(int coreId)
    {
        var coreIndex = CoreIndexOf(coreId);
        _cores.RemoveAt(coreIndex);
    }

    public void Rename(int partId, string? name)
    {
        if (partId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(partId), "Part id must be positive.");
        }

        var nodeIndex = _nodes.FindIndex(node => node.Id == partId);
        if (nodeIndex >= 0)
        {
            var node = _nodes[nodeIndex];
            _nodes[nodeIndex] = new NodeDef(node.Id, node.Position, node.Radius, name);
            return;
        }

        var beamIndex = _beams.FindIndex(beam => beam.Id == partId);
        if (beamIndex >= 0)
        {
            var beam = _beams[beamIndex];
            _beams[beamIndex] = new BeamDef(beam.Id, beam.NodeA, beam.NodeB, name);
            return;
        }

        var coreIndex = _cores.FindIndex(core => core.Id == partId);
        if (coreIndex >= 0)
        {
            var core = _cores[coreIndex];
            _cores[coreIndex] = new CoreDef(core.Id, core.NodeId, name);
            return;
        }

        throw new ArgumentOutOfRangeException(nameof(partId), "Part id must point to an existing part.");
    }

    /// <summary>The current drawing, finished or not: what a saved Creation stores.</summary>
    public CreatureDef Build() => new(_nodes, _beams, _cores, _nextPartId);

    /// <summary>The current drawing if it can be simulated, else the player-facing problems that stop it.</summary>
    public bool TryBuild(out CreatureDef? creature, out IReadOnlyList<string> errors)
    {
        var built = Build();
        errors = CreatureReadiness.Problems(built);
        creature = errors.Count == 0 ? built : null;
        return creature is not null;
    }

    public int NodeIndexOf(int nodeId)
    {
        var index = _nodes.FindIndex(node => node.Id == nodeId);
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nodeId), "Node id must point to an existing node.");
        }

        return index;
    }

    public int BeamIndexOf(int beamId)
    {
        var index = _beams.FindIndex(beam => beam.Id == beamId);
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(beamId), "Beam id must point to an existing beam.");
        }

        return index;
    }

    public int CoreIndexOf(int coreId)
    {
        var index = _cores.FindIndex(core => core.Id == coreId);
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(coreId), "Core id must point to an existing core.");
        }

        return index;
    }

    private static bool IsSamePair(BeamDef beam, int nodeA, int nodeB)
    {
        return (beam.NodeA == nodeA && beam.NodeB == nodeB) || (beam.NodeA == nodeB && beam.NodeB == nodeA);
    }

    private bool HasNode(int nodeId) => _nodes.Any(node => node.Id == nodeId);

    private void ValidateNodeId(int nodeId)
    {
        if (!HasNode(nodeId))
        {
            throw new ArgumentOutOfRangeException(nameof(nodeId), "Node id must point to an existing node.");
        }
    }

    private int AllocatePartId() => _nextPartId++;
}
