using NodeRunner.App.ViewModels;
using NodeRunner.Domain;
using NodeRunner.ML;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class BrainPortValuesTests
{
    [Fact]
    public void Sample_RunsTheBrainOnce_WithInputsAndOutputsInPortOrder()
    {
        var brain = PortedCreature.Brain();
        var readings = PortedCreature.Readings();

        var values = BrainPortValues.Sample(PortedCreature.Layout, brain, readings);

        var activations = brain.CaptureActivations(readings);
        values.IsLive.ShouldBeTrue();
        values.Layout.ShouldBe(PortedCreature.Layout);
        values.Inputs.ShouldBe(activations[0]);
        values.Outputs.ShouldBe(activations[1]);
        values.Weights.ShouldBe(brain.Weights[0]);
    }

    [Fact]
    public void Sample_KeepsItsValues_WhenTheReadingsChangeLater()
    {
        var readings = new List<double>(PortedCreature.Readings());
        var values = BrainPortValues.Sample(PortedCreature.Layout, PortedCreature.Brain(), readings);
        var first = values.Inputs!.ToArray();

        readings[0] = 42;

        values.Inputs.ShouldBe(first);
    }

    [Fact]
    public void Sample_WithNoBrain_KeepsTheLayoutButHasNoValues()
    {
        var values = BrainPortValues.Sample(PortedCreature.Layout, null, []);

        values.IsLive.ShouldBeFalse();
        values.Layout.ShouldBe(PortedCreature.Layout);
        values.Inputs.ShouldBeNull();
        values.Outputs.ShouldBeNull();
        values.Weights.ShouldBeNull();
        values.Of(PortedCreature.Servo).ShouldAllBe(port => port.Value == null);
    }

    [Fact]
    public void Sample_WithABrainThatDoesNotFitTheLayout_HasNoValues()
    {
        var brain = NeuralNetwork.FromGenome([2, 1], [1.0, 1.0, 0.0], Activation.Tanh);

        BrainPortValues.Sample(PortedCreature.Layout, brain, PortedCreature.Readings()).IsLive.ShouldBeFalse();
        BrainPortValues.Sample(PortedCreature.Layout, PortedCreature.Brain(), [0.5]).IsLive.ShouldBeFalse();
    }

    [Fact]
    public void Of_GroupsAPartsPortsByItsId_SensesThenOutputs_InPortOrder()
    {
        var values = BrainPortValues.Sample(PortedCreature.Layout, PortedCreature.Brain(), PortedCreature.Readings());

        var servo = values.Of(PortedCreature.Servo);

        servo.Select(value => value.Port).ShouldBe(
        [
            BrainPort.Input(PortedCreature.Servo, BrainPorts.ServoAngleChannel),
            BrainPort.Input(PortedCreature.Servo, BrainPorts.ServoSpeedChannel),
            BrainPort.Output(PortedCreature.Servo, BrainPorts.ServoAngleOutputChannel, PortSignal.Position),
            BrainPort.Output(PortedCreature.Servo, BrainPorts.ServoStrengthChannel, PortSignal.Strength),
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
        var values = BrainPortValues.Sample(PortedCreature.Layout, PortedCreature.Brain(), PortedCreature.Readings());
        var hit = BrainPort.Input(PortedCreature.Camera, BrainPorts.CameraHitChannel);

        values.ValueOf(hit).ShouldBe(values.Inputs![IndexOf(PortedCreature.Layout.Inputs, hit)]);
        values.ValueOf(BrainPort.Input(99, "along")).ShouldBeNull();
    }

    private static int IndexOf(IReadOnlyList<BrainPort> ports, BrainPort port) => ports.ToList().IndexOf(port);
}
