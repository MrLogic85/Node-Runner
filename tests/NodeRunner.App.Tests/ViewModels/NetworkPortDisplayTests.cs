using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class NetworkPortDisplayTests
{
    // The bar draws the value as it leaves the network, so each output's range must be its activation's.
    [Fact]
    public void EveryPortOfEveryPartKind_HasItsRangeAndShortLabel()
    {
        var layout = PortedCreature.Layout;
        layout.Inputs.Concat(layout.Outputs).ShouldBe(PortedCreature.Ports.Select(entry => entry.Port));

        foreach (var (port, kind, _, reading, range) in PortedCreature.Ports)
        {
            NetworkPortDisplay.Range(kind, port).ShouldBe(range, port.ToString());
            NetworkPortDisplay.ShortLabel(kind, port).ShouldBe(UiText.Plain(reading), port.ToString());
        }

        foreach (var port in layout.Outputs)
        {
            var activation = PortSignals.Activation(port.Signal) == NeuronActivation.Sigmoid ? PortRange.ZeroToOne : PortRange.MinusOneToOne;
            PortedCreature.Ports.Single(entry => entry.Port == port).Range.ShouldBe(activation, port.ToString());
        }
    }

    [Theory]
    [InlineData(PartKind.Wheel, PortDirection.Input, "spin")]
    [InlineData(PartKind.Accelerometer, PortDirection.Output, "along")]
    public void AnUnknownPort_Throws(PartKind kind, PortDirection direction, string channel)
    {
        var port = new NetworkPort(1, channel, direction, direction == PortDirection.Input ? PortSignal.Reading : PortSignal.Position);

        Should.Throw<InvalidOperationException>(() => NetworkPortDisplay.Range(kind, port));
        Should.Throw<InvalidOperationException>(() => NetworkPortDisplay.ShortLabel(kind, port));
    }
}
