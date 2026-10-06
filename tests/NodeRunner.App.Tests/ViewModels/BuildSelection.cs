using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.ViewModels;

/// <summary>Test setup for a selection of exactly one part, as a tap on it with nothing selected leaves.</summary>
internal static class BuildSelection
{
    public static void SelectOnly(this BuildViewModel build, CreatureElementKind kind, int id)
    {
        var one = new HashSet<int> { id };
        build.ReplaceSelection(kind switch
        {
            CreatureElementKind.Node => PartSet.None with { Nodes = one },
            CreatureElementKind.Beam => PartSet.None with { Beams = one },
            CreatureElementKind.Sensor => PartSet.None with { Sensors = one },
            CreatureElementKind.Servo => PartSet.None with { Servos = one },
            CreatureElementKind.Piston => PartSet.None with { Pistons = one },
            CreatureElementKind.Spring => PartSet.None with { Springs = one },
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        });
    }
}
