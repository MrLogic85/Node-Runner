using NodeRunner.App.ViewModels;
using NodeRunner.Domain;
using NodeRunner.ML;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class NetworkPortValuesTests
{
    [Fact]
    public void Sample_RunsTheNetworkOnce_AndGroupsAPartsPortsByItsId_InputsThenOutputs_InPortOrder()
    {
        var network = PortedCreature.Network();
        var readings = new List<double>(PortedCreature.Readings());
        var activations = network.CaptureActivations([.. readings]);

        var values = NetworkPortValues.Sample(PortedCreature.Layout, network, readings);
        readings[0] = 42;

        values.IsLive.ShouldBeTrue();
        values.Layout.ShouldBe(PortedCreature.Layout);
        values.Inputs.ShouldBe(activations[0]);
        values.Outputs.ShouldBe(activations[1]);
        values.Weights.ShouldBe(network.Weights[0]);
        var servo = values.Of(PortedCreature.Servo);
        servo.Select(value => value.Port).ShouldBe(PortedCreature.Ports.Select(entry => entry.Port).Where(port => port.PartId == PortedCreature.Servo));
        servo.Select(value => value.Value).ShouldBe(
        [
            values.Inputs[IndexOf(values.Layout.Inputs, servo[0].Port)],
            values.Inputs[IndexOf(values.Layout.Inputs, servo[1].Port)],
            values.Outputs[IndexOf(values.Layout.Outputs, servo[2].Port)],
            values.Outputs[IndexOf(values.Layout.Outputs, servo[3].Port)],
        ]);
        values.Of(PortedCreature.Wheel).ShouldBeEmpty();
        values.ValueOf(NetworkPort.Input(PortedCreature.Camera, NetworkPorts.CameraHitChannel)).ShouldBe(values.Inputs[7]);
        values.ValueOf(NetworkPort.Input(99, "along")).ShouldBeNull();
    }

    [Fact]
    public void Sample_WithNoNetwork_OrOneThatDoesNotFit_KeepsTheLayoutButHasNoValues()
    {
        var values = NetworkPortValues.Sample(PortedCreature.Layout, null, []);

        values.IsLive.ShouldBeFalse();
        values.Layout.ShouldBe(PortedCreature.Layout);
        values.Inputs.ShouldBeNull();
        values.Outputs.ShouldBeNull();
        values.Weights.ShouldBeNull();
        values.Of(PortedCreature.Servo).ShouldAllBe(port => port.Value == null);
        var tooSmall = NeuralNetwork.FromGenome([2, 1], [1.0, 1.0, 0.0], Activation.Tanh);
        NetworkPortValues.Sample(PortedCreature.Layout, tooSmall, PortedCreature.Readings()).IsLive.ShouldBeFalse();
        NetworkPortValues.Sample(PortedCreature.Layout, PortedCreature.Network(), [0.5]).IsLive.ShouldBeFalse();
    }

    private static int IndexOf(IReadOnlyList<NetworkPort> ports, NetworkPort port) => ports.ToList().IndexOf(port);
}
