using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace NodeRunner.Domain;

/// <summary>
/// A drawn creature: nodes, the beams between them, the sensors on the beams, the joint parts on
/// the nodes (Servos, Velocity motors, Brakes, Angle sensors, Wheels, Touch sensors and Rubbers) and the Pistons and Springs that link them. Any drawing is a valid
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
    private readonly ReadOnlyCollection<ServoDef> _servos;
    private readonly ReadOnlyCollection<PistonDef> _pistons;
    private readonly ReadOnlyCollection<SpringDef> _springs;
    private readonly ReadOnlyCollection<WheelDef> _wheels;
    private readonly ReadOnlyCollection<TouchSensorDef> _touchSensors;
    private readonly ReadOnlyCollection<RubberDef> _rubbers;
    private readonly ReadOnlyCollection<VelocityMotorDef> _velocityMotors;
    private readonly ReadOnlyCollection<BrakeDef> _brakes;
    private readonly ReadOnlyCollection<AngleSensorDef> _angleSensors;
    private readonly Dictionary<int, IPartDef> _partById;
    private readonly Dictionary<int, int> _nodeIndexById;
    private readonly Dictionary<int, int> _beamIndexById;
    private readonly Dictionary<int, int> _sensorIndexById;
    private readonly Dictionary<int, int> _servoIndexById;
    private readonly Dictionary<int, int> _pistonIndexById;
    private readonly Dictionary<int, int> _springIndexById;
    private readonly Dictionary<int, int> _wheelIndexById;
    private readonly Dictionary<int, int> _touchSensorIndexById;
    private readonly Dictionary<int, int> _rubberIndexById;
    private readonly Dictionary<int, int> _velocityMotorIndexById;
    private readonly Dictionary<int, int> _brakeIndexById;
    private readonly Dictionary<int, int> _angleSensorIndexById;

    internal CreatureDef(IReadOnlyList<NodeDef> nodes, IReadOnlyList<BeamDef> beams, IReadOnlyList<SensorDef> sensors)
        : this(Join(nodes, beams, sensors))
    {
    }

    internal CreatureDef(IReadOnlyList<NodeDef> nodes, IReadOnlyList<BeamDef> beams, IReadOnlyList<SensorDef> sensors, int nextPartId)
        : this(Join(nodes, beams, sensors), nextPartId)
    {
    }

    internal CreatureDef(IReadOnlyList<NodeDef> nodes, IReadOnlyList<BeamDef> beams, IReadOnlyList<SensorDef> sensors, IReadOnlyList<PistonDef> pistons)
        : this(Join(nodes, beams, sensors, pistons))
    {
    }

    internal CreatureDef(IReadOnlyList<NodeDef> nodes, IReadOnlyList<BeamDef> beams, IReadOnlyList<SensorDef> sensors, IReadOnlyList<PistonDef> pistons, int nextPartId)
        : this(Join(nodes, beams, sensors, pistons), nextPartId)
    {
    }

    internal CreatureDef(IReadOnlyList<NodeDef> nodes, IReadOnlyList<BeamDef> beams, IReadOnlyList<SensorDef> sensors, IReadOnlyList<PistonDef> pistons, IReadOnlyList<SpringDef> springs, int nextPartId)
        : this(Join(nodes, beams, sensors, pistons, springs), nextPartId)
    {
    }

    internal CreatureDef(IReadOnlyList<NodeDef> nodes, IReadOnlyList<BeamDef> beams, IReadOnlyList<SensorDef> sensors, IReadOnlyList<ServoDef> servos, IReadOnlyList<PistonDef> pistons, IReadOnlyList<SpringDef> springs, int nextPartId)
        : this(Join(nodes, beams, sensors, servos, pistons, springs), nextPartId)
    {
    }

    internal CreatureDef(IReadOnlyList<NodeDef> nodes, IReadOnlyList<BeamDef> beams, IReadOnlyList<SensorDef> sensors, IReadOnlyList<ServoDef> servos, IReadOnlyList<PistonDef> pistons, IReadOnlyList<SpringDef> springs, IReadOnlyList<WheelDef> wheels, int nextPartId)
        : this(Join(nodes, beams, sensors, servos, pistons, springs, wheels), nextPartId)
    {
    }

    internal CreatureDef(IReadOnlyList<NodeDef> nodes, IReadOnlyList<BeamDef> beams, IReadOnlyList<SensorDef> sensors, IReadOnlyList<ServoDef> servos, IReadOnlyList<PistonDef> pistons, IReadOnlyList<SpringDef> springs, IReadOnlyList<WheelDef> wheels, IReadOnlyList<TouchSensorDef> touchSensors, int nextPartId)
        : this(Join(nodes, beams, sensors, servos, pistons, springs, wheels, touchSensors), nextPartId)
    {
    }

    internal CreatureDef(IReadOnlyList<NodeDef> nodes, IReadOnlyList<BeamDef> beams, IReadOnlyList<SensorDef> sensors, IReadOnlyList<ServoDef> servos, IReadOnlyList<PistonDef> pistons, IReadOnlyList<SpringDef> springs, IReadOnlyList<WheelDef> wheels, IReadOnlyList<TouchSensorDef> touchSensors, IReadOnlyList<RubberDef> rubbers, int nextPartId)
        : this(Join(nodes, beams, sensors, servos, pistons, springs, wheels, touchSensors, rubbers), nextPartId)
    {
    }

    internal CreatureDef(IReadOnlyList<NodeDef> nodes, IReadOnlyList<BeamDef> beams, IReadOnlyList<SensorDef> sensors, IReadOnlyList<ServoDef> servos, IReadOnlyList<PistonDef> pistons, IReadOnlyList<SpringDef> springs, IReadOnlyList<WheelDef> wheels, IReadOnlyList<TouchSensorDef> touchSensors, IReadOnlyList<RubberDef> rubbers, IReadOnlyList<VelocityMotorDef> velocityMotors, IReadOnlyList<BrakeDef> brakes, int nextPartId)
        : this(Join(nodes, beams, sensors, servos, pistons, springs, wheels, touchSensors, rubbers, velocityMotors, brakes), nextPartId)
    {
    }

    [JsonConstructor]
    internal CreatureDef(IReadOnlyList<NodeDef> nodes, IReadOnlyList<BeamDef> beams, IReadOnlyList<SensorDef> sensors, IReadOnlyList<ServoDef> servos, IReadOnlyList<PistonDef> pistons, IReadOnlyList<SpringDef> springs, IReadOnlyList<WheelDef> wheels, IReadOnlyList<TouchSensorDef> touchSensors, IReadOnlyList<RubberDef> rubbers, IReadOnlyList<VelocityMotorDef> velocityMotors, IReadOnlyList<BrakeDef> brakes, IReadOnlyList<AngleSensorDef> angleSensors, int nextPartId)
        : this(Join(nodes, beams, sensors, servos, pistons, springs, wheels, touchSensors, rubbers, velocityMotors, brakes, angleSensors), nextPartId)
    {
    }

    /// <summary>
    /// A creature of <paramref name="parts"/> of any kinds, in any order (#1222): each kind keeps the order it has among
    /// them. Without <paramref name="nextPartId"/>, the counter starts just past the highest id.
    /// </summary>
    public CreatureDef(IEnumerable<IPartDef> parts, int? nextPartId = null)
    {
        ArgumentNullException.ThrowIfNull(parts);
        List<NodeDef> nodes = [];
        List<BeamDef> beams = [];
        List<SensorDef> sensors = [];
        List<ServoDef> servos = [];
        List<PistonDef> pistons = [];
        List<SpringDef> springs = [];
        List<WheelDef> wheels = [];
        List<TouchSensorDef> touchSensors = [];
        List<RubberDef> rubbers = [];
        List<VelocityMotorDef> velocityMotors = [];
        List<BrakeDef> brakes = [];
        List<AngleSensorDef> angleSensors = [];
        foreach (var part in parts)
        {
            switch (part)
            {
                case null:
                    throw new ArgumentException("A creature's part lists cannot contain null.", nameof(parts));
                case NodeDef node:
                    nodes.Add(node);
                    break;
                case BeamDef beam:
                    beams.Add(beam);
                    break;
                case SensorDef sensor:
                    sensors.Add(sensor);
                    break;
                case ServoDef servo:
                    servos.Add(servo);
                    break;
                case PistonDef piston:
                    pistons.Add(piston);
                    break;
                case SpringDef spring:
                    springs.Add(spring);
                    break;
                case WheelDef wheel:
                    wheels.Add(wheel);
                    break;
                case TouchSensorDef touchSensor:
                    touchSensors.Add(touchSensor);
                    break;
                case RubberDef rubber:
                    rubbers.Add(rubber);
                    break;
                case VelocityMotorDef velocityMotor:
                    velocityMotors.Add(velocityMotor);
                    break;
                case BrakeDef brake:
                    brakes.Add(brake);
                    break;
                case AngleSensorDef angleSensor:
                    angleSensors.Add(angleSensor);
                    break;
                default:
                    throw new ArgumentException($"A creature cannot hold a {part.GetType().Name}: it is not a part kind it saves.", nameof(parts));
            }
        }

        IPartDef[] sorted = [.. nodes, .. beams, .. sensors, .. servos, .. pistons, .. springs, .. wheels, .. touchSensors, .. rubbers, .. velocityMotors, .. brakes, .. angleSensors];

        var maxId = 0;
        var ids = new HashSet<int>();
        ValidatePartIds(sorted.Select(part => part.Id), ids, ref maxId);

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
        var links = LinkRef.All(beams, pistons, springs).ToArray();
        // A joint holds one part per slot (#1044).
        var filledSlots = new HashSet<(int NodeId, JointSlot Slot)>();
        foreach (var part in sorted.OfType<IJointPartDef>())
        {
            ValidateJointPart(part, nodeIds, filledSlots);
            if (part is IHingePartDef hingePart)
            {
                ValidateHingePartSide(hingePart.FixedLinkId, part.NodeId, links, wheels);
                ValidateHingePartSide(hingePart.TargetLinkId, part.NodeId, links, wheels);
            }
        }

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
        _servos = Array.AsReadOnly(servos.ToArray());
        _pistons = Array.AsReadOnly(pistons.ToArray());
        _springs = Array.AsReadOnly(springs.ToArray());
        _wheels = Array.AsReadOnly(wheels.ToArray());
        _touchSensors = Array.AsReadOnly(touchSensors.ToArray());
        _rubbers = Array.AsReadOnly(rubbers.ToArray());
        _velocityMotors = Array.AsReadOnly(velocityMotors.ToArray());
        _brakes = Array.AsReadOnly(brakes.ToArray());
        _angleSensors = Array.AsReadOnly(angleSensors.ToArray());
        JointMotors = Array.AsReadOnly(_servos.Concat<IJointMotorDef>(_velocityMotors).ToArray());
        HingeParts = Array.AsReadOnly(JointMotors.Concat<IHingePartDef>(_brakes).Concat(_angleSensors).ToArray());
        Parts = Array.AsReadOnly<IPartDef>([.. _nodes, .. _beams, .. _sensors, .. _servos, .. _pistons, .. _springs, .. _wheels, .. _touchSensors, .. _rubbers, .. _velocityMotors, .. _brakes, .. _angleSensors]);
        _partById = Parts.ToDictionary(part => part.Id);
        NextPartId = resolvedNextPartId;
        _nodeIndexById = BuildIndex(_nodes, node => node.Id);
        _beamIndexById = BuildIndex(_beams, beam => beam.Id);
        _sensorIndexById = BuildIndex(_sensors, sensor => sensor.Id);
        _servoIndexById = BuildIndex(_servos, servo => servo.Id);
        _pistonIndexById = BuildIndex(_pistons, piston => piston.Id);
        _springIndexById = BuildIndex(_springs, spring => spring.Id);
        _wheelIndexById = BuildIndex(_wheels, wheel => wheel.Id);
        _touchSensorIndexById = BuildIndex(_touchSensors, touch => touch.Id);
        _rubberIndexById = BuildIndex(_rubbers, rubber => rubber.Id);
        _velocityMotorIndexById = BuildIndex(_velocityMotors, motor => motor.Id);
        _brakeIndexById = BuildIndex(_brakes, brake => brake.Id);
        _angleSensorIndexById = BuildIndex(_angleSensors, angleSensor => angleSensor.Id);
    }

    public IReadOnlyList<NodeDef> Nodes => _nodes;

    public IReadOnlyList<BeamDef> Beams => _beams;

    public IReadOnlyList<SensorDef> Sensors => _sensors;

    /// <summary>The Servos (#452): powered joint motors on nodes.</summary>
    public IReadOnlyList<ServoDef> Servos => _servos;

    /// <summary>The Pistons (#451): powered links between two nodes.</summary>
    public IReadOnlyList<PistonDef> Pistons => _pistons;

    /// <summary>The Springs (#453): passive links between two nodes.</summary>
    public IReadOnlyList<SpringDef> Springs => _springs;

    /// <summary>The Wheels (#129): passive joint parts that roll on the ground.</summary>
    public IReadOnlyList<WheelDef> Wheels => _wheels;

    /// <summary>The Touch sensors (#1043): one input each, how near its node is to the ground.</summary>
    public IReadOnlyList<TouchSensorDef> TouchSensors => _touchSensors;

    /// <summary>The Rubbers (#1142): passive parts that make their node grip the ground.</summary>
    public IReadOnlyList<RubberDef> Rubbers => _rubbers;

    /// <summary>The Velocity motors (#454): powered joint motors the brain drives at a speed.</summary>
    public IReadOnlyList<VelocityMotorDef> VelocityMotors => _velocityMotors;

    /// <summary>The Brakes (#1039): passive joint parts that resist their sides turning, with no brain ports.</summary>
    public IReadOnlyList<BrakeDef> Brakes => _brakes;

    /// <summary>The Angle sensors (#1204): joint sensors that read the angle between their sides and apply no force.</summary>
    public IReadOnlyList<AngleSensorDef> AngleSensors => _angleSensors;

    /// <summary>
    /// Every joint motor, the Servos then the Velocity motors. With the <see cref="Brakes"/> and <see cref="AngleSensors"/>, they fill
    /// the joints' hinge-part slots (#1044).
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<IJointMotorDef> JointMotors { get; }

    /// <summary>What fills the joints' hinge-part slots (#1044): the <see cref="JointMotors"/>, then the <see cref="Brakes"/>, then the <see cref="AngleSensors"/>.</summary>
    [JsonIgnore]
    public IReadOnlyList<IHingePartDef> HingeParts { get; }

    /// <summary>
    /// Every part (#1221), list by list: nodes, beams, sensors, Servos, Pistons, Springs, Wheels, Touch sensors, Rubbers,
    /// Velocity motors, Brakes and Angle sensors. Ids are unique across them.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<IPartDef> Parts { get; }

    /// <summary>How many <see cref="Parts"/> there are, which what scales with the creature's size reads (#318).</summary>
    [JsonIgnore]
    public int PartCount => Parts.Count;

    public int NextPartId { get; }

    /// <summary>The part with <paramref name="partId"/>, of whatever kind (#1221), or null if there is none.</summary>
    public IPartDef? FindPart(int partId) => _partById.GetValueOrDefault(partId);

    /// <summary>The joint parts on joint <paramref name="nodeId"/>: see the static <see cref="JointParts"/>.</summary>
    public IReadOnlyList<CreatureElementSelection> JointPartsAt(int nodeId)
    {
        NodeIndexOf(nodeId);
        return JointParts(nodeId, Parts);
    }

    public int NodeIndexOf(int nodeId) => IndexOf(_nodeIndexById, nodeId, "Node id must point to an existing node.");

    public int BeamIndexOf(int beamId) => IndexOf(_beamIndexById, beamId, "Beam id must point to an existing beam.");

    public int SensorIndexOf(int sensorId) => IndexOf(_sensorIndexById, sensorId, "Sensor id must point to an existing sensor.");

    public int ServoIndexOf(int servoId) => IndexOf(_servoIndexById, servoId, "Servo id must point to an existing servo.");

    public int PistonIndexOf(int pistonId) => IndexOf(_pistonIndexById, pistonId, "Piston id must point to an existing piston.");

    public int SpringIndexOf(int springId) => IndexOf(_springIndexById, springId, "Spring id must point to an existing spring.");

    public int WheelIndexOf(int wheelId) => IndexOf(_wheelIndexById, wheelId, "Wheel id must point to an existing wheel.");

    /// <summary>The Wheel on joint <paramref name="nodeId"/>, if it has one.</summary>
    public WheelDef? WheelAt(int nodeId) => _wheels.FirstOrDefault(wheel => wheel.NodeId == nodeId);

    /// <summary>Whether a motor's side id names a Wheel, a one-sided link (#1118), rather than a link.</summary>
    public bool IsWheel(int partId) => _wheelIndexById.ContainsKey(partId);

    public int TouchSensorIndexOf(int touchSensorId) => IndexOf(_touchSensorIndexById, touchSensorId, "Touch sensor id must point to an existing touch sensor.");

    public int RubberIndexOf(int rubberId) => IndexOf(_rubberIndexById, rubberId, "Rubber id must point to an existing rubber.");

    public int VelocityMotorIndexOf(int velocityMotorId) => IndexOf(_velocityMotorIndexById, velocityMotorId, "Velocity motor id must point to an existing velocity motor.");

    public int BrakeIndexOf(int brakeId) => IndexOf(_brakeIndexById, brakeId, "Brake id must point to an existing brake.");

    public int AngleSensorIndexOf(int angleSensorId) => IndexOf(_angleSensorIndexById, angleSensorId, "Angle sensor id must point to an existing angle sensor.");

    /// <summary>A node's drawn and collision radius: see <see cref="JointRadius"/>.</summary>
    public double NodeRadius(int nodeId)
    {
        NodeIndexOf(nodeId);
        return JointRadius(nodeId, JointMotors, _wheels);
    }

    /// <summary>
    /// The drawn and collision radius of the joint <paramref name="nodeId"/> among these joint parts
    /// (#452, #626, #129): the largest of a plain joint's, a motor's housing and a Wheel's
    /// <see cref="WheelDef.Radius"/> on it, for collision and framing. Links' ends use
    /// <see cref="NodeDef.HubRadius"/>, and touch and sensors <see cref="CoveredRadius(int)"/> (#1086).
    /// </summary>
    public static double JointRadius(int nodeId, IEnumerable<IJointMotorDef> motors, IEnumerable<WheelDef> wheels)
    {
        ArgumentNullException.ThrowIfNull(motors);
        ArgumentNullException.ThrowIfNull(wheels);
        return motors.Where(motor => motor.NodeId == nodeId).Select(_ => JointMotor.Radius)
            .Concat(wheels.Where(wheel => wheel.NodeId == nodeId).Select(wheel => wheel.Radius))
            .Aggregate(NodeDef.PlainJointRadius, Math.Max);
    }

    /// <summary>
    /// The joint parts on joint <paramref name="nodeId"/> among <paramref name="parts"/> that are selected in its place,
    /// in <see cref="JointSlot"/> order. Empty on a bare joint.
    /// </summary>
    public static IReadOnlyList<CreatureElementSelection> JointParts(int nodeId, IEnumerable<IPartDef> parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        return
        [
            .. parts.OfType<IJointPartDef>()
                .Where(part => part.NodeId == nodeId)
                .OrderBy(PartKinds.SlotOf)
                .Select(CreatureElementSelection.Of),
        ];
    }

    /// <summary>A node's covered radius: see the static <see cref="CoveredRadius(int, IEnumerable{IJointMotorDef})"/>.</summary>
    public double CoveredRadius(int nodeId)
    {
        NodeIndexOf(nodeId);
        return CoveredRadius(nodeId, JointMotors);
    }

    /// <summary>
    /// How far out from the joint <paramref name="nodeId"/> what is drawn over its links reaches
    /// (#1086): a motor on it, or else its hub, ring or Wheel's hub. A Wheel's face lies under its
    /// links, so it covers nothing of them.
    /// </summary>
    public static double CoveredRadius(int nodeId, IEnumerable<IJointMotorDef> motors)
    {
        ArgumentNullException.ThrowIfNull(motors);
        return motors.Any(motor => motor.NodeId == nodeId) ? JointMotor.Radius : NodeDef.HubRadius;
    }

    /// <summary>Whether the joint <paramref name="nodeId"/> holds a joint part among <paramref name="parts"/>, in any slot (#1044).</summary>
    public static bool HasJointPart(int nodeId, IEnumerable<IPartDef> parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        return parts.OfType<IJointPartDef>().Any(part => part.NodeId == nodeId);
    }

    /// <summary>The beam, Piston or Spring with <paramref name="linkId"/>.</summary>
    public LinkRef Link(int linkId) => LinkRef.Find(Links(), linkId);

    /// <summary>The beams, Pistons and Springs touching <paramref name="nodeId"/>, in part-list order.</summary>
    public IReadOnlyList<LinkRef> LinksAt(int nodeId)
    {
        NodeIndexOf(nodeId);
        return [.. Links().Where(link => link.Touches(nodeId))];
    }

    private IEnumerable<LinkRef> Links() => LinkRef.All(_beams, _pistons, _springs);

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

    private static IPartDef[] Join(params IReadOnlyList<IPartDef>[] lists)
    {
        foreach (var list in lists)
        {
            ArgumentNullException.ThrowIfNull(list, nameof(lists));
        }

        return [.. lists.SelectMany(list => list)];
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

    private static void ValidateJointPart(IJointPartDef part, HashSet<int> nodeIds, HashSet<(int NodeId, JointSlot Slot)> filledSlots)
    {
        ValidateNodeId(part.NodeId, nodeIds);
        var slot = PartKinds.SlotOf(part);
        if (!filledSlots.Add((part.NodeId, slot)))
        {
            var slotName = slot switch
            {
                JointSlot.HingePart => "hinge part",
                JointSlot.Wheel => "Wheel",
                JointSlot.Rubber => "Rubber",
                JointSlot.TouchSensor => "Touch sensor",
                _ => throw new ArgumentOutOfRangeException(nameof(part), slot, "Unknown joint slot."),
            };
            throw new ArgumentException($"Node id {part.NodeId} already has a {slotName}; a joint holds at most one.");
        }
    }

    private static void ValidateBeamId(int beamId, HashSet<int> beamIds)
    {
        if (!beamIds.Contains(beamId))
        {
            throw new ArgumentOutOfRangeException(nameof(beamId), "Beam id must point to an existing beam.");
        }
    }

    // A hinge part's side is a link touching its joint, or the Wheel on it (#1118).
    private static void ValidateHingePartSide(int? linkId, int nodeId, IReadOnlyList<LinkRef> links, IReadOnlyList<WheelDef> wheels)
    {
        if (linkId is null)
        {
            return;
        }

        if (!links.Any(link => link.Id == linkId.Value && link.Touches(nodeId)) && !wheels.Any(wheel => wheel.Id == linkId.Value && wheel.NodeId == nodeId))
        {
            throw new ArgumentException($"Link id {linkId.Value} must touch node id {nodeId}, or be its Wheel, for that hinge part.");
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
