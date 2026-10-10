using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

/// <summary>The joints, beams, sensors, Servos, Pistons, Springs and Wheels a Select selection or box holds (#704).</summary>
public sealed record PartSet(IReadOnlySet<int> Nodes, IReadOnlySet<int> Beams, IReadOnlySet<int> Sensors, IReadOnlySet<int> Servos, IReadOnlySet<int> Pistons, IReadOnlySet<int> Springs, IReadOnlySet<int> Wheels)
{
    public static readonly PartSet None = new(new HashSet<int>(), new HashSet<int>(), new HashSet<int>(), new HashSet<int>(), new HashSet<int>(), new HashSet<int>(), new HashSet<int>());

    public int Count => Nodes.Count + Beams.Count + Sensors.Count + Servos.Count + Pistons.Count + Springs.Count + Wheels.Count;

    /// <summary>Every part in the set, kind by kind.</summary>
    public IEnumerable<CreatureElementSelection> Parts =>
        Enum.GetValues<CreatureElementKind>().SelectMany(kind => SetOf(kind).Select(id => new CreatureElementSelection(kind, id)));

    /// <summary>A set of just <paramref name="part"/>.</summary>
    public static PartSet Of(CreatureElementSelection part)
    {
        ArgumentNullException.ThrowIfNull(part);
        var ids = new HashSet<int> { part.Id };
        return part.Kind switch
        {
            CreatureElementKind.Node => None with { Nodes = ids },
            CreatureElementKind.Beam => None with { Beams = ids },
            CreatureElementKind.Sensor => None with { Sensors = ids },
            CreatureElementKind.Servo => None with { Servos = ids },
            CreatureElementKind.Piston => None with { Pistons = ids },
            CreatureElementKind.Spring => None with { Springs = ids },
            CreatureElementKind.Wheel => None with { Wheels = ids },
            _ => throw new ArgumentOutOfRangeException(nameof(part)),
        };
    }

    public IReadOnlySet<int> SetOf(CreatureElementKind kind) => kind switch
    {
        CreatureElementKind.Node => Nodes,
        CreatureElementKind.Beam => Beams,
        CreatureElementKind.Sensor => Sensors,
        CreatureElementKind.Servo => Servos,
        CreatureElementKind.Piston => Pistons,
        CreatureElementKind.Spring => Springs,
        CreatureElementKind.Wheel => Wheels,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}
