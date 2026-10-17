using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class PartCalloutPresentationTests
{
    private static NetworkPortValues Live() =>
        NetworkPortValues.Sample(PortedCreature.Layout, PortedCreature.Network(), PortedCreature.Readings());

    private static PartCalloutPresentation For(int id, NetworkPortValues values) =>
        PartCalloutPresentation.For(PortedCreature.Def, new CreatureElementSelection(PartKinds.Of(PortedCreature.Def.FindPart(id)!.PartKind).Element, id), values);

    [Fact]
    public void APartWithNetworkPorts_ShowsItsGlyphNameAndEachPort_WithLiveValues()
    {
        var values = Live();

        foreach (var part in PortedCreature.Ports.GroupBy(entry => entry.Port.PartId))
        {
            var callout = For(part.Key, values);

            callout.Kind.ShouldBe(part.First().Kind);
            callout.Name.ShouldBe(part.First().Name);
            callout.ShowsGlyph.ShouldBeTrue();
            callout.Note.ShouldBeNull();
            callout.Inputs.Concat(callout.Outputs).ShouldBe(part.Select(entry =>
                new PartCalloutPort(entry.Port, UiText.Plain(entry.Reading), entry.Range, values.ValueOf(entry.Port))));
            callout.Inputs.Concat(callout.Outputs).ShouldAllBe(port => port.Value != null);
            callout.Inputs.ShouldAllBe(port => port.Port.Direction == PortDirection.Input);
            callout.Outputs.ShouldAllBe(port => port.Port.Direction == PortDirection.Output);
        }

        For(PortedCreature.Piston, NetworkPortValues.Sample(PortedCreature.Layout, null, [])).Inputs.ShouldAllBe(port => port.Value == null);
    }

    // A wheel, rubber or brake says it has no network ports; a joint, beam or spring shows only its name.
    [Theory]
    [InlineData(PortedCreature.Wheel, PartKind.Wheel, true)]
    [InlineData(PortedCreature.Rubber, PartKind.Rubber, true)]
    [InlineData(PortedCreature.Brake, PartKind.Brake, true)]
    [InlineData(1, PartKind.Node, false)]
    [InlineData(5, PartKind.Beam, false)]
    [InlineData(PortedCreature.Spring, PartKind.Spring, false)]
    public void APartWithoutNetworkPorts_ShowsNoPorts(int id, PartKind kind, bool showsGlyph)
    {
        var callout = For(id, Live());

        callout.Kind.ShouldBe(kind);
        callout.ShowsGlyph.ShouldBe(showsGlyph);
        callout.Note.ShouldBe(showsGlyph ? UiText.Plain("No network ports") : null);
        callout.Inputs.ShouldBeEmpty();
        callout.Outputs.ShouldBeEmpty();
    }

    [Fact]
    public void NetworkFocusAndTheCallout_ShowTheSameSample()
    {
        var values = Live();
        var networkFocus = new NetworkFocusPresentationViewModel();
        networkFocus.Configure(NetworkPortLabels.For(PortedCreature.Def), []);

        networkFocus.Update(values);
        var callout = For(PortedCreature.Piston, values);

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
