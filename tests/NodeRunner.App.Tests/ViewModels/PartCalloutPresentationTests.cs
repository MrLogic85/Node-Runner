using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class PartCalloutPresentationTests
{
    private static NetworkPortValues Live() =>
        NetworkPortValues.Sample(PortedCreature.Layout, PortedCreature.Network(), PortedCreature.Readings());

    private static PartCalloutPresentation For(CreatureElementKind kind, int id, NetworkPortValues values) =>
        PartCalloutPresentation.For(PortedCreature.Def, new CreatureElementSelection(kind, id), values);

    [Fact]
    public void AServo_ShowsItsGlyphNameInputsAndOutputs_WithLiveValues()
    {
        var values = Live();

        var callout = For(CreatureElementKind.Servo, PortedCreature.Servo, values);

        callout.Kind.ShouldBe(PartKind.Servo);
        callout.Name.ShouldBe(UiText.AsWritten("Hip"));
        callout.ShowsGlyph.ShouldBeTrue();
        callout.Note.ShouldBeNull();
        callout.Inputs.Select(port => (port.Label, port.Range)).ShouldBe(
        [
            (UiText.Plain("angle"), PortRange.MinusOneToOne),
            (UiText.Plain("speed"), PortRange.MinusOneToOne),
        ]);
        callout.Outputs.Select(port => (port.Label, port.Range)).ShouldBe(
        [
            (UiText.Plain("angle"), PortRange.MinusOneToOne),
            (UiText.Plain("strength"), PortRange.ZeroToOne),
        ]);
        callout.Inputs.Concat(callout.Outputs).Select(port => port.Value)
            .ShouldBe(values.Of(PortedCreature.Servo).Select(value => value.Value));
    }

    [Theory]
    [InlineData(CreatureElementKind.Sensor, PortedCreature.Accelerometer, PartKind.Accelerometer, 2, 0)]
    [InlineData(CreatureElementKind.Sensor, PortedCreature.Camera, PartKind.Camera, 6, 0)]
    [InlineData(CreatureElementKind.Piston, PortedCreature.Piston, PartKind.Piston, 2, 2)]
    [InlineData(CreatureElementKind.VelocityMotor, PortedCreature.VelocityMotor, PartKind.VelocityMotor, 3, 2)]
    [InlineData(CreatureElementKind.AngleSensor, PortedCreature.AngleSensor, PartKind.AngleSensor, 3, 0)]
    [InlineData(CreatureElementKind.TouchSensor, PortedCreature.Touch, PartKind.TouchSensor, 1, 0)]
    public void APartWithNetworkPorts_ShowsEachOfThem(CreatureElementKind element, int id, PartKind kind, int inputs, int outputs)
    {
        var callout = For(element, id, Live());

        callout.Kind.ShouldBe(kind);
        callout.ShowsGlyph.ShouldBeTrue();
        callout.Inputs.Count.ShouldBe(inputs);
        callout.Outputs.Count.ShouldBe(outputs);
        callout.Inputs.Concat(callout.Outputs).ShouldAllBe(port => port.Value != null);
    }

    [Theory]
    [InlineData(CreatureElementKind.Wheel, PortedCreature.Wheel, PartKind.Wheel)]
    [InlineData(CreatureElementKind.Rubber, PortedCreature.Rubber, PartKind.Rubber)]
    [InlineData(CreatureElementKind.Brake, PortedCreature.Brake, PartKind.Brake)]
    public void AWheelRubberOrBrake_SaysItHasNoNetworkPorts(CreatureElementKind element, int id, PartKind kind)
    {
        var callout = For(element, id, Live());

        callout.Kind.ShouldBe(kind);

        callout.ShowsGlyph.ShouldBeTrue();
        callout.Note.ShouldBe(UiText.Plain("No network ports"));
        callout.Inputs.ShouldBeEmpty();
        callout.Outputs.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(CreatureElementKind.Node, 1)]
    [InlineData(CreatureElementKind.Beam, 5)]
    [InlineData(CreatureElementKind.Spring, PortedCreature.Spring)]
    public void AJointBeamOrSpring_ShowsOnlyItsName(CreatureElementKind kind, int id)
    {
        var callout = For(kind, id, Live());

        callout.ShowsGlyph.ShouldBeFalse();
        callout.Note.ShouldBeNull();
        callout.Inputs.ShouldBeEmpty();
        callout.Outputs.ShouldBeEmpty();
    }

    [Fact]
    public void WithNoLiveNetwork_ThePortsShowNoValues()
    {
        var callout = For(CreatureElementKind.Piston, PortedCreature.Piston, NetworkPortValues.Sample(PortedCreature.Layout, null, []));

        callout.Inputs.Count.ShouldBe(2);
        callout.Inputs.Concat(callout.Outputs).ShouldAllBe(port => port.Value == null);
    }

    [Fact]
    public void NetworkFocusAndTheCallout_ShowTheSameSample()
    {
        var values = Live();
        var networkFocus = new NetworkFocusPresentationViewModel();
        networkFocus.Configure(NetworkPortLabels.For(PortedCreature.Def), []);

        networkFocus.Update(values);
        var callout = For(CreatureElementKind.Piston, PortedCreature.Piston, values);

        var layout = PortedCreature.Layout;
        foreach (var port in callout.Inputs)
        {
            var neuron = networkFocus.Layers[NetworkFocusPresentationViewModel.InputLayer].Neurons[layout.Inputs.ToList().IndexOf(port.Port)];
            port.Value.ShouldBe(neuron.Activation);
        }

        foreach (var port in callout.Outputs)
        {
            var neuron = networkFocus.Layers[NetworkFocusPresentationViewModel.OutputLayer].Neurons[layout.Outputs.ToList().IndexOf(port.Port)];
            port.Value.ShouldBe(neuron.Activation);
        }
    }
}
