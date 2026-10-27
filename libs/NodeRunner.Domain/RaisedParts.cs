namespace NodeRunner.Domain;

/// <summary>
/// The parts drawn on the selected surface, from <see cref="DrawGroups.Raised"/>, so the
/// drawing and a touch (<see cref="DrawGroups.PartAt"/>) raise the same parts. <see cref="Joints"/>
/// raise what is drawn over their links, a ring, hub or motor; <see cref="Wheels"/> are the joints
/// whose Wheel's face rises too, which is drawn under their links.
/// </summary>
public sealed record RaisedParts(IReadOnlySet<int> Links, IReadOnlySet<int> Joints, IReadOnlySet<int> Sensors, IReadOnlySet<int> Wheels)
{
    public static RaisedParts None { get; } = new(new HashSet<int>(), new HashSet<int>(), new HashSet<int>(), new HashSet<int>());

    /// <summary>
    /// Whether <paramref name="part"/> is drawn on the selected surface: a joint or a joint part with its
    /// joint, a Wheel's face only with <see cref="Wheels"/>, a link, or a sensor as <see cref="Sensor"/> says.
    /// </summary>
    public bool Contains(IPartDef part) => part switch
    {
        NodeDef node => Joints.Contains(node.Id),
        IJointPartDef jointPart => JointPart(jointPart.NodeId, PartKinds.SlotOf(jointPart)),
        BeamDef or PistonDef or SpringDef => Links.Contains(part.Id),
        SensorDef sensor => Sensor(sensor),
        null => throw new ArgumentNullException(nameof(part)),
        _ => throw new ArgumentOutOfRangeException(nameof(part), part.PartKind, "Every part kind is raised or not."),
    };

    /// <summary>
    /// Whether a part in <paramref name="slot"/> of joint <paramref name="nodeId"/> is raised: a Wheel's face
    /// with <see cref="Wheels"/>, any other with <see cref="Joints"/>.
    /// </summary>
    public bool JointPart(int nodeId, JointSlot slot) => (slot == JointSlot.Wheel ? Wheels : Joints).Contains(nodeId);

    /// <summary>Whether <paramref name="sensor"/> is raised: selected, or on a raised beam.</summary>
    public bool Sensor(SensorDef sensor)
    {
        ArgumentNullException.ThrowIfNull(sensor);
        return Sensors.Contains(sensor.Id) || Links.Contains(sensor.BeamId);
    }
}
