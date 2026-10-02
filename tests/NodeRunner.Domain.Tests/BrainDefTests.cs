namespace NodeRunner.Domain.Tests;

public sealed class BrainDefTests
{
    private static NeuronDef Input(int id, int part = 6, string channel = "along") =>
        new(id, NeuronKind.Input, part, channel, 0, 0, NeuronActivation.Identity);

    private static NeuronDef Output(int id, int part = 2, string channel = "target:5") =>
        new(id, NeuronKind.Output, part, channel, 1, 0, NeuronActivation.Tanh);

    [Fact]
    public void ADirectBrain_IsValid()
    {
        var brain = new BrainDef([Input(1), Output(2)], [new ConnectionGeneDef(1, 2, 0.5, enabled: false)], nextNeuronId: 3);

        brain.Connections.Single().Enabled.ShouldBeFalse();
    }

    [Fact]
    public void AHiddenNeuron_FitsBetweenTheLayers()
    {
        var hidden = new NeuronDef(3, NeuronKind.Hidden, null, null, 1, 0.2, NeuronActivation.Relu);
        var output = new NeuronDef(2, NeuronKind.Output, 2, "target:5", 2, 0, NeuronActivation.Tanh);

        Should.NotThrow(() => new BrainDef([Input(1), output, hidden], [new ConnectionGeneDef(1, 3, 1, true), new ConnectionGeneDef(3, 2, 1, true), new ConnectionGeneDef(1, 2, 1, true)], 4));
    }

    [Fact]
    public void DuplicateIds_Throw() =>
        Should.Throw<ArgumentException>(() => new BrainDef([Input(1), Output(1)], [], 2));

    [Fact]
    public void AnIdAtOrAboveNextNeuronId_Throws() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new BrainDef([Input(1), Output(2)], [], 2));

    [Fact]
    public void TwoNeuronsForOnePort_Throw() =>
        Should.Throw<ArgumentException>(() => new BrainDef([Input(1), Input(2)], [], 3));

    [Fact]
    public void AGeneToAMissingNeuron_Throws() =>
        Should.Throw<ArgumentException>(() => new BrainDef([Input(1)], [new ConnectionGeneDef(1, 9, 1, true)], 2));

    [Fact]
    public void ABackwardGene_Throws() =>
        Should.Throw<ArgumentException>(() => new BrainDef([Input(1), Output(2)], [new ConnectionGeneDef(2, 1, 1, true)], 3));

    [Fact]
    public void ARepeatedGene_Throws() =>
        Should.Throw<ArgumentException>(() => new BrainDef([Input(1), Output(2)], [new ConnectionGeneDef(1, 2, 1, true), new ConnectionGeneDef(1, 2, 2, true)], 3));

    [Fact]
    public void APortNeuronWithoutAPort_Throws() =>
        Should.Throw<ArgumentException>(() => new NeuronDef(1, NeuronKind.Input, null, null, 0, 0, NeuronActivation.Identity));

    [Fact]
    public void AHiddenNeuronWithAPort_Throws() =>
        Should.Throw<ArgumentException>(() => new NeuronDef(1, NeuronKind.Hidden, 6, "along", 1, 0, NeuronActivation.Tanh));

    [Fact]
    public void AnInputWithABias_Throws() =>
        Should.Throw<ArgumentException>(() => new NeuronDef(1, NeuronKind.Input, 6, "along", 0, 0.5, NeuronActivation.Identity));

    [Fact]
    public void ANonFiniteBias_Throws() =>
        Should.Throw<ArgumentException>(() => new NeuronDef(1, NeuronKind.Output, 2, "target:5", 1, double.NaN, NeuronActivation.Tanh));

    [Fact]
    public void ANonFiniteWeight_Throws() =>
        Should.Throw<ArgumentException>(() => new ConnectionGeneDef(1, 2, double.PositiveInfinity, true));
}
