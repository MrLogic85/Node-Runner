using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class PartKindDisplaysTests
{
    [Fact]
    public void All_HasOneEntryPerPartKind_InItsOrder()
    {
        PartKindDisplays.All.Select(display => display.Kind).ShouldBe(Enum.GetValues<PartKind>());

        foreach (var kind in Enum.GetValues<PartKind>())
        {
            PartKindDisplays.Of(kind).Kind.ShouldBe(kind);
        }
    }

    [Theory]
    [InlineData(PartKind.Node, "Joint")]
    [InlineData(PartKind.Beam, "Beam")]
    [InlineData(PartKind.Accelerometer, "Accelerometer")]
    [InlineData(PartKind.Camera, "Camera")]
    [InlineData(PartKind.Servo, "Servo")]
    [InlineData(PartKind.Piston, "Piston")]
    [InlineData(PartKind.Spring, "Spring")]
    [InlineData(PartKind.Wheel, "Wheel")]
    [InlineData(PartKind.TouchSensor, "Touch sensor")]
    [InlineData(PartKind.Rubber, "Rubber")]
    [InlineData(PartKind.VelocityMotor, "Motor")]
    [InlineData(PartKind.Brake, "Brake")]
    [InlineData(PartKind.AngleSensor, "Angle sensor")]
    public void EachKind_HasItsTitle(PartKind kind, string title) =>
        PartKindDisplays.Of(kind).Title.ShouldBe(UiText.Plain(title));

    // The port checks here and in NetworkPortDisplayTests see only the kinds this creature holds.
    [Fact]
    public void ThePortedCreature_HoldsEveryPartKind() =>
        PortedCreature.Def.Parts.Select(part => part.PartKind).Distinct().ShouldBe(Enum.GetValues<PartKind>(), ignoreOrder: true);

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void EveryCameraRay_HasAnEntry(int rays)
    {
        var camera = PartKindDisplays.Of(PartKind.Camera);

        foreach (var channel in NetworkPorts.CameraChannels(rays))
        {
            Should.NotThrow(() => camera.Port(NetworkPort.Input(PortedCreature.Camera, channel)), channel);
        }
    }

    [Fact]
    public void EveryEntry_IsAPortItsKindHas()
    {
        var layout = PortedCreature.Layout;
        var ports = layout.Inputs.Concat(layout.Outputs)
            .Select(port => (PortedCreature.Def.FindPart(port.PartId)!.PartKind, port.Direction, port.Channel))
            .ToHashSet();

        foreach (var display in PartKindDisplays.All)
        {
            foreach (var entry in display.Ports)
            {
                ports.ShouldContain((display.Kind, entry.Direction, entry.Channel), $"{display.Kind} {entry.Direction} {entry.Channel}");
            }
        }
    }
}
