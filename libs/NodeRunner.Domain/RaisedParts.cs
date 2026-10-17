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

    /// <summary>Whether <paramref name="sensor"/> is raised: selected, or on a raised beam.</summary>
    public bool Sensor(SensorDef sensor)
    {
        ArgumentNullException.ThrowIfNull(sensor);
        return Sensors.Contains(sensor.Id) || Links.Contains(sensor.BeamId);
    }
}
