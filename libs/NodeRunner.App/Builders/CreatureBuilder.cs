using NodeRunner.App.Lifecycle;
using NodeRunner.Domain;

namespace NodeRunner.App.Builders;

/// <summary>
/// Mutable, in-progress creature anatomy driven by construction-mode UI
/// (0.3.0). Add/move/remove nodes, beams, and cores here; <see cref="Build"/>
/// returns the drawing as an immutable <see cref="CreatureDef"/> for saving, and
/// <see cref="TryBuild"/> returns it only once it can be simulated. See
/// docs/CREATURE_MODEL.md for the vocabulary and docs/ROADMAP.md 0.3.0 for
/// the feature this supports.
///
/// Lives in `NodeRunner.App`, not `NodeRunner.Domain`: this is mutable
/// business logic (add/remove/cascade/reindex), which
/// `libs/NodeRunner.Domain/AGENTS.md` explicitly reserves for
/// <c>MotorTopology</c> only.
/// </summary>
public sealed class CreatureBuilder
{
    private readonly List<NodeDef> _nodes = [];
    private readonly List<BeamDef> _beams = [];
    private readonly List<CoreDef> _cores = [];

    public CreatureBuilder()
    {
    }

    public CreatureBuilder(CreatureDef creature)
    {
        ArgumentNullException.ThrowIfNull(creature);
        _nodes.AddRange(creature.Nodes);
        _beams.AddRange(creature.Beams);
        _cores.AddRange(creature.Cores);
    }

    public IReadOnlyList<NodeDef> Nodes => _nodes;

    public IReadOnlyList<BeamDef> Beams => _beams;

    public IReadOnlyList<CoreDef> Cores => _cores;

    /// <summary>Adds a node and returns its index.</summary>
    public int AddNode(Vector2D position, double radius)
    {
        _nodes.Add(new NodeDef(position, radius));
        return _nodes.Count - 1;
    }

    /// <summary>Moves an existing node to a new position, keeping its radius.</summary>
    public void MoveNode(int nodeIndex, Vector2D position)
    {
        ValidateNodeIndex(nodeIndex);
        _nodes[nodeIndex] = new NodeDef(position, _nodes[nodeIndex].Radius);
    }

    /// <summary>
    /// Removes a node, cascading to every beam and core that referenced it,
    /// and reindexes the remaining nodes' references so they stay valid.
    /// </summary>
    public void RemoveNode(int nodeIndex)
    {
        ValidateNodeIndex(nodeIndex);

        _beams.RemoveAll(beam => beam.NodeA == nodeIndex || beam.NodeB == nodeIndex);
        _cores.RemoveAll(core => core.NodeIndex == nodeIndex);
        _nodes.RemoveAt(nodeIndex);

        for (var i = 0; i < _beams.Count; i++)
        {
            _beams[i] = new BeamDef(ReindexAfterRemoval(_beams[i].NodeA, nodeIndex), ReindexAfterRemoval(_beams[i].NodeB, nodeIndex));
        }

        for (var i = 0; i < _cores.Count; i++)
        {
            _cores[i] = new CoreDef(ReindexAfterRemoval(_cores[i].NodeIndex, nodeIndex));
        }
    }

    /// <summary>
    /// Adds a beam between two distinct, existing nodes and returns its
    /// index. Throws if either node index is invalid, the nodes are the
    /// same, or a beam between them already exists.
    /// </summary>
    public int AddBeam(int nodeA, int nodeB)
    {
        ValidateNodeIndex(nodeA);
        ValidateNodeIndex(nodeB);

        if (nodeA == nodeB)
        {
            throw new ArgumentException("A beam must connect two different nodes.");
        }

        if (_beams.Any(beam => IsSamePair(beam, nodeA, nodeB)))
        {
            throw new ArgumentException($"A beam already connects node {nodeA} and node {nodeB}.");
        }

        _beams.Add(new BeamDef(nodeA, nodeB));
        return _beams.Count - 1;
    }

    /// <summary>Whether <see cref="AddBeam"/> would accept this pair: two distinct, existing nodes not yet joined.</summary>
    public bool CanAddBeam(int nodeA, int nodeB) =>
        nodeA >= 0 && nodeA < _nodes.Count
        && nodeB >= 0 && nodeB < _nodes.Count
        && nodeA != nodeB
        && !_beams.Any(beam => IsSamePair(beam, nodeA, nodeB));

    /// <summary>Removes a beam by index.</summary>
    public void RemoveBeam(int beamIndex)
    {
        ValidateBeamIndex(beamIndex);
        _beams.RemoveAt(beamIndex);
    }

    /// <summary>Adds a core mounted on an existing node and returns its index.</summary>
    public int AddCore(int nodeIndex)
    {
        ValidateNodeIndex(nodeIndex);
        _cores.Add(new CoreDef(nodeIndex));
        return _cores.Count - 1;
    }

    /// <summary>Removes a core by index.</summary>
    public void RemoveCore(int coreIndex)
    {
        ValidateCoreIndex(coreIndex);
        _cores.RemoveAt(coreIndex);
    }

    /// <summary>The current drawing, finished or not: what a saved Creation stores.</summary>
    public CreatureDef Build() => new(_nodes, _beams, _cores);

    /// <summary>The current drawing if it can be simulated, else the player-facing problems that stop it.</summary>
    public bool TryBuild(out CreatureDef? creature, out IReadOnlyList<string> errors)
    {
        var built = Build();
        errors = CreatureReadiness.Problems(built);
        creature = errors.Count == 0 ? built : null;
        return creature is not null;
    }

    private static bool IsSamePair(BeamDef beam, int nodeA, int nodeB)
    {
        return (beam.NodeA == nodeA && beam.NodeB == nodeB) || (beam.NodeA == nodeB && beam.NodeB == nodeA);
    }

    private static int ReindexAfterRemoval(int nodeIndex, int removedIndex)
    {
        return nodeIndex > removedIndex ? nodeIndex - 1 : nodeIndex;
    }

    private void ValidateNodeIndex(int nodeIndex)
    {
        if (nodeIndex < 0 || nodeIndex >= _nodes.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(nodeIndex), "Node index must point to an existing node.");
        }
    }

    private void ValidateBeamIndex(int beamIndex)
    {
        if (beamIndex < 0 || beamIndex >= _beams.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(beamIndex), "Beam index must point to an existing beam.");
        }
    }

    private void ValidateCoreIndex(int coreIndex)
    {
        if (coreIndex < 0 || coreIndex >= _cores.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(coreIndex), "Core index must point to an existing core.");
        }
    }
}
