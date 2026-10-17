using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class NetworkPortLabelsTests
{
    // Every port of every kind, named by its part's own or default name.
    [Fact]
    public void For_NamesEachPortByItsPartAndReading_InPortOrder()
    {
        var labels = NetworkPortLabels.For(PortedCreature.Def);

        var expected = PortedCreature.Ports.Select(entry => (entry.Port.Direction, Label: UiText.Format($"{{0}}:\u00A0{entry.Reading}", entry.Name))).ToArray();
        labels.Inputs.ShouldBe(expected.Where(entry => entry.Direction == PortDirection.Input).Select(entry => entry.Label));
        labels.Outputs.ShouldBe(expected.Where(entry => entry.Direction == PortDirection.Output).Select(entry => entry.Label));
    }
}
