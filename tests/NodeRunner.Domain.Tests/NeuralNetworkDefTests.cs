namespace NodeRunner.Domain.Tests;

public sealed class NeuralNetworkDefTests
{
    private static readonly NeuronDef _in = new(1, NeuronKind.Input, 6, "along", 0, 0, NeuronActivation.Identity);
    private static readonly NeuronDef _out = new(2, NeuronKind.Output, 2, "target:5", 1, 0, NeuronActivation.Tanh);

    [Fact]
    public void AHiddenNeuron_FitsBetweenTheLayers()
    {
        var hidden = new NeuronDef(3, NeuronKind.Hidden, null, null, 1, 0.2, NeuronActivation.Relu);
        var output = new NeuronDef(2, NeuronKind.Output, 2, "target:5", 2, 0, NeuronActivation.Tanh);

        new NeuralNetworkDef([_in, output, hidden], [new(1, 3, 1, true), new(3, 2, 1, true), new(1, 2, 1, false)], 4).Connections.Count.ShouldBe(3);
    }

    [Fact]
    public void Constructors_RefuseABrokenNetwork()
    {
        Refuses.Invalid(() => new NeuralNetworkDef([_in, _out, new NeuronDef(2, NeuronKind.Hidden, null, null, 1, 0, NeuronActivation.Tanh)], [], 3))
            .Message.ShouldBe("Neuron id 2 is used more than once.");
        Refuses.OutOfRange(() => new NeuralNetworkDef([_in, _out], [], 2));
        Refuses.OutOfRange(() => new NeuralNetworkDef([], [], 0));
        Refuses.Invalid(() => new NeuralNetworkDef([_in, null!], [], 3));
        Refuses.Invalid(() => new NeuralNetworkDef([_in, new NeuronDef(2, NeuronKind.Input, 6, "along", 0, 0, NeuronActivation.Identity)], [], 3));
        Refuses.Invalid(() => new NeuralNetworkDef([_in], [new(1, 9, 1, true)], 2));
        Refuses.Invalid(() => new NeuralNetworkDef([_in, _out], [new(2, 1, 1, true)], 3));
        Refuses.Invalid(() => new NeuralNetworkDef([_in, _out], [new(1, 2, 1, true), new(1, 2, 2, true)], 3));

        Refuses.OutOfRange(() => new NeuronDef(0, NeuronKind.Input, 6, "along", 0, 0, NeuronActivation.Identity));
        Refuses.OutOfRange(() => new NeuronDef(1, (NeuronKind)9, 6, "along", 0, 0, NeuronActivation.Identity));
        Refuses.OutOfRange(() => new NeuronDef(1, NeuronKind.Input, 6, "along", 0, 0, (NeuronActivation)9));
        Refuses.Invalid(() => new NeuronDef(1, NeuronKind.Input, null, null, 0, 0, NeuronActivation.Identity));
        Refuses.Invalid(() => new NeuronDef(1, NeuronKind.Output, 2, " ", 1, 0, NeuronActivation.Tanh));
        Refuses.Invalid(() => new NeuronDef(1, NeuronKind.Hidden, 6, "along", 1, 0, NeuronActivation.Tanh));
        Refuses.Invalid(() => new NeuronDef(1, NeuronKind.Input, 6, "along", 0, 0.5, NeuronActivation.Identity));
        Refuses.Invalid(() => new NeuronDef(1, NeuronKind.Output, 2, "target:5", 0, 0, NeuronActivation.Tanh));
        Refuses.OutOfRange(() => new NeuronDef(1, NeuronKind.Output, 2, "target:5", 1, double.NaN, NeuronActivation.Tanh));
        Refuses.OutOfRange(() => new ConnectionGeneDef(0, 2, 1, true));
        Refuses.OutOfRange(() => new ConnectionGeneDef(1, 0, 1, true));
        Refuses.OutOfRange(() => new ConnectionGeneDef(1, 2, double.PositiveInfinity, true));
    }
}
