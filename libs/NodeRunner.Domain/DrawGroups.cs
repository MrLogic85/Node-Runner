
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
    /// What rises to the selected surface (#1107): the <paramref name="selected"/> joints, a Servo
    /// or Wheel as its joint, with their links; the selected links; the joints at the ends of all
    /// those links, so a link never covers the ring at its end; every link whose ends both rose
    /// with its sensor, so a raised Wheel or Servo never hides it; and the selected sensors. The
    /// <paramref name="targetLinks"/> and <paramref name="targetJoints"/> a placed part can take
    /// rise alone, without what they join.
    /// </summary>
    public RaisedParts Raised(
        IEnumerable<CreatureElementSelection> selected,
        IEnumerable<ServoDef> servos,
        IEnumerable<WheelDef> wheels,
        IEnumerable<int>? targetLinks = null,
        IEnumerable<int>? targetJoints = null)
    {
        ArgumentNullException.ThrowIfNull(selected);
        ArgumentNullException.ThrowIfNull(servos);
        ArgumentNullException.ThrowIfNull(wheels);
        var parts = selected.ToArray();
        var servoNodes = servos.ToDictionary(servo => servo.Id, servo => servo.NodeId);
        var wheelNodes = wheels.ToDictionary(wheel => wheel.Id, wheel => wheel.NodeId);
        var joints = parts
            .Select(part => part.Kind switch
            {
                CreatureElementKind.Node => part.Id,
                CreatureElementKind.Servo => servoNodes.GetValueOrDefault(part.Id, -1),
                CreatureElementKind.Wheel => wheelNodes.GetValueOrDefault(part.Id, -1),
                _ => -1,
            })
            .Where(nodeId => nodeId >= 0)
            .ToHashSet();
        var links = _linkRefs.Values.Where(link => joints.Contains(link.NodeA) || joints.Contains(link.NodeB)).Select(link => link.Id).ToHashSet();
        links.UnionWith(parts.Where(part => part.Kind is CreatureElementKind.Beam or CreatureElementKind.Piston or CreatureElementKind.Spring && _linkRefs.ContainsKey(part.Id)).Select(part => part.Id));
        joints.UnionWith(links.SelectMany(id => new[] { _linkRefs[id].NodeA, _linkRefs[id].NodeB }));
        links.UnionWith(_linkRefs.Values.Where(link => joints.Contains(link.NodeA) && joints.Contains(link.NodeB)).Select(link => link.Id));
        links.UnionWith(targetLinks ?? []);
        joints.UnionWith(targetJoints ?? []);
        var sensors = parts.Where(part => part.Kind == CreatureElementKind.Sensor).Select(part => part.Id).ToHashSet();
        return new RaisedParts(links, joints, sensors);
    }

    /// <summary>
    /// The part drawn on top at <paramref name="position"/> (#1107), so a touch hits what it sees,
    /// in Build and Training alike: a joint's ring or Servo, or a Wheel's face, each out to its
    /// halo (<see cref="SelectionMarks.JointHalo"/>); a sensor's picture; or a link within its drawn
    /// body (<see cref="SelectionMarks.LinkBody"/>), outside its own ends' halos, since it is drawn
    /// rim to rim under them. Parts rank by <see cref="Rank"/>, the <paramref name="raised"/> ones on
    /// the selected surface. A Servo or Wheel is hit as itself; null off every part. Joint positions
    /// come from <c>nodeAt</c>, so a moving creature is hit where it is drawn now.
    /// </summary>
    public CreatureElementSelection? PartAt(
        Vector2D position,
        Func<int, Vector2D> nodeAt,
        IReadOnlyCollection<ServoDef> servos,
        IReadOnlyCollection<WheelDef> wheels,
        IEnumerable<SensorDef> sensors,
        RaisedParts raised)
    {
        ArgumentNullException.ThrowIfNull(nodeAt);
        ArgumentNullException.ThrowIfNull(servos);
        ArgumentNullException.ThrowIfNull(wheels);
        ArgumentNullException.ThrowIfNull(sensors);
        ArgumentNullException.ThrowIfNull(raised);
        CreatureElementSelection? top = null;
        var topRank = -1;

        foreach (var nodeId in _nodes)
        {
            var distance = Math.Sqrt(DistanceSquared(nodeAt(nodeId), position));
            var servo = servos.FirstOrDefault(servo => servo.NodeId == nodeId);
            var wheel = wheels.FirstOrDefault(wheel => wheel.NodeId == nodeId);
            var lifted = raised.Joints.Contains(nodeId);
            // A Servo or a joint's ring draws over the joint's links; a Wheel's face under them.
            if (servo is not null && distance <= SelectionMarks.JointHalo(ServoDef.JointRadius))
            {
                Consider(new CreatureElementSelection(CreatureElementKind.Servo, servo.Id), DrawSlot.Over, Joint(nodeId, DrawSlot.Over), lifted);
            }
            else if (servo is null && wheel is null && distance <= SelectionMarks.JointHalo(NodeDef.PlainJointRadius))
            {
                Consider(new CreatureElementSelection(CreatureElementKind.Node, nodeId), DrawSlot.Over, Joint(nodeId, DrawSlot.Over), lifted);
            }

            if (wheel is not null && distance <= SelectionMarks.JointHalo(wheel.Radius))
            {
                Consider(new CreatureElementSelection(CreatureElementKind.Wheel, wheel.Id), DrawSlot.Under, Joint(nodeId, DrawSlot.Under), lifted);
            }
        }

        foreach (var sensor in sensors)
        {
            if (_linkRefs.TryGetValue(sensor.BeamId, out var beam)
                && SensorPicture.Contains(sensor.Kind, position, nodeAt(beam.NodeA), nodeAt(beam.NodeB)))
            {
                Consider(new CreatureElementSelection(CreatureElementKind.Sensor, sensor.Id), DrawSlot.Sensor, Link(beam.Id), raised.Sensor(sensor));
            }
        }

        foreach (var link in _linkRefs.Values)
        {
            var nodeA = nodeAt(link.NodeA);
            var nodeB = nodeAt(link.NodeB);
            if (DistanceSquaredToSegment(position, nodeA, nodeB) <= SelectionMarks.LinkBody * SelectionMarks.LinkBody
                && !InReach(link.NodeA, nodeA) && !InReach(link.NodeB, nodeB))
            {
                Consider(new CreatureElementSelection(link.Kind, link.Id), DrawSlot.Link, Link(link.Id), raised.Links.Contains(link.Id));
            }
        }

        return top;

        bool InReach(int nodeId, Vector2D at)
        {
            var reach = SelectionMarks.JointHalo(CreatureDef.JointRadius(nodeId, servos, wheels));
            return DistanceSquared(at, position) <= reach * reach;
        }

        void Consider(CreatureElementSelection part, DrawSlot slot, int? group, bool lifted)
        {
            var rank = Rank(slot, group, lifted);
            if (rank > topRank)
            {
                topRank = rank;
                top = part;
            }
        }
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
