using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

/// <summary>The joints, beams, sensors and Pistons a Select selection or box holds (#704).</summary>
public sealed record PartSet(IReadOnlySet<int> Nodes, IReadOnlySet<int> Beams, IReadOnlySet<int> Sensors, IReadOnlySet<int> Pistons)
{
    public static readonly PartSet None = new(new HashSet<int>(), new HashSet<int>(), new HashSet<int>(), new HashSet<int>());

    public int Count => Nodes.Count + Beams.Count + Sensors.Count + Pistons.Count;

    public IReadOnlySet<int> SetOf(CreatureElementKind kind) => kind switch
    {
        CreatureElementKind.Node => Nodes,
        CreatureElementKind.Beam => Beams,
        CreatureElementKind.Sensor => Sensors,
        CreatureElementKind.Piston => Pistons,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}
