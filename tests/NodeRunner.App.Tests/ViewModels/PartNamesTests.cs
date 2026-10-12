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
        var touchSensors = Enumerable.Range(0, count).Select(index => new TouchSensorDef(7000 + index, index + 1)).ToArray();
        var rubbers = Enumerable.Range(0, count).Select(index => new RubberDef(8000 + index, index + 1)).ToArray();
        var velocityMotors = Enumerable.Range(0, count).Select(index => new VelocityMotorDef(9000 + index, index + 1)).ToArray();
        var sensors = Enum.GetValues<SensorKind>()
            .SelectMany((kind, kindIndex) => Enumerable.Range(0, count).Select(index => new SensorDef(4000 + (kindIndex * count) + index, 1000 + index, kind)))
            .ToArray();
        int[] partIds = [.. nodes.Select(node => node.Id), .. beams.Select(beam => beam.Id), .. servos.Select(servo => servo.Id), .. pistons.Select(piston => piston.Id), .. springs.Select(spring => spring.Id), .. wheels.Select(wheel => wheel.Id), .. touchSensors.Select(touch => touch.Id), .. rubbers.Select(rubber => rubber.Id), .. velocityMotors.Select(motor => motor.Id), .. sensors.Select(sensor => sensor.Id)];

        foreach (var partId in partIds)
        {
            var name = PartNames.Default(nodes, beams, sensors, servos, pistons, springs, wheels, touchSensors, rubbers, velocityMotors, partId);
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

        PartNames.Default(nodes, beams, sensors, [], [], [], [], [], [], [], 20).ShouldBe(UiText.Format("Accel {0}", 1));
        PartNames.Default(nodes, beams, sensors, [], [], [], [], [], [], [], 21).ShouldBe(UiText.Format("Camera {0}", 1));
        PartNames.Default(nodes, beams, sensors, [], [], [], [], [], [], [], 22).ShouldBe(UiText.Format("Accel {0}", 2));
    }

    [Fact]
    public void TouchSensors_AreNumberedAmongThemselves_AndShowTheirOwnName()
    {
        var nodes = Enumerable.Range(1, 3).Select(id => new NodeDef(id, new Vector2D(id * 80, 0))).ToArray();
        var touchSensors = new[] { new TouchSensorDef(20, 3), new TouchSensorDef(21, 1, "Toe") };

        PartNames.Default(nodes, [], [], [], [], [], [], touchSensors, [], [], 20).ShouldBe(UiText.Format("Touch {0}", 1));
        PartNames.Default(nodes, [], [], [], [], [], [], touchSensors, [], [], 21).ShouldBe(UiText.Format("Touch {0}", 2));
        PartNames.Display(nodes, [], [], [], [], [], [], touchSensors, [], [], 21).ShouldBe(UiText.AsWritten("Toe"));
    }

    [Fact]
    public void Rubbers_AreNumberedAmongThemselves_AndShowTheirOwnName()
    {
        var nodes = Enumerable.Range(1, 3).Select(id => new NodeDef(id, new Vector2D(id * 80, 0))).ToArray();
        var rubbers = new[] { new RubberDef(20, 3), new RubberDef(21, 1, "Sole") };

        PartNames.Default(nodes, [], [], [], [], [], [], [], rubbers, [], 20).ShouldBe(UiText.Format("Rubber {0}", 1));
        PartNames.Default(nodes, [], [], [], [], [], [], [], rubbers, [], 21).ShouldBe(UiText.Format("Rubber {0}", 2));
        PartNames.Display(nodes, [], [], [], [], [], [], [], rubbers, [], 21).ShouldBe(UiText.AsWritten("Sole"));
    }

    [Fact]
    public void VelocityMotors_AreNumberedAmongThemselves_AndShowTheirOwnName()
    {
        var nodes = Enumerable.Range(1, 3).Select(id => new NodeDef(id, new Vector2D(id * 80, 0))).ToArray();
        var velocityMotors = new[] { new VelocityMotorDef(20, 3), new VelocityMotorDef(21, 1, name: "Drive") };

        PartNames.Default(nodes, [], [], [], [], [], [], [], [], velocityMotors, 20).ShouldBe(UiText.Format("Motor {0}", 1));
        PartNames.Default(nodes, [], [], [], [], [], [], [], [], velocityMotors, 21).ShouldBe(UiText.Format("Motor {0}", 2));
        PartNames.Display(nodes, [], [], [], [], [], [], [], [], velocityMotors, 21).ShouldBe(UiText.AsWritten("Drive"));
    }

    [Fact]
    public void Display_WithANamedServo_ReturnsTheServoName()
    {
        var nodes = new[] { new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(80, 0)), new NodeDef(3, new Vector2D(0, 80)) };
        var beams = new[] { new BeamDef(10, 1, 2), new BeamDef(11, 1, 3) };
        var servos = new[] { new ServoDef(20, 1, 10, 11, "Hip") };

        PartNames.Display(nodes, beams, [], servos, [], [], [], [], [], [], 20).ShouldBe(UiText.AsWritten("Hip"));
    }
}
