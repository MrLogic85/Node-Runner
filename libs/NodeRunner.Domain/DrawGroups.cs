
namespace NodeRunner.Domain;

/// <summary>
/// The back-to-front draw groups of a creature's parts (#1107), from its built shape so the order
/// never changes while it moves. Each link is a group; the link whose lower end (larger y) sits
/// highest draws first, so what is lower on screen is nearer, and ties keep link id order. A
/// joint's <see cref="DrawSlot.Under"/> parts draw with its first link and its
/// <see cref="DrawSlot.Over"/> parts with its last, so each part draws once. A touch hits the part
/// drawn on top (<see cref="PartAt"/>), ranked by <see cref="Rank"/> as the drawing ranks it.
/// </summary>
public sealed class DrawGroups
{
    /// <summary>The most draw groups a surface keeps apart; later links share the last group.</summary>
    public const int MaxGroups = 250;

    /// <summary>How many ranks one surface has: every slot of every group, then the joints joined to no link.</summary>
    public const int SurfaceRanks = (MaxGroups * DrawSlots.Count) + 1;

    private readonly Dictionary<int, int> _links;
    private readonly Dictionary<int, (int First, int Last)> _joints = [];
    private readonly Dictionary<int, LinkRef> _linkRefs;
    private readonly int[] _nodes;

    private DrawGroups(IReadOnlyDictionary<int, Vector2D> positions, IEnumerable<LinkRef> links)
    {
        _nodes = [.. positions.Keys];
        var ordered = links
            .OrderBy(link => Math.Max(positions[link.NodeA].Y, positions[link.NodeB].Y))
            .ThenBy(link => link.Id)
            .ToArray();
        _links = ordered.Select((link, group) => (link.Id, group)).ToDictionary(entry => entry.Id, entry => entry.group);
        _linkRefs = ordered.ToDictionary(link => link.Id);
        for (var group = 0; group < ordered.Length; group++)
        {
            foreach (var node in new[] { ordered[group].NodeA, ordered[group].NodeB })
            {
                _joints[node] = _joints.TryGetValue(node, out var span) ? (span.First, group) : (group, group);
            }
        }
    }

    /// <summary>
    /// Where a part in <paramref name="slot"/> of draw <paramref name="group"/> sits on its surface,
    /// back to front from 0: joints joined to no link (no group) over every group. A part on the
    /// selected surface (<paramref name="selected"/>) ranks <see cref="SurfaceRanks"/> higher.
    /// </summary>
    public static int Rank(DrawSlot slot, int? group, bool selected)
    {
        var onSurface = group is { } index
            ? (Math.Clamp(index, 0, MaxGroups - 1) * DrawSlots.Count) + (int)slot
            : MaxGroups * DrawSlots.Count;
        return selected ? SurfaceRanks + onSurface : onSurface;
    }

    public static DrawGroups Of(IEnumerable<NodeDef> nodes, IEnumerable<LinkRef> links) =>
        new(nodes.ToDictionary(node => node.Id, node => node.Position), links);

    /// <summary>The group of a link and the sensor on it.</summary>
    public int? Link(int linkId) => _links.TryGetValue(linkId, out var group) ? group : null;

    /// <summary>The group a joint's part in <paramref name="slot"/> draws in, or null for a joint joined to no link.</summary>
    public int? Joint(int nodeId, DrawSlot slot) =>
        _joints.TryGetValue(nodeId, out var span) ? slot == DrawSlot.Under ? span.First : span.Last : null;

    /// <summary>
    /// What rises to the selected surface (#1107): the <paramref name="selected"/> joints, a motor
    /// (a Servo or Velocity motor), Wheel, Touch sensor (#1045) or Rubber (#1142) as its joint, with their links; the selected links; the joints at the ends of all
    /// those links, so a link never covers the ring, hub or motor at its end; every link whose ends
    /// both rose with its sensor, so a raised motor never hides it; and the selected sensors. A
    /// Wheel's face, under its links, rises only with a selected joint, so a raised link never makes
    /// it cover the Wheel's other links (#1086). The <paramref name="targetLinks"/> and
    /// <paramref name="targetJoints"/> a placed part can take rise alone, without what they join,
    /// and a target joint's Wheel face stays down.
    /// </summary>
    public RaisedParts Raised(
        IEnumerable<CreatureElementSelection> selected,
        IEnumerable<IJointMotorDef> motors,
        IEnumerable<WheelDef> wheels,
        IEnumerable<TouchSensorDef> touchSensors,
        IEnumerable<RubberDef> rubbers,
        IEnumerable<int>? targetLinks = null,
        IEnumerable<int>? targetJoints = null)
    {
        ArgumentNullException.ThrowIfNull(selected);
        ArgumentNullException.ThrowIfNull(motors);
        ArgumentNullException.ThrowIfNull(wheels);
        ArgumentNullException.ThrowIfNull(touchSensors);
        ArgumentNullException.ThrowIfNull(rubbers);
        var parts = selected.ToArray();
        var motorNodes = motors.ToDictionary(motor => motor.Id, motor => motor.NodeId);
        var wheelNodes = wheels.ToDictionary(wheel => wheel.Id, wheel => wheel.NodeId);
        var touchNodes = touchSensors.ToDictionary(touch => touch.Id, touch => touch.NodeId);
        var rubberNodes = rubbers.ToDictionary(rubber => rubber.Id, rubber => rubber.NodeId);
        var joints = parts
            .Select(part => part.Kind switch
            {
                CreatureElementKind.Node => part.Id,
                CreatureElementKind.Servo or CreatureElementKind.VelocityMotor => motorNodes.GetValueOrDefault(part.Id, -1),
                CreatureElementKind.Wheel => wheelNodes.GetValueOrDefault(part.Id, -1),
                CreatureElementKind.TouchSensor => touchNodes.GetValueOrDefault(part.Id, -1),
                CreatureElementKind.Rubber => rubberNodes.GetValueOrDefault(part.Id, -1),
                _ => -1,
            })
            .Where(nodeId => nodeId >= 0)
            .ToHashSet();
        var wheelJoints = joints.ToHashSet();
        var links = _linkRefs.Values.Where(link => joints.Contains(link.NodeA) || joints.Contains(link.NodeB)).Select(link => link.Id).ToHashSet();
        links.UnionWith(parts.Where(part => part.Kind is CreatureElementKind.Beam or CreatureElementKind.Piston or CreatureElementKind.Spring && _linkRefs.ContainsKey(part.Id)).Select(part => part.Id));
        joints.UnionWith(links.SelectMany(id => new[] { _linkRefs[id].NodeA, _linkRefs[id].NodeB }));
        links.UnionWith(_linkRefs.Values.Where(link => joints.Contains(link.NodeA) && joints.Contains(link.NodeB)).Select(link => link.Id));
        links.UnionWith(targetLinks ?? []);
        joints.UnionWith(targetJoints ?? []);
        var sensors = parts.Where(part => part.Kind == CreatureElementKind.Sensor).Select(part => part.Id).ToHashSet();
        return new RaisedParts(links, joints, sensors, wheelJoints);
    }

    /// <summary>
    /// The part drawn on top at <paramref name="position"/> (#1107), so a touch hits what it sees,
    /// in Build and Training alike: a joint's ring or motor, or a Wheel's hub, each out to its halo
    /// (<see cref="SelectionMarks.JointHalo"/>); a sensor's picture; or a link within its drawn body
    /// (<see cref="SelectionMarks.LinkBody"/>), outside its ends' hubs, since it is drawn hub to hub
    /// (#1086), under a ring, hub or motor. A Wheel is hit only at its hub (#1086); its tyre and its
    /// see-through middle are left to a tap's last fallback, <see cref="WheelDiscAt"/>. Parts rank by <see cref="Rank"/>, the <paramref name="raised"/> ones on
    /// the selected surface. A motor (a Servo or Velocity motor) or Wheel is hit as itself; null off every part. Joint positions
    /// come from <c>nodeAt</c>, so a moving creature is hit where it is drawn now.
    /// </summary>
    public CreatureElementSelection? PartAt(
        Vector2D position,
        Func<int, Vector2D> nodeAt,
        IReadOnlyCollection<IJointMotorDef> motors,
        IReadOnlyCollection<WheelDef> wheels,
        IEnumerable<SensorDef> sensors,
        RaisedParts raised)
    {
        ArgumentNullException.ThrowIfNull(nodeAt);
        ArgumentNullException.ThrowIfNull(motors);
        ArgumentNullException.ThrowIfNull(wheels);
        ArgumentNullException.ThrowIfNull(sensors);
        ArgumentNullException.ThrowIfNull(raised);
        CreatureElementSelection? top = null;
        var topRank = -1;

        foreach (var nodeId in _nodes)
        {
            var distance = Math.Sqrt(DistanceSquared(nodeAt(nodeId), position));
            var motor = motors.FirstOrDefault(motor => motor.NodeId == nodeId);
            var wheel = wheels.FirstOrDefault(wheel => wheel.NodeId == nodeId);
            var lifted = raised.Joints.Contains(nodeId);
            // A motor, a joint's ring or a Wheel's hub draws over the joint's links. A Wheel is hit only
            // at its hub, so its see-through tyre and middle never stand in the way of what lies under it.
            if (motor is not null && distance <= SelectionMarks.JointHalo(JointMotor.Radius))
            {
                Consider(new CreatureElementSelection(motor.Kind, motor.Id), Rank(DrawSlot.Over, Joint(nodeId, DrawSlot.Over), lifted));
            }
            else if (motor is null && distance <= SelectionMarks.JointHalo(NodeDef.HubRadius))
            {
                var part = wheel is null
                    ? new CreatureElementSelection(CreatureElementKind.Node, nodeId)
                    : new CreatureElementSelection(CreatureElementKind.Wheel, wheel.Id);
                Consider(part, Rank(DrawSlot.Over, Joint(nodeId, DrawSlot.Over), lifted));
            }
        }

        foreach (var sensor in sensors)
        {
            if (_linkRefs.TryGetValue(sensor.BeamId, out var beam)
                && SensorPicture.Contains(sensor.Kind, position, nodeAt(beam.NodeA), nodeAt(beam.NodeB), CreatureDef.CoveredRadius(beam.NodeA, motors), CreatureDef.CoveredRadius(beam.NodeB, motors)))
            {
                Consider(new CreatureElementSelection(CreatureElementKind.Sensor, sensor.Id), Rank(DrawSlot.Sensor, Link(beam.Id), raised.Sensor(sensor)));
            }
        }

        foreach (var link in _linkRefs.Values)
        {
            var nodeA = nodeAt(link.NodeA);
            var nodeB = nodeAt(link.NodeB);
            if (DistanceSquaredToSegment(position, nodeA, nodeB) <= SelectionMarks.LinkBody * SelectionMarks.LinkBody
                && !InHub(nodeA) && !InHub(nodeB))
            {
                Consider(new CreatureElementSelection(link.Kind, link.Id), Rank(DrawSlot.Link, Link(link.Id), raised.Links.Contains(link.Id)));
            }
        }

        return top;

        bool InHub(Vector2D joint) => DistanceSquared(joint, position) < NodeDef.HubRadius * NodeDef.HubRadius;

        void Consider(CreatureElementSelection part, int rank)
        {
            if (rank > topRank)
            {
                topRank = rank;
                top = part;
            }
        }
    }

    /// <summary>
    /// The Wheel whose disc, face and tyre out to its <see cref="WheelDef.Radius"/>, holds
    /// <paramref name="position"/> (#1143): where discs overlap, the one whose hub is nearest, then the
    /// lower id. A touch asks last, after every part <see cref="PartAt"/> and a finger's reach find,
    /// so a disc covers nothing but other discs. Null outside every disc.
    /// </summary>
    public static WheelDef? WheelDiscAt(Vector2D position, Func<int, Vector2D> nodeAt, IEnumerable<WheelDef> wheels)
    {
        ArgumentNullException.ThrowIfNull(nodeAt);
        ArgumentNullException.ThrowIfNull(wheels);
        return wheels
            .Select(wheel => (Wheel: wheel, DistanceSquared: DistanceSquared(nodeAt(wheel.NodeId), position)))
            .Where(entry => entry.DistanceSquared <= entry.Wheel.Radius * entry.Wheel.Radius)
            .OrderBy(entry => entry.DistanceSquared)
            .ThenBy(entry => entry.Wheel.Id)
            .Select(entry => entry.Wheel)
            .FirstOrDefault();
    }

    private static double DistanceSquared(Vector2D a, Vector2D b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return (dx * dx) + (dy * dy);
    }

    private static double DistanceSquaredToSegment(Vector2D point, Vector2D start, Vector2D end)
    {
        var segmentX = end.X - start.X;
        var segmentY = end.Y - start.Y;
        var lengthSquared = (segmentX * segmentX) + (segmentY * segmentY);
        var t = lengthSquared == 0 ? 0 : Math.Clamp((((point.X - start.X) * segmentX) + ((point.Y - start.Y) * segmentY)) / lengthSquared, 0, 1);
        return DistanceSquared(point, new Vector2D(start.X + (t * segmentX), start.Y + (t * segmentY)));
    }
}
