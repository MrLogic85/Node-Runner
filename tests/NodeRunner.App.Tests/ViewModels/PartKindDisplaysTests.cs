using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class PartKindDisplaysTests
{
    [Fact]
    public void All_HasOneEntryPerPartKind_InItsOrder_WithItsTitleAndDefaultName()
    {
        PartKindDisplays.All.Select(display => display.Kind).ShouldBe(Enum.GetValues<PartKind>());
        Enum.GetValues<PartKind>().ShouldAllBe(kind => PartKindDisplays.Of(kind).Kind == kind);
        PartKindDisplays.All.Select(display => display.Title.Message).ShouldBe(
        [
            "Joint", "Beam", "Accelerometer", "Camera", "Servo", "Piston", "Spring", "Wheel", "Touch sensor", "Rubber",
            "Motor", "Brake", "Angle sensor", "Stepper", "Memory cell",
        ]);
        PartKindDisplays.All.Select(display => display.DefaultName(2)).ShouldBe(
            new[] { "Joint", "Beam", "Accel", "Camera", "Servo", "Piston", "Spring", "Wheel", "Touch", "Rubber", "Motor", "Brake", "Angle", "Stepper", "Memory" }
                .Select(name => UiText.Format(name + " {0}", 2)));
    }

    // The ported creature holds every part kind and a camera with the most rays, so its ports are
    // every port there is; the port checks here and in NetworkPortDisplayTests rely on that.
    [Fact]
    public void EveryEntry_IsAPortItsKindHas_AndEveryPortHasAnEntry()
    {
        PortedCreature.Def.Parts.Select(part => part.PartKind).Distinct().ShouldBe(Enum.GetValues<PartKind>(), ignoreOrder: true);
        var layout = PortedCreature.Layout;
        var ports = layout.Inputs.Concat(layout.Outputs)
            .Select(port => (PortedCreature.Def.FindPart(port.PartId)!.PartKind, port.Direction, port.Channel))
            .ToHashSet();

        PartKindDisplays.All.SelectMany(display => display.Ports.Select(entry => (display.Kind, entry.Direction, entry.Channel)))
            .ShouldBe(ports, ignoreOrder: true);
    }
}
