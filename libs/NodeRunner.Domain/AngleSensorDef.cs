namespace NodeRunner.Domain;

/// <summary>
/// An Angle sensor (#1201, #1204): a joint sensor on one node that tells the brain the angle between
/// its Fixed and Target sides and how fast it changes (<c>NodeRunner.Mechanics.AngleSensor</c>). It fills
/// the joint's motor-or-brake slot and takes its sides by the Servo's rules (<see cref="IMotorOrBrakeDef"/>),
/// but it is no joint motor: it has inputs only and applies no force, so it is not in
/// <see cref="CreatureDef.JointMotors"/>. It has no settings. See docs/CREATURE_MODEL.md → "Angle sensor".
/// </summary>
public sealed record AngleSensorDef : IMotorOrBrakeDef
{
    public AngleSensorDef(int id, int nodeId, int? fixedLinkId = null, int? targetLinkId = null, string? name = null)
    {
        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Angle sensor id must be positive.");
        }

        if (nodeId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nodeId), "Node id must be positive.");
        }

        if (fixedLinkId is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fixedLinkId), "Link id must be positive when present.");
        }

        if (targetLinkId is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(targetLinkId), "Link id must be positive when present.");
        }

        if (fixedLinkId is { } fixedId && targetLinkId == fixedId)
        {
            throw new ArgumentException("An angle sensor's fixed and target links must be different.");
        }

        Id = id;
        NodeId = nodeId;
        FixedLinkId = fixedLinkId;
        TargetLinkId = targetLinkId;
        Name = name;
    }

    public int Id { get; }

    public int NodeId { get; }

    /// <summary>The side measured from: a link at the joint or the joint's Wheel (#1118), or null once deleted.</summary>
    public int? FixedLinkId { get; }

    /// <summary>The side whose angle from the Fixed side it reads: a link at the joint or the joint's Wheel (#1118), or null once deleted.</summary>
    public int? TargetLinkId { get; }

    public string? Name { get; }

    CreatureElementKind IMotorOrBrakeDef.Kind => CreatureElementKind.AngleSensor;

    public AngleSensorDef WithName(string? name) => new(Id, NodeId, FixedLinkId, TargetLinkId, name);

    /// <summary>The sensor with new sides. It keeps its id, like a Servo (<see cref="ServoDef.WithLinks"/>).</summary>
    public AngleSensorDef WithLinks(int? fixedLinkId, int? targetLinkId) => new(Id, NodeId, fixedLinkId, targetLinkId, Name);

    /// <summary>The width of its arc, in canvas units: <c>stroke-signal</c>, which its picture draws it with (#1205).</summary>
    public const double ArcWidth = 2;

    /// <summary>The width of its selection halo, <c>stroke-signal</c>, as <see cref="ArcWidth"/>.</summary>
    public const double HaloWidth = 2;

    /// <summary>
    /// Its arc's centre line (#1205), shared by its picture and the Build rings that clear it (#1206):
    /// <see cref="SelectionMarks.Gap"/> outside the joint's outermost part, a Touch sensor's cushion over a
    /// Rubber's skin over its ring, a plain joint's. A Wheel on the joint changes nothing, so on its joint
    /// the arc lies over the tyre's inside and tubes. See docs/WORLD_VISUALS.md → "Angle sensor".
    /// </summary>
    public static double ArcRadius(bool rubber, bool touch) =>
        NodeDef.PlainJointRadius
        + (touch ? TouchSensorDef.Cushion : rubber ? RubberDef.Skin : 0)
        + SelectionMarks.Gap
        + (ArcWidth / 2);

    /// <summary>
    /// <see cref="ArcRadius(bool, bool)"/> on joint <paramref name="nodeId"/>, given the joints that hold a
    /// Rubber (<paramref name="rubberNodes"/>) and a Touch sensor (<paramref name="touchSensorNodes"/>).
    /// </summary>
    public static double ArcRadiusAt(int nodeId, IEnumerable<int> rubberNodes, IEnumerable<int> touchSensorNodes)
    {
        ArgumentNullException.ThrowIfNull(rubberNodes);
        ArgumentNullException.ThrowIfNull(touchSensorNodes);
        return ArcRadius(rubber: rubberNodes.Contains(nodeId), touch: touchSensorNodes.Contains(nodeId));
    }

    /// <summary>
    /// The radius its selection halo is marked round, as a joint's halo is round its edge
    /// (<see cref="SelectionMarks.JointHalo"/>): the arc's outer edge plus half the halo's width, so the
    /// halo's inner edge lies a <see cref="SelectionMarks.Gap"/> outside the arc.
    /// </summary>
    public static double MarkRadius(double arcRadius) => arcRadius + (ArcWidth / 2) + (HaloWidth / 2);

    /// <summary>Its selection halo's centre line round an arc at <paramref name="arcRadius"/>: 24 on a bare joint.</summary>
    public static double HaloRadius(double arcRadius) => SelectionMarks.JointHalo(MarkRadius(arcRadius));
}
