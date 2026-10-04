using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace NodeRunner.Domain;

/// <summary>
/// A drawn creature: nodes, the beams between them, the sensors on them and the Pistons and Springs
/// that link them. Any drawing is a valid
/// <see cref="CreatureDef"/>, so an unfinished one can be saved; only its part references must point
/// at existing parts. Whether it can be simulated and trained is checked before training
/// (<c>CreatureReadiness</c> in <c>NodeRunner.App</c>). A Camera placed without an aim gets
/// <see cref="SensorDef.DefaultAim"/> from its beam's pose here, so every Camera has one.
/// </summary>
public sealed record CreatureDef
{
    private readonly ReadOnlyCollection<NodeDef> _nodes;
    private readonly ReadOnlyCollection<BeamDef> _beams;
    private readonly ReadOnlyCollection<SensorDef> _sensors;
    private readonly ReadOnlyCollection<PistonDef> _pistons;
    private readonly ReadOnlyCollection<SpringDef> _springs;
    private readonly Dictionary<int, int> _nodeIndexById;
    private readonly Dictionary<int, int> _beamIndexById;
    private readonly Dictionary<int, int> _sensorIndexById;
    private readonly Dictionary<int, int> _pistonIndexById;
    private readonly Dictionary<int, int> _springIndexById;

    public CreatureDef(IReadOnlyList<NodeDef> nodes, IReadOnlyList<BeamDef> beams, IReadOnlyList<SensorDef> sensors)
        : this(nodes, beams, sensors, [], [], nextPartId: null)
    {
    }

    public CreatureDef(IReadOnlyList<NodeDef> nodes, IReadOnlyList<BeamDef> beams, IReadOnlyList<SensorDef> sensors, int nextPartId)
        : this(nodes, beams, sensors, [], [], (int?)nextPartId)
    {
    }

    public CreatureDef(IReadOnlyList<NodeDef> nodes, IReadOnlyList<BeamDef> beams, IReadOnlyList<SensorDef> sensors, IReadOnlyList<PistonDef> pistons)
        : this(nodes, beams, sensors, pistons, [], nextPartId: null)
    {
    }

    public CreatureDef(IReadOnlyList<NodeDef> nodes, IReadOnlyList<BeamDef> beams, IReadOnlyList<SensorDef> sensors, IReadOnlyList<PistonDef> pistons, int nextPartId)
        : this(nodes, beams, sensors, pistons, [], (int?)nextPartId)
    {
    }

    [JsonConstructor]
    public CreatureDef(IReadOnlyList<NodeDef> nodes, IReadOnlyList<BeamDef> beams, IReadOnlyList<SensorDef> sensors, IReadOnlyList<PistonDef> pistons, IReadOnlyList<SpringDef> springs, int nextPartId)
        : this(nodes, beams, sensors, pistons, springs, (int?)nextPartId)
    {
    }

    private CreatureDef(IReadOnlyList<NodeDef> nodes, IReadOnlyList<BeamDef> beams, IReadOnlyList<SensorDef> sensors, IReadOnlyList<PistonDef> pistons, IReadOnlyList<SpringDef> springs, int? nextPartId)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(beams);
        ArgumentNullException.ThrowIfNull(sensors);
        ArgumentNullException.ThrowIfNull(pistons);
        ArgumentNullException.ThrowIfNull(springs);
        if (nodes.Contains(null!) || beams.Contains(null!) || sensors.Contains(null!) || pistons.Contains(null!) || springs.Contains(null!))
        {
            throw new ArgumentException("A creature's part lists cannot contain null.");
        }

        var maxId = 0;
        var ids = new HashSet<int>();
        ValidatePartIds(nodes.Select(node => node.Id), ids, ref maxId);
        ValidatePartIds(beams.Select(beam => beam.Id), ids, ref maxId);
        ValidatePartIds(sensors.Select(sensor => sensor.Id), ids, ref maxId);
        ValidatePartIds(pistons.Select(piston => piston.Id), ids, ref maxId);
        ValidatePartIds(springs.Select(spring => spring.Id), ids, ref maxId);

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

        foreach (var piston in pistons)
        {
            ValidateNodeId(piston.NodeA, nodeIds);
            ValidateNodeId(piston.NodeB, nodeIds);
        }

        foreach (var spring in springs)
        {
            ValidateNodeId(spring.NodeA, nodeIds);
            ValidateNodeId(spring.NodeB, nodeIds);
        }

        var beamIds = beams.Select(beam => beam.Id).ToHashSet();
        var beamsWithSensor = new HashSet<int>();
        foreach (var sensor in sensors)
        {
            ValidateBeamId(sensor.BeamId, beamIds);
            if (!beamsWithSensor.Add(sensor.BeamId))
            {
                throw new ArgumentException($"Beam id {sensor.BeamId} already has a sensor; a beam holds at most one.");
            }
        }

        _nodes = Array.AsReadOnly(nodes.ToArray());
        _beams = Array.AsReadOnly(beams.ToArray());
        _sensors = Array.AsReadOnly(sensors.Select(sensor => WithAim(sensor, nodes, beams)).ToArray());
        _pistons = Array.AsReadOnly(pistons.ToArray());
        _springs = Array.AsReadOnly(springs.ToArray());
        NextPartId = resolvedNextPartId;
        _nodeIndexById = BuildIndex(_nodes, node => node.Id);
        _beamIndexById = BuildIndex(_beams, beam => beam.Id);
        _sensorIndexById = BuildIndex(_sensors, sensor => sensor.Id);
        _pistonIndexById = BuildIndex(_pistons, piston => piston.Id);
        _springIndexById = BuildIndex(_springs, spring => spring.Id);
    }

    public IReadOnlyList<NodeDef> Nodes => _nodes;

    public IReadOnlyList<BeamDef> Beams => _beams;

    public IReadOnlyList<SensorDef> Sensors => _sensors;

    /// <summary>The Pistons (#451): powered links between two nodes.</summary>
    public IReadOnlyList<PistonDef> Pistons => _pistons;

    /// <summary>The Springs (#453): passive links between two nodes.</summary>
    public IReadOnlyList<SpringDef> Springs => _springs;

    public int NextPartId { get; }

    public int NodeIndexOf(int nodeId) => IndexOf(_nodeIndexById, nodeId, "Node id must point to an existing node.");

    public int BeamIndexOf(int beamId) => IndexOf(_beamIndexById, beamId, "Beam id must point to an existing beam.");

    public int SensorIndexOf(int sensorId) => IndexOf(_sensorIndexById, sensorId, "Sensor id must point to an existing sensor.");

    public int PistonIndexOf(int pistonId) => IndexOf(_pistonIndexById, pistonId, "Piston id must point to an existing piston.");

    public int SpringIndexOf(int springId) => IndexOf(_springIndexById, springId, "Spring id must point to an existing spring.");

    private static SensorDef WithAim(SensorDef sensor, IReadOnlyList<NodeDef> nodes, IReadOnlyList<BeamDef> beams)
    {
        if (sensor.Kind != SensorKind.Camera || sensor.Aim is not null)
        {
            return sensor;
        }

        var beam = beams.First(entry => entry.Id == sensor.BeamId);
        return sensor.WithAim(SensorDef.DefaultAim(
            nodes.First(node => node.Id == beam.NodeA).Position,
            nodes.First(node => node.Id == beam.NodeB).Position));
    }

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

    private static void ValidateBeamId(int beamId, HashSet<int> beamIds)
    {
        if (!beamIds.Contains(beamId))
        {
            throw new ArgumentOutOfRangeException(nameof(beamId), "Beam id must point to an existing beam.");
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
