using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

/// <summary>The joints, beams, sensors, Servos, Pistons and Springs a Select selection or box holds (#704).</summary>
public sealed record PartSet(IReadOnlySet<int> Nodes, IReadOnlySet<int> Beams, IReadOnlySet<int> Sensors, IReadOnlySet<int> Servos, IReadOnlySet<int> Pistons, IReadOnlySet<int> Springs)
{
    public static readonly PartSet None = new(new HashSet<int>(), new HashSet<int>(), new HashSet<int>(), new HashSet<int>(), new HashSet<int>(), new HashSet<int>());

    public PartSet(IReadOnlySet<int> nodes, IReadOnlySet<int> beams, IReadOnlySet<int> sensors, IReadOnlySet<int> pistons, IReadOnlySet<int> springs)
        : this(nodes, beams, sensors, new HashSet<int>(), pistons, springs)
    {
    }

    public int Count => Nodes.Count + Beams.Count + Sensors.Count + Servos.Count + Pistons.Count + Springs.Count;

    public IReadOnlySet<int> SetOf(CreatureElementKind kind) => kind switch
    {
        CreatureElementKind.Node => Nodes,
        CreatureElementKind.Beam => Beams,
        CreatureElementKind.Sensor => Sensors,
        CreatureElementKind.Servo => Servos,
        CreatureElementKind.Piston => Pistons,
        CreatureElementKind.Spring => Springs,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}
