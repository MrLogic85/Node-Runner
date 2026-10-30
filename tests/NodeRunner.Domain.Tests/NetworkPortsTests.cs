using System.Text.Json;

namespace NodeRunner.Domain.Tests;

public sealed class NetworkPortsTests
{
    // Ports in part-id order, each part's in its declared order; nodes, beams, Springs, Wheels, Rubbers and Brakes have none.
    [Fact]
    public void Of_EveryPartKind_GivesTheSavedLayout()
    {
        var layout = NetworkPorts.Of(EveryPartKind.Creature);

        layout.Inputs.Select(port => (port.PartId, port.Channel)).ShouldBe(
        [
            (7, "along"), (7, "across"), (8, "left1"), (8, "centre"), (8, "right1"), (8, "hit"), (9, "angle"), (9, "speed"),
            (10, "length"), (10, "speed"), (13, "touch"), (15, "angleSin"), (15, "angleCos"), (15, "speed"),
            (17, "angleSin"), (17, "angleCos"), (17, "speed"), (21, "angleSin"), (21, "angleCos"), (21, "tick"), (22, "value"),
        ]);
        layout.Outputs.ShouldBe(
        [
            NetworkPort.Output(9, "angle", PortSignal.Position), NetworkPort.Output(9, "strength", PortSignal.Strength),
            NetworkPort.Output(10, "position", PortSignal.Length), NetworkPort.Output(10, "strength", PortSignal.Strength),
            NetworkPort.Output(15, "speed", PortSignal.Velocity), NetworkPort.Output(15, "strength", PortSignal.Strength),
            NetworkPort.Output(21, "frequency", PortSignal.Frequency),
            NetworkPort.Output(22, "target", PortSignal.Remember), NetworkPort.Output(22, "write", PortSignal.Write),
        ]);
        layout.Inputs.ShouldContain(NetworkPorts.TouchInput(13));
        NetworkPorts.Of(new CreatureDef([new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(1, 0)), new BeamDef(3, 1, 2)]))
            .ShouldBe(NetworkPortLayout.Empty, new LayoutComparer());
    }

    // A network's ports must stay put when the creature is moved, reordered, saved or grown.
    [Fact]
    public void Of_IgnoresPositionsAndListOrder_SurvivesASave_AndKeepsOldPortsInOrderWhenAPartIsAdded()
    {
        var every = EveryPartKind.Creature;
        var before = NetworkPorts.Of(every);
        var moved = new CreatureDef(
            every.Parts.Reverse().Select(part => part is NodeDef node ? new NodeDef(node.Id, new Vector2D(node.Position.X * 3, node.Position.Y - 40)) : part),
            every.NextPartId);
        var loaded = JsonSerializer.Deserialize<CreatureDef>(JsonSerializer.Serialize(every))!;
        var grown = NetworkPorts.Of(new CreatureDef([.. every.Parts, new PistonDef(23, 1, 18)], 24));

        NetworkPorts.Of(moved).ShouldBe(before, new LayoutComparer());
        NetworkPorts.Of(loaded).ShouldBe(before, new LayoutComparer());
        grown.Inputs.Where(before.Inputs.Contains).ShouldBe(before.Inputs);
        grown.Outputs.Where(before.Outputs.Contains).ShouldBe(before.Outputs);
        grown.Outputs.ShouldContain(NetworkPort.Output(23, "position", PortSignal.Length));
    }

    [Theory]
    [InlineData(1, new[] { "centre", "hit" })]
    [InlineData(3, new[] { "left1", "centre", "right1", "hit" })]
    [InlineData(5, new[] { "left2", "left1", "centre", "right1", "right2", "hit" })]
    public void CameraPorts_AreOnePerRayFromLeftToRight_ThenHit(int rays, string[] channels)
    {
        NetworkPorts.SensorPorts(new SensorDef(3, 1, SensorKind.Camera, rays: rays))
            .ShouldBe(channels.Select(channel => NetworkPort.Input(3, channel)));
        Refuses.OutOfRange(() => NetworkPorts.CameraChannels(rays + 1));
    }

    // Readings pass through; motions, frequencies and what a Memory cell remembers are signed; lengths,
    // strengths and its write are shares. A new port starts passive: its Strength or write about 2%
    // (sigmoid of −4), everything else at 0. Only what drives a motion or force is powered.
    [Fact]
    public void PortSignals_GiveEachSignalItsActivationAndPassiveBias()
    {
        foreach (var (signal, activation, bias) in new[]
        {
            (PortSignal.Reading, NeuronActivation.Identity, 0.0), (PortSignal.Velocity, NeuronActivation.Tanh, 0),
            (PortSignal.Position, NeuronActivation.Tanh, 0), (PortSignal.Frequency, NeuronActivation.Tanh, 0),
            (PortSignal.Length, NeuronActivation.Sigmoid, 0), (PortSignal.Strength, NeuronActivation.Sigmoid, -4),
            (PortSignal.Remember, NeuronActivation.Tanh, 0), (PortSignal.Write, NeuronActivation.Sigmoid, -4),
        })
        {
            (PortSignals.Activation(signal), PortSignals.PassiveBias(signal)).ShouldBe((activation, bias), $"{signal}");
        }

        Enum.GetValues<PortSignal>().Where(PortSignals.IsPowered).ShouldBe(
            [PortSignal.Velocity, PortSignal.Position, PortSignal.Length, PortSignal.Strength, PortSignal.Frequency], ignoreOrder: true);
        (1 / (1 + Math.Exp(-PortSignals.PassiveStrengthBias))).ShouldBe(0.018, tolerance: 0.001);
        Refuses.OutOfRange(() => NetworkPort.Output(1, "x", PortSignal.Reading));
    }

    private sealed class LayoutComparer : IEqualityComparer<NetworkPortLayout>
    {
        public bool Equals(NetworkPortLayout? x, NetworkPortLayout? y) =>
            x is not null && y is not null && x.Inputs.SequenceEqual(y.Inputs) && x.Outputs.SequenceEqual(y.Outputs);

        public int GetHashCode(NetworkPortLayout obj) => obj.Inputs.Count;
    }
}
