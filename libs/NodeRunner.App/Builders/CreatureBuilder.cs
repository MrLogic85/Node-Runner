using NodeRunner.Domain;

namespace NodeRunner.App.Builders;

/// <summary>
/// Mutable, in-progress creature anatomy driven by construction-mode UI
/// (0.3.0). Add/move/remove nodes, beams, and cores here; call
/// <see cref="TryBuild"/> to attempt converting the current state into an
/// immutable <see cref="CreatureDef"/> once the user is done editing. See
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

    /// <summary>
    /// Attempts to convert the current builder state into an immutable
    /// <see cref="CreatureDef"/>. Returns false with understandable,
    /// beginner-facing error messages (not raw exception text) instead of
    /// throwing when the current anatomy is not yet valid to simulate.
    /// </summary>
    public bool TryBuild(out CreatureDef? creature, out IReadOnlyList<string> errors)
    {
        var problems = new List<string>();

        if (_nodes.Count == 0)
        {
            problems.Add("Add at least one node before running the creature.");
        }

        for (var i = 0; i < _nodes.Count; i++)
        {
            var beamCount = _beams.Count(beam => beam.NodeA == i || beam.NodeB == i);
            if (beamCount == 0)
            {
                problems.Add($"Node {i} has no beams attached. Connect it with a beam or remove it.");
            }
        }

        foreach (var beam in _beams)
        {
            if (_nodes[beam.NodeA].Position == _nodes[beam.NodeB].Position)
            {
                problems.Add($"The beam between node {beam.NodeA} and node {beam.NodeB} has zero length. Move one of the nodes apart.");
            }
        }

        if (problems.Count > 0)
        {
            creature = null;
            errors = problems;
            return false;
        }

        creature = new CreatureDef(_nodes, _beams, _cores);
        errors = [];
        return true;
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
