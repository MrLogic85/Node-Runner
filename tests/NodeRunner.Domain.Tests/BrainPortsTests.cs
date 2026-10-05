using System.Text.Json;

namespace NodeRunner.Domain.Tests;

public sealed class BrainPortsTests
{
    [Fact]
    public void Of_OrdersPortsByPartIdThenTheirDeclaredOrder()
    {
        var layout = BrainPorts.Of(Chain());

        layout.Inputs.ShouldBe(
        [
            BrainPort.Input(6, "length"),
            BrainPort.Input(6, "speed"),
            BrainPort.Input(7, "along"),
            BrainPort.Input(7, "across"),
            BrainPort.Input(8, "left1"),
            BrainPort.Input(8, "centre"),
            BrainPort.Input(8, "right1"),
        ]);
        layout.Outputs.ShouldBe([BrainPort.Output(6, "position", PortSignal.Position), BrainPort.Output(6, "strength", PortSignal.Strength)]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Of_GivesPassiveJointsNoPorts(bool closedTriangle)
    {
        BeamDef[] beams = closedTriangle
            ? [new BeamDef(4, 1, 2), new BeamDef(5, 2, 3), new BeamDef(6, 3, 1)]
            : [new BeamDef(4, 1, 2), new BeamDef(5, 2, 3)];
        var creature = new CreatureDef(
            [
                new NodeDef(1, new Vector2D(0, 0)),
                new NodeDef(2, new Vector2D(1, 0)),
                new NodeDef(3, new Vector2D(0, 1)),
            ],
            beams,
            []);

        BrainPorts.Of(creature).ShouldBe(BrainPortLayout.Empty, new LayoutComparer());
    }

    [Fact]
    public void Of_GivesSpringsNoPorts()
    {
        var creature = new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(1, 0))],
            [],
            [],
            [],
            [new SpringDef(3, 1, 2)],
            nextPartId: 4);

        BrainPorts.Of(creature).ShouldBe(BrainPortLayout.Empty, new LayoutComparer());
    }

    [Fact]
    public void Of_IgnoresWhereThePartsAreAndTheOrderOfNodesAndSensors()
    {
        var chain = Chain();
        var moved = new CreatureDef(
            chain.Nodes.Reverse().Select(node => new NodeDef(node.Id, new Vector2D(node.Position.X * 3, node.Position.Y - 40))).ToArray(),
            chain.Beams,
            chain.Sensors.Reverse().ToArray(),
            chain.Pistons);

        BrainPorts.Of(moved).ShouldBe(BrainPorts.Of(chain), new LayoutComparer());
    }

    [Fact]
    public void Of_KeepsTheOrderOfExistingPortsWhenAPartIsAdded()
    {
        var chain = Chain();
        var grown = new CreatureDef(
            [.. chain.Nodes, new NodeDef(9, new Vector2D(3, 0))],
            [.. chain.Beams, new BeamDef(10, 3, 9)],
            chain.Sensors,
            [new PistonDef(11, 2, 9), .. chain.Pistons]);

        var before = BrainPorts.Of(chain);
        var after = BrainPorts.Of(grown);

        after.Inputs.Where(before.Inputs.Contains).ShouldBe(before.Inputs);
        after.Outputs.Where(before.Outputs.Contains).ShouldBe(before.Outputs);
        after.Outputs.ShouldContain(BrainPort.Output(11, "position", PortSignal.Position));
    }

    [Fact]
    public void Of_IsTheSameAfterASaveAndLoad()
    {
        var chain = Chain();

        var loaded = JsonSerializer.Deserialize<CreatureDef>(JsonSerializer.Serialize(chain))!;

        BrainPorts.Of(loaded).ShouldBe(BrainPorts.Of(chain), new LayoutComparer());
    }

    [Fact]
    public void Of_GivesEachPistonLengthAndSpeedInputs_AndPositionAndStrengthOutputs()
    {
        var creature = new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(1, 0))],
            [],
            [],
            [new PistonDef(3, 1, 2)]);

        var layout = BrainPorts.Of(creature);

        layout.Inputs.ShouldBe([BrainPort.Input(3, "length"), BrainPort.Input(3, "speed")]);
        layout.Outputs.ShouldBe([BrainPort.Output(3, "position", PortSignal.Position), BrainPort.Output(3, "strength", PortSignal.Strength)]);
    }

    [Fact]
    public void Of_GivesEachServoAngleAndSpeedInputs_AndAngleAndStrengthOutputs()
    {
        var creature = new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(1, 0)), new NodeDef(3, new Vector2D(0, 1))],
            [new BeamDef(4, 1, 2), new BeamDef(5, 1, 3)],
            [],
            [new ServoDef(6, 1, 4, 5)],
            [],
            [],
            nextPartId: 7);

        var layout = BrainPorts.Of(creature);

        layout.Inputs.ShouldBe([BrainPort.Input(6, "angle"), BrainPort.Input(6, "speed")]);
        layout.Outputs.ShouldBe([BrainPort.Output(6, "angle", PortSignal.Position), BrainPort.Output(6, "strength", PortSignal.Strength)]);
    }

    [Fact]
    public void SensorPorts_UseTheSavedChannelKeys()
    {
        BrainPorts.AccelerometerChannels.ShouldBe(["along", "across"]);
        BrainPorts.CameraChannels.ShouldBe(["left1", "centre", "right1"]);
        BrainPorts.SensorPorts(new SensorDef(3, 1, SensorKind.Accelerometer)).Select(port => port.Channel)
            .ShouldBe(BrainPorts.AccelerometerChannels);
        BrainPorts.SensorPorts(new SensorDef(3, 1, SensorKind.Camera)).Select(port => port.Channel)
            .ShouldBe(BrainPorts.CameraChannels);
    }

    // Three nodes in a row with passive joints: a Piston (6) links the end nodes, an
    // Accelerometer (7) sits on beam 4 and a Camera (8) on beam 5.
    private static CreatureDef Chain() => new(
        [
            new NodeDef(1, new Vector2D(0, 0)),
            new NodeDef(2, new Vector2D(1, 0)),
            new NodeDef(3, new Vector2D(2, 0)),
        ],
        [new BeamDef(4, 1, 2), new BeamDef(5, 2, 3)],
        [new SensorDef(7, 4, SensorKind.Accelerometer), new SensorDef(8, 5, SensorKind.Camera)],
        [new PistonDef(6, 1, 3)]);

    private sealed class LayoutComparer : IEqualityComparer<BrainPortLayout>
    {
        public bool Equals(BrainPortLayout? x, BrainPortLayout? y) =>
            x is not null && y is not null && x.Inputs.SequenceEqual(y.Inputs) && x.Outputs.SequenceEqual(y.Outputs);

        public int GetHashCode(BrainPortLayout obj) => obj.Inputs.Count;
    }
}
