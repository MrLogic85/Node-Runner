using System.Text.Json;

namespace NodeRunner.Domain.Tests;

public sealed class NetworkPortsTests
{
    [Fact]
    public void Of_OrdersPortsByPartIdThenTheirDeclaredOrder()
    {
        var layout = NetworkPorts.Of(Chain());

        layout.Inputs.ShouldBe(
        [
            NetworkPort.Input(6, "length"),
            NetworkPort.Input(6, "speed"),
            NetworkPort.Input(7, "along"),
            NetworkPort.Input(7, "across"),
            NetworkPort.Input(8, "left1"),
            NetworkPort.Input(8, "centre"),
            NetworkPort.Input(8, "right1"),
            NetworkPort.Input(8, "hit"),
        ]);
        layout.Outputs.ShouldBe([NetworkPort.Output(6, "position", PortSignal.Length), NetworkPort.Output(6, "strength", PortSignal.Strength)]);
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

        NetworkPorts.Of(creature).ShouldBe(NetworkPortLayout.Empty, new LayoutComparer());
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

        NetworkPorts.Of(creature).ShouldBe(NetworkPortLayout.Empty, new LayoutComparer());
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

        NetworkPorts.Of(moved).ShouldBe(NetworkPorts.Of(chain), new LayoutComparer());
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

        var before = NetworkPorts.Of(chain);
        var after = NetworkPorts.Of(grown);

        after.Inputs.Where(before.Inputs.Contains).ShouldBe(before.Inputs);
        after.Outputs.Where(before.Outputs.Contains).ShouldBe(before.Outputs);
        after.Outputs.ShouldContain(NetworkPort.Output(11, "position", PortSignal.Length));
    }

    [Fact]
    public void Of_IsTheSameAfterASaveAndLoad()
    {
        var chain = Chain();

        var loaded = JsonSerializer.Deserialize<CreatureDef>(JsonSerializer.Serialize(chain))!;

        NetworkPorts.Of(loaded).ShouldBe(NetworkPorts.Of(chain), new LayoutComparer());
    }

    [Fact]
    public void Of_GivesEachPistonLengthAndSpeedInputs_AndPositionAndStrengthOutputs()
    {
        var creature = new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(1, 0))],
            [],
            [],
            [new PistonDef(3, 1, 2)]);

        var layout = NetworkPorts.Of(creature);

        layout.Inputs.ShouldBe([NetworkPort.Input(3, "length"), NetworkPort.Input(3, "speed")]);
        layout.Outputs.ShouldBe([NetworkPort.Output(3, "position", PortSignal.Length), NetworkPort.Output(3, "strength", PortSignal.Strength)]);
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

        var layout = NetworkPorts.Of(creature);

        layout.Inputs.ShouldBe([NetworkPort.Input(6, "angle"), NetworkPort.Input(6, "speed")]);
        layout.Outputs.ShouldBe([NetworkPort.Output(6, "angle", PortSignal.Position), NetworkPort.Output(6, "strength", PortSignal.Strength)]);
    }

    [Fact]
    public void Of_GivesAVelocityMotorAngleAndSpeedInputs_AndSpeedAndStrengthOutputs_InPartIdOrder()
    {
        NodeDef[] nodes = [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(1, 0)), new NodeDef(3, new Vector2D(0, 1))];
        var creature = new CreatureDef(
            nodes,
            [new BeamDef(4, 1, 2), new BeamDef(5, 1, 3)],
            [],
            [new ServoDef(8, 2, 4)],
            [],
            [],
            [],
            [],
            [],
            [new VelocityMotorDef(6, 1, 4, 5)],
            [],
            nextPartId: 9);

        var layout = NetworkPorts.Of(creature);

        layout.Inputs.ShouldBe(
        [
            NetworkPort.Input(6, "angleSin"),
            NetworkPort.Input(6, "angleCos"),
            NetworkPort.Input(6, "speed"),
            NetworkPort.Input(8, "angle"),
            NetworkPort.Input(8, "speed"),
        ]);
        layout.Outputs.ShouldBe(
        [
            NetworkPort.Output(6, "speed", PortSignal.Velocity),
            NetworkPort.Output(6, "strength", PortSignal.Strength),
            NetworkPort.Output(8, "angle", PortSignal.Position),
            NetworkPort.Output(8, "strength", PortSignal.Strength),
        ]);
    }

    // Inputs only, in this order: it applies no force, so it has no outputs (#1204).
    [Fact]
    public void Of_GivesAnAngleSensorAngleSinAngleCosAndSpeedInputs_AndNoOutputs_InPartIdOrder()
    {
        NodeDef[] nodes = [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(100, 0)), new NodeDef(3, new Vector2D(0, 100))];
        BeamDef[] beams = [new BeamDef(4, 1, 2), new BeamDef(5, 1, 3)];
        var without = new CreatureDef(nodes, beams, [], [new ServoDef(6, 2, 4)], [], [], [], [], [], [], [], [], nextPartId: 9);
        var with = new CreatureDef(nodes, beams, [], [new ServoDef(6, 2, 4)], [], [], [], [], [], [], [], [new AngleSensorDef(7, 1, 4, 5)], nextPartId: 9);

        var layout = NetworkPorts.Of(with);

        layout.Inputs.ShouldBe(
        [
            NetworkPort.Input(6, "angle"),
            NetworkPort.Input(6, "speed"),
            NetworkPort.Input(7, "angleSin"),
            NetworkPort.Input(7, "angleCos"),
            NetworkPort.Input(7, "speed"),
        ]);
        NetworkPorts.AngleSensorInputs(7).ShouldBe([NetworkPort.Input(7, "angleSin"), NetworkPort.Input(7, "angleCos"), NetworkPort.Input(7, "speed")]);
        layout.Outputs.ShouldBe(NetworkPorts.Of(without).Outputs);
    }

    // #1036, #1263: the angle between its sides and its tick in, its signed frequency out.
    [Fact]
    public void Of_GivesAStepperAngleAndTickInputs_AndAFrequencyOutput_InPartIdOrder()
    {
        NodeDef[] nodes = [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(100, 0)), new NodeDef(3, new Vector2D(0, 100))];
        BeamDef[] beams = [new BeamDef(4, 1, 2), new BeamDef(5, 1, 3)];
        var creature = new CreatureDef(nodes, beams, [], [new ServoDef(8, 2, 4)], [], [], [], [], [], [], [], [], [new StepperDef(6, 1, 4, 5)], nextPartId: 9);

        var layout = NetworkPorts.Of(creature);

        layout.Inputs.ShouldBe(
        [
            NetworkPort.Input(6, "angleSin"),
            NetworkPort.Input(6, "angleCos"),
            NetworkPort.Input(6, "tick"),
            NetworkPort.Input(8, "angle"),
            NetworkPort.Input(8, "speed"),
        ]);
        layout.Outputs.ShouldBe(
        [
            NetworkPort.Output(6, "frequency", PortSignal.Frequency),
            NetworkPort.Output(8, "angle", PortSignal.Position),
            NetworkPort.Output(8, "strength", PortSignal.Strength),
        ]);
    }

    [Fact]
    public void Of_GivesAWheelNoPorts()
    {
        NodeDef[] nodes = [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(100, 0)), new NodeDef(3, new Vector2D(0, 100))];
        BeamDef[] beams = [new BeamDef(4, 1, 2), new BeamDef(5, 1, 3)];
        var without = new CreatureDef(nodes, beams, [new SensorDef(6, 4, SensorKind.Accelerometer)], [new ServoDef(7, 1, 4, 5)], [], [], [], nextPartId: 9);
        var with = new CreatureDef(nodes, beams, [new SensorDef(6, 4, SensorKind.Accelerometer)], [new ServoDef(7, 1, 4, 5)], [], [], [new WheelDef(8, 2)], nextPartId: 9);

        var layout = NetworkPorts.Of(with);

        layout.Inputs.ShouldBe(NetworkPorts.Of(without).Inputs);
        layout.Outputs.ShouldBe(NetworkPorts.Of(without).Outputs);
    }

    [Fact]
    public void Of_GivesATouchSensorOneTouchInput_InPartIdOrder()
    {
        NodeDef[] nodes = [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(100, 0)), new NodeDef(3, new Vector2D(0, 100))];
        BeamDef[] beams = [new BeamDef(4, 1, 2), new BeamDef(5, 1, 3)];
        var creature = new CreatureDef(
            nodes,
            beams,
            [new SensorDef(8, 4, SensorKind.Accelerometer)],
            [new ServoDef(6, 1, 4, 5)],
            [],
            [],
            [],
            [new TouchSensorDef(9, 3), new TouchSensorDef(7, 1)],
            nextPartId: 10);

        var layout = NetworkPorts.Of(creature);

        layout.Inputs.ShouldBe(
        [
            NetworkPort.Input(6, "angle"),
            NetworkPort.Input(6, "speed"),
            NetworkPort.Input(7, "touch"),
            NetworkPort.Input(8, "along"),
            NetworkPort.Input(8, "across"),
            NetworkPort.Input(9, "touch"),
        ]);
        layout.Outputs.Select(port => port.PartId).ShouldAllBe(id => id == 6);
        NetworkPorts.TouchInput(7).ShouldBe(NetworkPort.Input(7, NetworkPorts.TouchChannel));
    }

    [Fact]
    public void SensorPorts_UseTheSavedChannelKeys()
    {
        NetworkPorts.AccelerometerChannels.ShouldBe(["along", "across"]);
        NetworkPorts.CameraChannels(3).ShouldBe(["left1", "centre", "right1", "hit"]);
        NetworkPorts.SensorPorts(new SensorDef(3, 1, SensorKind.Accelerometer)).Select(port => port.Channel)
            .ShouldBe(NetworkPorts.AccelerometerChannels);
        NetworkPorts.SensorPorts(new SensorDef(3, 1, SensorKind.Camera)).Select(port => port.Channel)
            .ShouldBe(NetworkPorts.CameraChannels(SensorDef.DefaultRays));
    }

    [Theory]
    [InlineData(1, new[] { "centre", "hit" })]
    [InlineData(3, new[] { "left1", "centre", "right1", "hit" })]
    [InlineData(5, new[] { "left2", "left1", "centre", "right1", "right2", "hit" })]
    public void CameraPorts_AreOnePerRayFromLeftToRight_ThenHit(int rays, string[] channels)
    {
        NetworkPorts.CameraChannels(rays).ShouldBe(channels);
        NetworkPorts.SensorPorts(new SensorDef(3, 1, SensorKind.Camera, rays: rays))
            .ShouldBe(channels.Select(channel => NetworkPort.Input(3, channel)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(7)]
    public void CameraChannels_OfAnUnknownRayCount_Throw(int rays)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => NetworkPorts.CameraChannels(rays));
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

    private sealed class LayoutComparer : IEqualityComparer<NetworkPortLayout>
    {
        public bool Equals(NetworkPortLayout? x, NetworkPortLayout? y) =>
            x is not null && y is not null && x.Inputs.SequenceEqual(y.Inputs) && x.Outputs.SequenceEqual(y.Outputs);

        public int GetHashCode(NetworkPortLayout obj) => obj.Inputs.Count;
    }
}
