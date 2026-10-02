using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

/// <summary>
/// The names parts show: a part's own name if it has one, else its default, "Node 2", "Beam 1",
/// "Piston 1" or its sensor kind. Names are labels only (#220).
/// </summary>
public static class PartNames
{
    public static string SensorKind(SensorKind kind) => kind switch
    {
        Domain.SensorKind.Accelerometer => "Accelerometer",
        Domain.SensorKind.Camera => "Camera",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    public static string Display(IReadOnlyList<NodeDef> nodes, IReadOnlyList<BeamDef> beams, IReadOnlyList<SensorDef> sensors, IReadOnlyList<PistonDef> pistons, int partId) =>
        Own(nodes, beams, sensors, pistons, partId) ?? Default(nodes, beams, sensors, pistons, partId);

    public static string Default(IReadOnlyList<NodeDef> nodes, IReadOnlyList<BeamDef> beams, IReadOnlyList<SensorDef> sensors, IReadOnlyList<PistonDef> pistons, int partId)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(beams);
        ArgumentNullException.ThrowIfNull(sensors);
        ArgumentNullException.ThrowIfNull(pistons);
        if (sensors.FirstOrDefault(sensor => sensor.Id == partId) is { } sensor)
        {
            return SensorKind(sensor.Kind);
        }

        var pistonIndex = IndexOf(pistons, piston => piston.Id == partId);
        if (pistonIndex >= 0)
        {
            return $"Piston {pistonIndex + 1}";
        }

        var beamIndex = IndexOf(beams, beam => beam.Id == partId);
        if (beamIndex >= 0)
        {
            return $"Beam {beamIndex + 1}";
        }

        var nodeIndex = IndexOf(nodes, node => node.Id == partId);
        return nodeIndex >= 0
            ? $"Node {nodeIndex + 1}"
            : throw new ArgumentOutOfRangeException(nameof(partId), "Part id must point to an existing part.");
    }

    public static string? Own(IReadOnlyList<NodeDef> nodes, IReadOnlyList<BeamDef> beams, IReadOnlyList<SensorDef> sensors, IReadOnlyList<PistonDef> pistons, int partId)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(beams);
        ArgumentNullException.ThrowIfNull(sensors);
        ArgumentNullException.ThrowIfNull(pistons);
        return nodes.FirstOrDefault(node => node.Id == partId)?.Name
            ?? beams.FirstOrDefault(beam => beam.Id == partId)?.Name
            ?? sensors.FirstOrDefault(sensor => sensor.Id == partId)?.Name
            ?? pistons.FirstOrDefault(piston => piston.Id == partId)?.Name;
    }

    private static int IndexOf<T>(IReadOnlyList<T> items, Func<T, bool> match)
    {
        for (var index = 0; index < items.Count; index++)
        {
            if (match(items[index]))
            {
                return index;
            }
        }

        return -1;
    }
}
