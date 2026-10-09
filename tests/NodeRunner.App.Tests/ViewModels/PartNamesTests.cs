using System.Globalization;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class PartNamesTests
{
    [Fact]
    public void Default_EveryKind_FitsThePartLimit_UpToNinetyNineOfAKind()
    {
        const int count = 99;
        var nodes = Enumerable.Range(1, count + 1).Select(id => new NodeDef(id, new Vector2D(id, 0))).ToArray();
        var beams = Enumerable.Range(0, count).Select(index => new BeamDef(1000 + index, index + 1, index + 2)).ToArray();
        var servos = Enumerable.Range(0, count).Select(index => new ServoDef(5000 + index, index + 1, 1000 + index, 1000 + index + 1)).ToArray();
        var pistons = Enumerable.Range(0, count).Select(index => new PistonDef(2000 + index, index + 1, index + 2)).ToArray();
        var springs = Enumerable.Range(0, count).Select(index => new SpringDef(3000 + index, index + 1, index + 2)).ToArray();
        var wheels = Enumerable.Range(0, count).Select(index => new WheelDef(6000 + index, index + 1)).ToArray();
        var sensors = Enum.GetValues<SensorKind>()
            .SelectMany((kind, kindIndex) => Enumerable.Range(0, count).Select(index => new SensorDef(4000 + (kindIndex * count) + index, 1000 + index, kind)))
            .ToArray();
        int[] partIds = [.. nodes.Select(node => node.Id), .. beams.Select(beam => beam.Id), .. servos.Select(servo => servo.Id), .. pistons.Select(piston => piston.Id), .. springs.Select(spring => spring.Id), .. wheels.Select(wheel => wheel.Id), .. sensors.Select(sensor => sensor.Id)];

        foreach (var partId in partIds)
        {
            var name = PartNames.Default(nodes, beams, sensors, servos, pistons, springs, wheels, partId);
            var shown = string.Format(CultureInfo.InvariantCulture, name.Message, [.. name.Args]);
            shown.Length.ShouldBeLessThanOrEqualTo(NameLimits.Part, shown);
        }
    }

    [Fact]
    public void Default_Sensors_AreNumberedAmongTheirOwnKind()
    {
        var nodes = Enumerable.Range(1, 4).Select(id => new NodeDef(id, new Vector2D(id * 80, 0))).ToArray();
        var beams = new[] { new BeamDef(10, 1, 2), new BeamDef(11, 2, 3), new BeamDef(12, 3, 4) };
        var sensors = new[] { new SensorDef(20, 10, SensorKind.Accelerometer), new SensorDef(21, 11, SensorKind.Camera), new SensorDef(22, 12, SensorKind.Accelerometer) };

        PartNames.Default(nodes, beams, sensors, [], [], [], [], 20).ShouldBe(UiText.Format("Accel {0}", 1));
        PartNames.Default(nodes, beams, sensors, [], [], [], [], 21).ShouldBe(UiText.Format("Camera {0}", 1));
        PartNames.Default(nodes, beams, sensors, [], [], [], [], 22).ShouldBe(UiText.Format("Accel {0}", 2));
    }

    [Fact]
    public void Display_WithANamedServo_ReturnsTheServoName()
    {
        var nodes = new[] { new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(80, 0)), new NodeDef(3, new Vector2D(0, 80)) };
        var beams = new[] { new BeamDef(10, 1, 2), new BeamDef(11, 1, 3) };
        var servos = new[] { new ServoDef(20, 1, 10, 11, "Hip") };

        PartNames.Display(nodes, beams, [], servos, [], [], [], 20).ShouldBe(UiText.AsWritten("Hip"));
    }
}
