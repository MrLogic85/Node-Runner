using NodeRunner.App.Lifecycle;
using NodeRunner.Domain;

namespace NodeRunner.App.Builders;

/// <summary>
/// Mutable, in-progress creature anatomy driven by Build-mode UI
/// (0.3.0). Add/move/remove nodes, beams, sensors and Pistons here; <see cref="Build"/>
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
    public const string OneSensorPerBeamReason = "One sensor per beam";

    /// <summary>Why a Piston cannot join two nodes a beam already holds rigid (#451).</summary>
    public const string BeamJoinsTheseNodesReason = "A beam already joins these nodes";

    /// <summary>Why a beam or a second Piston cannot join two nodes a Piston already links.</summary>
    public const string PistonJoinsTheseNodesReason = "These nodes already have a piston";

    private readonly List<NodeDef> _nodes = [];
    private readonly List<BeamDef> _beams = [];
    private readonly List<SensorDef> _sensors = [];
    private readonly List<PistonDef> _pistons = [];
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
        _nextPartId = creature.NextPartId;
    }

    public IReadOnlyList<NodeDef> Nodes => _nodes;

    public IReadOnlyList<BeamDef> Beams => _beams;

    public IReadOnlyList<SensorDef> Sensors => _sensors;

    public IReadOnlyList<PistonDef> Pistons => _pistons;

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
    /// Removes a node, cascading to every beam, sensor and Piston that referenced it.
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
        _nodes.RemoveAt(nodeIndex);
    }

    /// <summary>
    /// Adds a beam between two distinct, existing nodes and returns its
    /// id. Throws if either node id is invalid, the nodes are the
    /// same, or a beam or Piston between them already exists.
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

        if (_pistons.Any(piston => IsSamePair(piston.NodeA, piston.NodeB, nodeIdA, nodeIdB)))
        {
            throw new ArgumentException(PistonJoinsTheseNodesReason);
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
        && !_pistons.Any(piston => IsSamePair(piston.NodeA, piston.NodeB, nodeIdA, nodeIdB));

    /// <summary>
    /// Whether <see cref="AddPiston"/> would accept this pair (#451): two distinct, existing nodes
    /// with no beam between them, which would hold them rigid, and no Piston yet; if not,
    /// <paramref name="reason"/> says why.
    /// </summary>
    public bool CanAddPiston(int nodeIdA, int nodeIdB, out string reason)
    {
        if (!HasNode(nodeIdA) || !HasNode(nodeIdB) || nodeIdA == nodeIdB)
        {
            reason = "A piston must connect two different nodes.";
            return false;
        }

        if (_beams.Any(beam => IsSamePair(beam, nodeIdA, nodeIdB)))
        {
            reason = BeamJoinsTheseNodesReason;
            return false;
        }

        if (_pistons.Any(piston => IsSamePair(piston.NodeA, piston.NodeB, nodeIdA, nodeIdB)))
        {
            reason = PistonJoinsTheseNodesReason;
            return false;
        }

        reason = string.Empty;
        return true;
    }

    /// <summary>Adds a Piston between two nodes with the default settings and returns its id; see <see cref="CanAddPiston"/>.</summary>
    public int AddPiston(int nodeIdA, int nodeIdB)
    {
        ValidateNodeId(nodeIdA);
        ValidateNodeId(nodeIdB);
        if (!CanAddPiston(nodeIdA, nodeIdB, out var reason))
        {
            throw new ArgumentException(reason);
        }

        var id = AllocatePartId();
        _pistons.Add(new PistonDef(id, nodeIdA, nodeIdB));
        return id;
    }

    /// <summary>Changes a Piston's Strength, stroke and max speed (#451).</summary>
    public void SetPistonSettings(int pistonId, double strength, double stroke, double maxSpeed)
    {
        var index = PistonIndexOf(pistonId);
        _pistons[index] = _pistons[index].WithSettings(strength, stroke, maxSpeed);
    }

    /// <summary>Removes a Piston by id.</summary>
    public void RemovePiston(int pistonId) => _pistons.RemoveAt(PistonIndexOf(pistonId));

    /// <summary>Removes a beam by id, cascading to sensors on it.</summary>
    public void RemoveBeam(int beamId)
    {
        var beamIndex = BeamIndexOf(beamId);
        _beams.RemoveAt(beamIndex);
        _sensors.RemoveAll(sensor => sensor.BeamId == beamId);
    }

    /// <summary>
    /// Replaces a beam with two beams through an existing node. The beam's sensors move, keeping
    /// their ids, to the longer half (the half at the beam's NodeA on a tie).
    /// </summary>
    public (int FirstBeamId, int SecondBeamId) SplitBeamAtNode(int beamId, int nodeId)
    {
        ValidateNodeId(nodeId);
        var beamIndex = BeamIndexOf(beamId);
        var beam = _beams[beamIndex];
        var movedSensors = _sensors
            .Where(sensor => sensor.BeamId == beamId)
            .ToArray();

        _beams.RemoveAt(beamIndex);
        var firstBeamId = AddBeam(beam.NodeA, nodeId);
        var secondBeamId = AddBeam(nodeId, beam.NodeB);
        var sensorsToFirstHalf = DistanceSquared(beam.NodeA, nodeId) >= DistanceSquared(nodeId, beam.NodeB);
        var targetBeamId = sensorsToFirstHalf ? firstBeamId : secondBeamId;
        foreach (var sensor in movedSensors)
        {
            var sensorIndex = SensorIndexOf(sensor.Id);
            _sensors[sensorIndex] = sensor.WithBeam(targetBeamId);
        }

        return (firstBeamId, secondBeamId);
    }

    /// <summary>Adds a sensor mounted on an existing beam unless that beam already has a sensor.</summary>
    public bool AddSensor(int beamId, SensorKind kind, out int sensorId, out string reason)
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
            ? CameraRays.DefaultAim(_nodes[NodeIndexOf(beam.NodeA)].Position, _nodes[NodeIndexOf(beam.NodeB)].Position)
            : null;
        _sensors.Add(new SensorDef(sensorId, beamId, kind, aim: aim));
        reason = string.Empty;
        return true;
    }

    /// <summary>Turns a Camera to <paramref name="aim"/>, relative to its beam (#594).</summary>
    public void SetCameraAim(int sensorId, double aim)
    {
        var sensorIndex = SensorIndexOf(sensorId);
        var sensor = _sensors[sensorIndex];
        if (sensor.Kind != SensorKind.Camera)
        {
            throw new ArgumentOutOfRangeException(nameof(sensorId), "Only a Camera has an aim.");
        }

        _sensors[sensorIndex] = sensor.WithAim(aim);
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

        throw new ArgumentOutOfRangeException(nameof(partId), "Part id must point to an existing part.");
    }

    /// <summary>The current drawing, finished or not: what a saved Creation stores.</summary>
    public CreatureDef Build() => new(_nodes, _beams, _sensors, _pistons, _nextPartId);

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

    private static bool IsSamePair(BeamDef beam, int nodeA, int nodeB) => IsSamePair(beam.NodeA, beam.NodeB, nodeA, nodeB);

    private static bool IsSamePair(int linkA, int linkB, int nodeA, int nodeB) =>
        (linkA == nodeA && linkB == nodeB) || (linkA == nodeB && linkB == nodeA);

    private double DistanceSquared(int nodeIdA, int nodeIdB)
    {
        var a = _nodes[NodeIndexOf(nodeIdA)].Position;
        var b = _nodes[NodeIndexOf(nodeIdB)].Position;
        return ((a.X - b.X) * (a.X - b.X)) + ((a.Y - b.Y) * (a.Y - b.Y));
    }

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
