using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

/// <summary>The joints, beams, sensors, Pistons and Springs a Select selection or box holds (#704).</summary>
public sealed record PartSet(IReadOnlySet<int> Nodes, IReadOnlySet<int> Beams, IReadOnlySet<int> Sensors, IReadOnlySet<int> Pistons, IReadOnlySet<int> Springs)
{
    public static readonly PartSet None = new(new HashSet<int>(), new HashSet<int>(), new HashSet<int>(), new HashSet<int>(), new HashSet<int>());

    public int Count => Nodes.Count + Beams.Count + Sensors.Count + Pistons.Count + Springs.Count;

    public IReadOnlySet<int> SetOf(CreatureElementKind kind) => kind switch
    {
        CreatureElementKind.Node => Nodes,
        CreatureElementKind.Beam => Beams,
        CreatureElementKind.Sensor => Sensors,
        CreatureElementKind.Piston => Pistons,
        CreatureElementKind.Spring => Springs,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}
