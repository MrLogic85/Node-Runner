using NodeRunner.App.ViewModels;
using NodeRunner.Domain;
using NodeRunner.ML;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class NetworkPortValuesTests
{
    [Fact]
    public void Sample_RunsTheNetworkOnce_WithInputsAndOutputsInPortOrder()
    {
        var network = PortedCreature.Network();
        var readings = PortedCreature.Readings();

        var values = NetworkPortValues.Sample(PortedCreature.Layout, network, readings);

        var activations = network.CaptureActivations(readings);
        values.IsLive.ShouldBeTrue();
        values.Layout.ShouldBe(PortedCreature.Layout);
        values.Inputs.ShouldBe(activations[0]);
        values.Outputs.ShouldBe(activations[1]);
        values.Weights.ShouldBe(network.Weights[0]);
    }

    [Fact]
    public void Sample_KeepsItsValues_WhenTheReadingsChangeLater()
    {
        var readings = new List<double>(PortedCreature.Readings());
        var values = NetworkPortValues.Sample(PortedCreature.Layout, PortedCreature.Network(), readings);
        var first = values.Inputs!.ToArray();

        readings[0] = 42;

        values.Inputs.ShouldBe(first);
    }

    [Fact]
    public void Sample_WithNoNetwork_KeepsTheLayoutButHasNoValues()
    {
        var values = NetworkPortValues.Sample(PortedCreature.Layout, null, []);

        values.IsLive.ShouldBeFalse();
        values.Layout.ShouldBe(PortedCreature.Layout);
        values.Inputs.ShouldBeNull();
        values.Outputs.ShouldBeNull();
        values.Weights.ShouldBeNull();
        values.Of(PortedCreature.Servo).ShouldAllBe(port => port.Value == null);
    }

    [Fact]
    public void Sample_WithANetworkThatDoesNotFitTheLayout_HasNoValues()
    {
        var network = NeuralNetwork.FromGenome([2, 1], [1.0, 1.0, 0.0], Activation.Tanh);

        NetworkPortValues.Sample(PortedCreature.Layout, network, PortedCreature.Readings()).IsLive.ShouldBeFalse();
        NetworkPortValues.Sample(PortedCreature.Layout, PortedCreature.Network(), [0.5]).IsLive.ShouldBeFalse();
    }

    [Fact]
    public void Of_GroupsAPartsPortsByItsId_InputsThenOutputs_InPortOrder()
    {
        var values = NetworkPortValues.Sample(PortedCreature.Layout, PortedCreature.Network(), PortedCreature.Readings());

        var servo = values.Of(PortedCreature.Servo);

        servo.Select(value => value.Port).ShouldBe(
        [
            NetworkPort.Input(PortedCreature.Servo, NetworkPorts.ServoAngleChannel),
            NetworkPort.Input(PortedCreature.Servo, NetworkPorts.ServoSpeedChannel),
            NetworkPort.Output(PortedCreature.Servo, NetworkPorts.ServoAngleOutputChannel, PortSignal.Position),
            NetworkPort.Output(PortedCreature.Servo, NetworkPorts.ServoStrengthChannel, PortSignal.Strength),
        ]);
        var layout = PortedCreature.Layout;
        servo.Select(value => value.Value).ShouldBe(
        [
            values.Inputs![IndexOf(layout.Inputs, servo[0].Port)],
            values.Inputs[IndexOf(layout.Inputs, servo[1].Port)],
            values.Outputs![IndexOf(layout.Outputs, servo[2].Port)],
            values.Outputs[IndexOf(layout.Outputs, servo[3].Port)],
        ]);
        values.Of(PortedCreature.Camera).Select(value => value.Port.Channel)
            .ShouldBe(["left2", "left1", "centre", "right1", "right2", "hit"]);
        values.Of(PortedCreature.Wheel).ShouldBeEmpty();
    }

    [Fact]
    public void ValueOf_FindsAPortByItsIdentity()
    {
        var values = NetworkPortValues.Sample(PortedCreature.Layout, PortedCreature.Network(), PortedCreature.Readings());
        var hit = NetworkPort.Input(PortedCreature.Camera, NetworkPorts.CameraHitChannel);

        values.ValueOf(hit).ShouldBe(values.Inputs![IndexOf(PortedCreature.Layout.Inputs, hit)]);
        values.ValueOf(NetworkPort.Input(99, "along")).ShouldBeNull();
    }

    private static int IndexOf(IReadOnlyList<NetworkPort> ports, NetworkPort port) => ports.ToList().IndexOf(port);
}
