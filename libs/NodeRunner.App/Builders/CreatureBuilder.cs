using System.Diagnostics.CodeAnalysis;
using NodeRunner.App.Lifecycle;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Builders;

/// <summary>
/// Mutable, in-progress creature anatomy driven by Build-mode UI
/// (0.3.0). Add/move/remove nodes, beams, sensors, Pistons and Springs here; <see cref="Build"/>
/// returns the drawing as an immutable <see cref="CreatureDef"/> for saving, and
/// <see cref="TryBuild"/> returns it only once it can be simulated. See
/// docs/CREATURE_MODEL.md for the vocabulary and docs/ROADMAP.md 0.3.0 for
/// the feature this supports.
///
/// Lives in `NodeRunner.App`, not `NodeRunner.Domain`: this is mutable
/// business logic (add/remove/cascade), which
/// `libs/NodeRunner.Domain/AGENTS.md` explicitly reserves for
/// stateless derivations such as <c>RigidTriangles</c> only.
/// </summary>
public sealed class CreatureBuilder
{
    /// <summary>Why a sensor cannot go on a beam that already has one.</summary>
    public static UiText OneSensorPerBeamReason { get; } = UiText.Plain("One sensor per beam");

    /// <summary>Why a Piston or a Spring cannot join two nodes a beam already holds rigid (#451, #453).</summary>
    public static UiText BeamJoinsTheseNodesReason { get; } = UiText.Plain("A beam already joins these nodes");

    /// <summary>Why a beam or another link cannot join two nodes a Piston already links.</summary>
    public static UiText PistonJoinsTheseNodesReason { get; } = UiText.Plain("These nodes already have a piston");

    /// <summary>Why a beam or another link cannot join two nodes a Spring already links (#453).</summary>
    public static UiText SpringJoinsTheseNodesReason { get; } = UiText.Plain("These nodes already have a spring");

    private readonly List<NodeDef> _nodes = [];
    private readonly List<BeamDef> _beams = [];
    private readonly List<SensorDef> _sensors = [];
    private readonly List<PistonDef> _pistons = [];
    private readonly List<SpringDef> _springs = [];
    private int _nextPartId = 1;

    public CreatureBuilder()
    {
    }

    public CreatureBuilder(CreatureDef creature)
    {
        ArgumentNullException.ThrowIfNull(creature);
        _nodes.AddRange(creature.Nodes);
        _beams.AddRange(creature.Beams);
        _sensors.AddRange(creature.Sensors);
        _pistons.AddRange(creature.Pistons);
        _springs.AddRange(creature.Springs);
        _nextPartId = creature.NextPartId;
    }

    public IReadOnlyList<NodeDef> Nodes => _nodes;

    public IReadOnlyList<BeamDef> Beams => _beams;

    public IReadOnlyList<SensorDef> Sensors => _sensors;

    public IReadOnlyList<PistonDef> Pistons => _pistons;

    public IReadOnlyList<SpringDef> Springs => _springs;

    public int NextPartId => _nextPartId;

    /// <summary>Adds a node and returns its id.</summary>
    public int AddNode(Vector2D position)
    {
        var id = AllocatePartId();
        _nodes.Add(new NodeDef(id, position));
        return id;
    }

    /// <summary>Moves an existing node to a new position, keeping its name.</summary>
    public void MoveNode(int nodeId, Vector2D position)
    {
        var nodeIndex = NodeIndexOf(nodeId);
        var node = _nodes[nodeIndex];
        _nodes[nodeIndex] = new NodeDef(node.Id, position, node.Name);
    }

    /// <summary>
    /// Removes a node, cascading to every beam, sensor, Piston and Spring that referenced it.
    /// </summary>
    public void RemoveNode(int nodeId)
    {
        var nodeIndex = NodeIndexOf(nodeId);
        var removedBeamIds = _beams
            .Where(beam => beam.NodeA == nodeId || beam.NodeB == nodeId)
            .Select(beam => beam.Id)
            .ToHashSet();

        _beams.RemoveAll(beam => removedBeamIds.Contains(beam.Id));
        _sensors.RemoveAll(sensor => removedBeamIds.Contains(sensor.BeamId));
        _pistons.RemoveAll(piston => piston.NodeA == nodeId || piston.NodeB == nodeId);
        _springs.RemoveAll(spring => spring.NodeA == nodeId || spring.NodeB == nodeId);
        _nodes.RemoveAt(nodeIndex);
    }

    /// <summary>
    /// Adds a beam between two distinct, existing nodes and returns its
    /// id. Throws if either node id is invalid, the nodes are the
    /// same, or a beam or link between them already exists.
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

        if (LinkReason(nodeIdA, nodeIdB) is { } linked)
        {
            throw new ArgumentException(linked.Message);
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
        && !_beams.Any(beam => IsSamePair(beam, nodeIdA, nodeIdB))
        && LinkReason(nodeIdA, nodeIdB) is null;

    /// <summary>
    /// Whether <see cref="AddPiston"/> would accept this pair (#451): two distinct, existing nodes
    /// with no beam between them, which would hold them rigid, and no link yet; if not,
    /// <paramref name="reason"/> says why.
    /// </summary>
    public bool CanAddPiston(int nodeIdA, int nodeIdB, [NotNullWhen(false)] out UiText? reason)
    {
        if (!HasNode(nodeIdA) || !HasNode(nodeIdB) || nodeIdA == nodeIdB)
        {
            reason = UiText.Plain("A piston must connect two different nodes.");
            return false;
        }

        reason = LinkBlockedReason(nodeIdA, nodeIdB);
        return reason is null;
    }

    /// <summary>
    /// Whether <see cref="AddSpring"/> would accept this pair (#453): the same rules as a Piston's,
    /// two distinct, existing nodes with no beam and no link between them; if not,
    /// <paramref name="reason"/> says why.
    /// </summary>
    public bool CanAddSpring(int nodeIdA, int nodeIdB, [NotNullWhen(false)] out UiText? reason)
    {
        if (!HasNode(nodeIdA) || !HasNode(nodeIdB) || nodeIdA == nodeIdB)
        {
            reason = UiText.Plain("A spring must connect two different nodes.");
            return false;
        }

        reason = LinkBlockedReason(nodeIdA, nodeIdB);
        return reason is null;
    }

    // Two nodes hold one link at most, and a beam between them would hold any link rigid.
    private UiText? LinkBlockedReason(int nodeIdA, int nodeIdB) =>
        _beams.Any(beam => IsSamePair(beam, nodeIdA, nodeIdB)) ? BeamJoinsTheseNodesReason : LinkReason(nodeIdA, nodeIdB);

    // Why the link already between these nodes stops another part; null when there is none.
    private UiText? LinkReason(int nodeIdA, int nodeIdB) =>
        _pistons.Any(piston => IsSamePair(piston.NodeA, piston.NodeB, nodeIdA, nodeIdB)) ? PistonJoinsTheseNodesReason
        : _springs.Any(spring => IsSamePair(spring.NodeA, spring.NodeB, nodeIdA, nodeIdB)) ? SpringJoinsTheseNodesReason
        : null;

    /// <summary>Adds a Piston between two nodes with the default settings and returns its id; see <see cref="CanAddPiston"/>.</summary>
    public int AddPiston(int nodeIdA, int nodeIdB)
    {
        ValidateNodeId(nodeIdA);
        ValidateNodeId(nodeIdB);
        if (!CanAddPiston(nodeIdA, nodeIdB, out var reason))
        {
            throw new ArgumentException(reason.Message);
        }

        var id = AllocatePartId();
        _pistons.Add(new PistonDef(id, nodeIdA, nodeIdB));
        return id;
    }

    /// <summary>Adds a Spring between two nodes with the default settings and returns its id; see <see cref="CanAddSpring"/>.</summary>
    public int AddSpring(int nodeIdA, int nodeIdB)
    {
        ValidateNodeId(nodeIdA);
        ValidateNodeId(nodeIdB);
        if (!CanAddSpring(nodeIdA, nodeIdB, out var reason))
        {
            throw new ArgumentException(reason.Message);
        }

        var id = AllocatePartId();
        _springs.Add(new SpringDef(id, nodeIdA, nodeIdB));
        return id;
    }

    /// <summary>The settings part <paramref name="partId"/> has (#704), in panel order; a joint and a beam have none.</summary>
    public IReadOnlyList<PartParameterId> ParametersOf(int partId) =>
        _pistons.Any(piston => piston.Id == partId) ? _pistonParameters
        : _springs.Any(spring => spring.Id == partId) ? _springParameters
        : _sensors.Any(sensor => sensor.Id == partId && sensor.Kind == SensorKind.Camera) ? _cameraParameters
        : [];

    /// <summary>Part <paramref name="partId"/>'s <paramref name="parameter"/>, in world units.</summary>
    public double ParameterValue(int partId, PartParameterId parameter) => parameter switch
    {
        PartParameterId.Strength => _pistons[PistonIndexOf(partId)].Strength,
        PartParameterId.Stroke => _pistons[PistonIndexOf(partId)].Stroke,
        PartParameterId.MaxSpeed => _pistons[PistonIndexOf(partId)].MaxSpeed,
        PartParameterId.Aim => Camera(partId).Aim ?? DefaultAim(Camera(partId).BeamId),
        PartParameterId.Stiffness => _springs[SpringIndexOf(partId)].Stiffness,
        PartParameterId.Damping => _springs[SpringIndexOf(partId)].Damping,
        _ => throw new ArgumentOutOfRangeException(nameof(parameter)),
    };

    /// <summary>Sets part <paramref name="partId"/>'s <paramref name="parameter"/>, in world units, keeping its other settings.</summary>
    public void SetParameter(int partId, PartParameterId parameter, double value)
    {
        if (parameter == PartParameterId.Aim)
        {
            var camera = Camera(partId);
            _sensors[SensorIndexOf(partId)] = camera.WithAim(value);
            return;
        }

        if (parameter is PartParameterId.Stiffness or PartParameterId.Damping)
        {
            var springIndex = SpringIndexOf(partId);
            var spring = _springs[springIndex];
            _springs[springIndex] = parameter == PartParameterId.Stiffness
                ? spring.WithSettings(value, spring.Damping)
                : spring.WithSettings(spring.Stiffness, value);
            return;
        }

        var index = PistonIndexOf(partId);
        var piston = _pistons[index];
        _pistons[index] = parameter switch
        {
            PartParameterId.Strength => piston.WithSettings(value, piston.Stroke, piston.MaxSpeed),
            PartParameterId.Stroke => piston.WithSettings(piston.Strength, value, piston.MaxSpeed),
            PartParameterId.MaxSpeed => piston.WithSettings(piston.Strength, piston.Stroke, value),
            _ => throw new ArgumentOutOfRangeException(nameof(parameter)),
        };
    }

    private static readonly PartParameterId[] _pistonParameters = [PartParameterId.Strength, PartParameterId.Stroke, PartParameterId.MaxSpeed];

    private static readonly PartParameterId[] _springParameters = [PartParameterId.Stiffness, PartParameterId.Damping];

    private static readonly PartParameterId[] _cameraParameters = [PartParameterId.Aim];

    private SensorDef Camera(int sensorId) => _sensors[SensorIndexOf(sensorId)] is { Kind: SensorKind.Camera } camera
        ? camera
        : throw new ArgumentOutOfRangeException(nameof(sensorId), "Only a Camera has an aim.");

    private double DefaultAim(int beamId)
    {
        var beam = _beams[BeamIndexOf(beamId)];
        return SensorDef.DefaultAim(_nodes[NodeIndexOf(beam.NodeA)].Position, _nodes[NodeIndexOf(beam.NodeB)].Position);
    }

    /// <summary>Removes a Piston by id.</summary>
    public void RemovePiston(int pistonId) => _pistons.RemoveAt(PistonIndexOf(pistonId));

    /// <summary>Removes a Spring by id.</summary>
    public void RemoveSpring(int springId) => _springs.RemoveAt(SpringIndexOf(springId));

    /// <summary>Removes a beam by id, cascading to sensors on it.</summary>
    public void RemoveBeam(int beamId)
    {
        var beamIndex = BeamIndexOf(beamId);
        _beams.RemoveAt(beamIndex);
        _sensors.RemoveAll(sensor => sensor.BeamId == beamId);
    }

    /// <summary>Adds a sensor mounted on an existing beam unless that beam already has a sensor.</summary>
    public bool AddSensor(int beamId, SensorKind kind, out int sensorId, [NotNullWhen(false)] out UiText? reason)
    {
        ValidateBeamId(beamId);
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), "Sensor kind must be defined.");
        }

        if (_sensors.Any(sensor => sensor.BeamId == beamId))
        {
            sensorId = 0;
            reason = OneSensorPerBeamReason;
            return false;
        }

        sensorId = AllocatePartId();
        var beam = _beams[BeamIndexOf(beamId)];
        double? aim = kind == SensorKind.Camera
            ? SensorDef.DefaultAim(_nodes[NodeIndexOf(beam.NodeA)].Position, _nodes[NodeIndexOf(beam.NodeB)].Position)
            : null;
        _sensors.Add(new SensorDef(sensorId, beamId, kind, aim: aim));
        reason = null;
        return true;
    }

    /// <summary>Removes a sensor by id.</summary>
    public void RemoveSensor(int sensorId)
    {
        var sensorIndex = SensorIndexOf(sensorId);
        _sensors.RemoveAt(sensorIndex);
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
            _nodes[nodeIndex] = new NodeDef(node.Id, node.Position, name);
            return;
        }

        var beamIndex = _beams.FindIndex(beam => beam.Id == partId);
        if (beamIndex >= 0)
        {
            var beam = _beams[beamIndex];
            _beams[beamIndex] = new BeamDef(beam.Id, beam.NodeA, beam.NodeB, name);
            return;
        }

        var sensorIndex = _sensors.FindIndex(sensor => sensor.Id == partId);
        if (sensorIndex >= 0)
        {
            var sensor = _sensors[sensorIndex];
            _sensors[sensorIndex] = sensor.WithName(name);
            return;
        }

        var pistonIndex = _pistons.FindIndex(piston => piston.Id == partId);
        if (pistonIndex >= 0)
        {
            _pistons[pistonIndex] = _pistons[pistonIndex].WithName(name);
            return;
        }

        var springIndex = _springs.FindIndex(spring => spring.Id == partId);
        if (springIndex >= 0)
        {
            _springs[springIndex] = _springs[springIndex].WithName(name);
            return;
        }

        throw new ArgumentOutOfRangeException(nameof(partId), "Part id must point to an existing part.");
    }

    /// <summary>The current drawing, finished or not: what a saved Creation stores.</summary>
    public CreatureDef Build() => new(_nodes, _beams, _sensors, _pistons, _springs, _nextPartId);

    /// <summary>The current drawing if it can be simulated, else the player-facing problems that stop it.</summary>
    public bool TryBuild(out CreatureDef? creature, out IReadOnlyList<UiText> errors)
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

    public int SensorIndexOf(int sensorId)
    {
        var index = _sensors.FindIndex(sensor => sensor.Id == sensorId);
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sensorId), "Sensor id must point to an existing sensor.");
        }

        return index;
    }

    public int PistonIndexOf(int pistonId)
    {
        var index = _pistons.FindIndex(piston => piston.Id == pistonId);
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pistonId), "Piston id must point to an existing piston.");
        }

        return index;
    }

    public int SpringIndexOf(int springId)
    {
        var index = _springs.FindIndex(spring => spring.Id == springId);
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(springId), "Spring id must point to an existing spring.");
        }

        return index;
    }

    private static bool IsSamePair(BeamDef beam, int nodeA, int nodeB) => IsSamePair(beam.NodeA, beam.NodeB, nodeA, nodeB);

    private static bool IsSamePair(int linkA, int linkB, int nodeA, int nodeB) =>
        (linkA == nodeA && linkB == nodeB) || (linkA == nodeB && linkB == nodeA);

    private bool HasNode(int nodeId) => _nodes.Any(node => node.Id == nodeId);

    private bool HasBeam(int beamId) => _beams.Any(beam => beam.Id == beamId);

    private void ValidateNodeId(int nodeId)
    {
        if (!HasNode(nodeId))
        {
            throw new ArgumentOutOfRangeException(nameof(nodeId), "Node id must point to an existing node.");
        }
    }

    private void ValidateBeamId(int beamId)
    {
        if (!HasBeam(beamId))
        {
            throw new ArgumentOutOfRangeException(nameof(beamId), "Beam id must point to an existing beam.");
        }
    }

    private int AllocatePartId() => _nextPartId++;
}
