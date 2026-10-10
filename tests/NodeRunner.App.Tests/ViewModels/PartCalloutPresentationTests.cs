using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class PartCalloutPresentationTests
{
    private static BrainPortValues Live() =>
        BrainPortValues.Sample(PortedCreature.Layout, PortedCreature.Brain(), PortedCreature.Readings());

    private static PartCalloutPresentation For(CreatureElementKind kind, int id, BrainPortValues values) =>
        PartCalloutPresentation.For(PortedCreature.Def, new CreatureElementSelection(kind, id), values);

    [Fact]
    public void AServo_ShowsItsGlyphNameSensesAndOutputs_WithLiveValues()
    {
        var values = Live();

        var callout = For(CreatureElementKind.Servo, PortedCreature.Servo, values);

        callout.Kind.ShouldBe(PartSettingsKind.Servo);
        callout.Name.ShouldBe(UiText.AsWritten("Hip"));
        callout.ShowsGlyph.ShouldBeTrue();
        callout.Note.ShouldBeNull();
        callout.Senses.Select(port => (port.Label, port.Range)).ShouldBe(
        [
            (UiText.Plain("angle"), PortRange.MinusOneToOne),
            (UiText.Plain("speed"), PortRange.MinusOneToOne),
        ]);
        callout.Outputs.Select(port => (port.Label, port.Range)).ShouldBe(
        [
            (UiText.Plain("angle"), PortRange.MinusOneToOne),
            (UiText.Plain("strength"), PortRange.ZeroToOne),
        ]);
        callout.Senses.Concat(callout.Outputs).Select(port => port.Value)
            .ShouldBe(values.Of(PortedCreature.Servo).Select(value => value.Value));
    }

    [Theory]
    [InlineData(CreatureElementKind.Sensor, PortedCreature.Accelerometer, PartSettingsKind.Accelerometer, 2, 0)]
    [InlineData(CreatureElementKind.Sensor, PortedCreature.Camera, PartSettingsKind.Camera, 6, 0)]
    [InlineData(CreatureElementKind.Piston, PortedCreature.Piston, PartSettingsKind.Piston, 2, 2)]
    public void APartWithBrainPorts_ShowsEachOfThem(CreatureElementKind element, int id, PartSettingsKind kind, int senses, int outputs)
    {
        var callout = For(element, id, Live());

        callout.Kind.ShouldBe(kind);
        callout.ShowsGlyph.ShouldBeTrue();
        callout.Senses.Count.ShouldBe(senses);
        callout.Outputs.Count.ShouldBe(outputs);
        callout.Senses.Concat(callout.Outputs).ShouldAllBe(port => port.Value != null);
    }

    [Fact]
    public void AWheel_SaysItHasNoBrainPorts()
    {
        var callout = For(CreatureElementKind.Wheel, PortedCreature.Wheel, Live());

        callout.ShowsGlyph.ShouldBeTrue();
        callout.Note.ShouldBe(UiText.Plain("No brain ports"));
        callout.Senses.ShouldBeEmpty();
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
        callout.Senses.ShouldBeEmpty();
        callout.Outputs.ShouldBeEmpty();
    }

    [Fact]
    public void WithNoLiveBrain_ThePortsShowNoValues()
    {
        var callout = For(CreatureElementKind.Piston, PortedCreature.Piston, BrainPortValues.Sample(PortedCreature.Layout, null, []));

        callout.Senses.Count.ShouldBe(2);
        callout.Senses.Concat(callout.Outputs).ShouldAllBe(port => port.Value == null);
    }

    [Fact]
    public void BrainFocusAndTheCallout_ShowTheSameSample()
    {
        var values = Live();
        var brainFocus = new BrainFocusPresentationViewModel();
        brainFocus.Configure(BrainPortLabels.For(PortedCreature.Def), []);

        brainFocus.Update(values);
        var callout = For(CreatureElementKind.Piston, PortedCreature.Piston, values);

        var layout = PortedCreature.Layout;
        foreach (var port in callout.Senses)
        {
            var neuron = brainFocus.Layers[BrainFocusPresentationViewModel.InputLayer].Neurons[layout.Inputs.ToList().IndexOf(port.Port)];
            port.Value.ShouldBe(neuron.Activation);
        }

        foreach (var port in callout.Outputs)
        {
            var neuron = brainFocus.Layers[BrainFocusPresentationViewModel.OutputLayer].Neurons[layout.Outputs.ToList().IndexOf(port.Port)];
            port.Value.ShouldBe(BrainPortDisplay.ShownValue(PartSettingsKind.Piston, port.Port, neuron.Activation));
        }
    }
}
