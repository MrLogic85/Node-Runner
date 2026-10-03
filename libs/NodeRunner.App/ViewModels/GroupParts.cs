using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

/// <summary>The beams, sensors and Pistons a group of joints carries (see <see cref="BuildViewModel.PartsWithin"/>).</summary>
public sealed record GroupParts(IReadOnlySet<int> Beams, IReadOnlySet<int> Sensors, IReadOnlySet<int> Pistons)
{
    public static readonly GroupParts None = new(new HashSet<int>(), new HashSet<int>(), new HashSet<int>());

    public int Count => Beams.Count + Sensors.Count + Pistons.Count;

    /// <summary>Each beam with both joints in <paramref name="nodeIds"/>, its sensor, and each Piston between two of them.</summary>
    public static GroupParts Within(
        IReadOnlyCollection<int> nodeIds,
        IEnumerable<BeamDef> beams,
        IEnumerable<SensorDef> sensors,
        IEnumerable<PistonDef> pistons)
    {
        ArgumentNullException.ThrowIfNull(nodeIds);
        ArgumentNullException.ThrowIfNull(beams);
        ArgumentNullException.ThrowIfNull(sensors);
        ArgumentNullException.ThrowIfNull(pistons);
        var nodes = nodeIds.ToHashSet();
        var carried = beams.Where(beam => nodes.Contains(beam.NodeA) && nodes.Contains(beam.NodeB)).Select(beam => beam.Id).ToHashSet();
        return new GroupParts(
            carried,
            sensors.Where(sensor => carried.Contains(sensor.BeamId)).Select(sensor => sensor.Id).ToHashSet(),
            pistons.Where(piston => nodes.Contains(piston.NodeA) && nodes.Contains(piston.NodeB)).Select(piston => piston.Id).ToHashSet());
    }
}
