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

    [Fact]
    public void EverySettingsKind_HasExactlyOneEntry()
    {
        PartKindDisplays.All.Select(display => display.SettingsKind).ShouldBe(Enum.GetValues<PartSettingsKind>(), ignoreOrder: true);

        foreach (var display in PartKindDisplays.All)
        {
            PartKindDisplays.Of(display.SettingsKind).ShouldBeSameAs(display);
        }
    }

    [Theory]
    [InlineData(PartKind.Node, PartSettingsKind.Node, "Joint")]
    [InlineData(PartKind.Beam, PartSettingsKind.Beam, "Beam")]
    [InlineData(PartKind.Accelerometer, PartSettingsKind.Accelerometer, "Accelerometer")]
    [InlineData(PartKind.Camera, PartSettingsKind.Camera, "Camera")]
    [InlineData(PartKind.Servo, PartSettingsKind.Servo, "Servo")]
    [InlineData(PartKind.Piston, PartSettingsKind.Piston, "Piston")]
    [InlineData(PartKind.Spring, PartSettingsKind.Spring, "Spring")]
    [InlineData(PartKind.Wheel, PartSettingsKind.Wheel, "Wheel")]
    [InlineData(PartKind.TouchSensor, PartSettingsKind.Touch, "Touch sensor")]
    [InlineData(PartKind.Rubber, PartSettingsKind.Rubber, "Rubber")]
    [InlineData(PartKind.VelocityMotor, PartSettingsKind.VelocityMotor, "Motor")]
    [InlineData(PartKind.Brake, PartSettingsKind.Brake, "Brake")]
    [InlineData(PartKind.AngleSensor, PartSettingsKind.AngleSensor, "Angle sensor")]
    public void EachKind_HasItsSettingsKindAndTitle(PartKind kind, PartSettingsKind settingsKind, string title)
    {
        PartKindDisplays.Of(kind).SettingsKind.ShouldBe(settingsKind);
        PartKindDisplays.Of(settingsKind).Title.ShouldBe(UiText.Plain(title));
    }

    // The port checks here and in BrainPortDisplayTests see only the kinds this creature holds.
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

        foreach (var channel in BrainPorts.CameraChannels(rays))
        {
            Should.NotThrow(() => camera.Port(BrainPort.Input(PortedCreature.Camera, channel)), channel);
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
