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
            BrainPort.Input(2, "angle:5"),
            BrainPort.Input(2, "speed:5"),
            BrainPort.Input(6, "along"),
            BrainPort.Input(6, "across"),
            BrainPort.Input(7, "left1"),
            BrainPort.Input(7, "centre"),
            BrainPort.Input(7, "right1"),
        ]);
        layout.Outputs.ShouldBe([BrainPort.Output(2, "target:5", PortSignal.Velocity)]);
    }

    [Fact]
    public void Of_GivesEachMotorAtAJointItsOwnPortsInBeamIdOrder()
    {
        // Node 1 joins three beams that don't close a triangle: two motors, against beam 10.
        var creature = new CreatureDef(
            [
                new NodeDef(1, new Vector2D(0, 0), 1),
                new NodeDef(2, new Vector2D(1, 0), 1),
                new NodeDef(3, new Vector2D(0, 1), 1),
                new NodeDef(4, new Vector2D(-1, 0), 1),
            ],
            [new BeamDef(10, 1, 2), new BeamDef(12, 1, 3), new BeamDef(11, 1, 4)],
            []);

        var layout = BrainPorts.Of(creature);

        layout.Inputs.Select(port => port.Channel).ShouldBe(["angle:11", "speed:11", "angle:12", "speed:12"]);
        layout.Outputs.Select(port => port.Channel).ShouldBe(["target:11", "target:12"]);
        layout.Inputs.Concat(layout.Outputs).ShouldAllBe(port => port.PartId == 1);
    }

    [Fact]
    public void Of_GivesARigidTriangleNoPorts()
    {
        var creature = new CreatureDef(
            [
                new NodeDef(1, new Vector2D(0, 0), 1),
                new NodeDef(2, new Vector2D(1, 0), 1),
                new NodeDef(3, new Vector2D(0, 1), 1),
            ],
            [new BeamDef(4, 1, 2), new BeamDef(5, 2, 3), new BeamDef(6, 3, 1)],
            []);

        BrainPorts.Of(creature).ShouldBe(BrainPortLayout.Empty, new LayoutComparer());
    }

    [Fact]
    public void Of_IgnoresWhereThePartsAreAndTheOrderOfNodesAndSensors()
    {
        var chain = Chain();
        var moved = new CreatureDef(
            chain.Nodes.Reverse().Select(node => new NodeDef(node.Id, new Vector2D(node.Position.X * 3, node.Position.Y - 40), node.Radius)).ToArray(),
            chain.Beams,
            chain.Sensors.Reverse().ToArray());

        BrainPorts.Of(moved).ShouldBe(BrainPorts.Of(chain), new LayoutComparer());
    }

    [Fact]
    public void Of_KeepsTheOrderOfExistingPortsWhenAPartIsAdded()
    {
        var chain = Chain();
        var grown = new CreatureDef(
            [.. chain.Nodes, new NodeDef(8, new Vector2D(3, 0), 1)],
            [.. chain.Beams, new BeamDef(9, 3, 8)],
            chain.Sensors);

        var before = BrainPorts.Of(chain);
        var after = BrainPorts.Of(grown);

        after.Inputs.Where(before.Inputs.Contains).ShouldBe(before.Inputs);
        after.Outputs.Where(before.Outputs.Contains).ShouldBe(before.Outputs);
        after.Outputs.ShouldContain(BrainPort.Output(3, "target:9", PortSignal.Velocity));
    }

    [Fact]
    public void Of_IsTheSameAfterASaveAndLoad()
    {
        var chain = Chain();

        var loaded = JsonSerializer.Deserialize<CreatureDef>(JsonSerializer.Serialize(chain))!;

        BrainPorts.Of(loaded).ShouldBe(BrainPorts.Of(chain), new LayoutComparer());
    }

    [Fact]
    public void SensorPorts_MatchTheReadingsEachSensorGives()
    {
        BrainPorts.SensorPorts(new SensorDef(3, 1, SensorKind.Accelerometer)).Count().ShouldBe(Accelerometer.ReadingNames.Count);
        BrainPorts.SensorPorts(new SensorDef(3, 1, SensorKind.Camera)).Count().ShouldBe(CameraRays.RayCount);
    }

    // Three nodes in a row: a motor at node 2 turns beam 5 against beam 4; an
    // Accelerometer (6) sits on beam 4 and a Camera (7) on beam 5.
    private static CreatureDef Chain() => new(
        [
            new NodeDef(1, new Vector2D(0, 0), 1),
            new NodeDef(2, new Vector2D(1, 0), 1),
            new NodeDef(3, new Vector2D(2, 0), 1),
        ],
        [new BeamDef(4, 1, 2), new BeamDef(5, 2, 3)],
        [new SensorDef(6, 4, SensorKind.Accelerometer), new SensorDef(7, 5, SensorKind.Camera)]);

    private sealed class LayoutComparer : IEqualityComparer<BrainPortLayout>
    {
        public bool Equals(BrainPortLayout? x, BrainPortLayout? y) =>
            x is not null && y is not null && x.Inputs.SequenceEqual(y.Inputs) && x.Outputs.SequenceEqual(y.Outputs);

        public int GetHashCode(BrainPortLayout obj) => obj.Inputs.Count;
    }
}
