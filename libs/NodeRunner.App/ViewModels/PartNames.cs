using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

/// <summary>
/// The names parts show: a part's own name if it has one, else its default, "Node 2", "Beam 1",
/// "Piston 1", "Spring 1", "Accel" or "Camera". A default fits in <see cref="NameLimits.Part"/>
/// (#868). Names are labels only (#220). A default name is translated; an own name is the
/// player's and shows as written (#757).
/// </summary>
public static class PartNames
{
    private static UiText SensorKind(SensorKind kind) => kind switch
    {
        Domain.SensorKind.Accelerometer => UiText.Plain("Accel"),
        Domain.SensorKind.Camera => UiText.Plain("Camera"),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    public static UiText Display(IReadOnlyList<NodeDef> nodes, IReadOnlyList<BeamDef> beams, IReadOnlyList<SensorDef> sensors, IReadOnlyList<PistonDef> pistons, IReadOnlyList<SpringDef> springs, int partId) =>
        Own(nodes, beams, sensors, pistons, springs, partId) is { } own
            ? UiText.AsWritten(own)
            : Default(nodes, beams, sensors, pistons, springs, partId);

    public static UiText Default(IReadOnlyList<NodeDef> nodes, IReadOnlyList<BeamDef> beams, IReadOnlyList<SensorDef> sensors, IReadOnlyList<PistonDef> pistons, IReadOnlyList<SpringDef> springs, int partId)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(beams);
        ArgumentNullException.ThrowIfNull(sensors);
        ArgumentNullException.ThrowIfNull(pistons);
        ArgumentNullException.ThrowIfNull(springs);
        if (sensors.FirstOrDefault(sensor => sensor.Id == partId) is { } sensor)
        {
            return SensorKind(sensor.Kind);
        }

        var pistonIndex = IndexOf(pistons, piston => piston.Id == partId);
        if (pistonIndex >= 0)
        {
            return UiText.Format("Piston {0}", pistonIndex + 1);
        }

        var springIndex = IndexOf(springs, spring => spring.Id == partId);
        if (springIndex >= 0)
        {
            return UiText.Format("Spring {0}", springIndex + 1);
        }

        var beamIndex = IndexOf(beams, beam => beam.Id == partId);
        if (beamIndex >= 0)
        {
            return UiText.Format("Beam {0}", beamIndex + 1);
        }

        var nodeIndex = IndexOf(nodes, node => node.Id == partId);
        return nodeIndex >= 0
            ? UiText.Format("Node {0}", nodeIndex + 1)
            : throw new ArgumentOutOfRangeException(nameof(partId), "Part id must point to an existing part.");
    }

    public static string? Own(IReadOnlyList<NodeDef> nodes, IReadOnlyList<BeamDef> beams, IReadOnlyList<SensorDef> sensors, IReadOnlyList<PistonDef> pistons, IReadOnlyList<SpringDef> springs, int partId)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(beams);
        ArgumentNullException.ThrowIfNull(sensors);
        ArgumentNullException.ThrowIfNull(pistons);
        ArgumentNullException.ThrowIfNull(springs);
        return nodes.FirstOrDefault(node => node.Id == partId)?.Name
            ?? beams.FirstOrDefault(beam => beam.Id == partId)?.Name
            ?? sensors.FirstOrDefault(sensor => sensor.Id == partId)?.Name
            ?? pistons.FirstOrDefault(piston => piston.Id == partId)?.Name
            ?? springs.FirstOrDefault(spring => spring.Id == partId)?.Name;
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
