using NodeRunner.Domain;
using NodeRunner.ML.Brains;

namespace NodeRunner.ML.Tests.Brains;

public sealed class DirectBrainTests
{
    private static readonly BrainPortLayout _ports = new(
        [new BrainPort(2, "angle:5", PortDirection.Input), new BrainPort(2, "speed:5", PortDirection.Input), new BrainPort(6, "along", PortDirection.Input)],
        [new BrainPort(2, "target:5", PortDirection.Output)]);

    // Three weights (one per input) then the output's bias.
    private static readonly double[] _genome = [0.5, -0.25, 0.75, 0.1];

    [Fact]
    public void LayerSizes_IsInputsThenOutputs()
    {
        DirectBrain.LayerSizes(_ports).ShouldBe([3, 1]);
    }

    [Fact]
    public void ToBrainDef_ThenCompile_RoundTripsTheGenome()
    {
        var brain = DirectBrain.ToBrainDef(_ports, _genome, previous: null);

        DirectBrain.Compile(brain, _ports).ShouldBe(_genome);
        brain.Neurons.Select(neuron => neuron.Id).ShouldBe([1, 2, 3, 4]);
        brain.NextNeuronId.ShouldBe(5);
        brain.Connections.ShouldAllBe(gene => gene.Enabled);
        brain.Neurons.Last().Activation.ShouldBe(NeuronActivation.Tanh);
    }

    [Fact]
    public void Compile_DoesNotDependOnListOrder()
    {
        var brain = DirectBrain.ToBrainDef(_ports, _genome, previous: null);
        var shuffled = new BrainDef(brain.Neurons.Reverse().ToArray(), brain.Connections.Reverse().ToArray(), brain.NextNeuronId);

        DirectBrain.Compile(shuffled, _ports).ShouldBe(_genome);
    }

    [Fact]
    public void Compile_MatchesTheForwardPassOfTheDirectWeights()
    {
        var brain = DirectBrain.ToBrainDef(_ports, _genome, previous: null);
        var network = NeuralNetwork.FromGenome(DirectBrain.LayerSizes(_ports), DirectBrain.Compile(brain, _ports), Activation.Tanh);

        network.Forward([1, 2, -1])[0].ShouldBe(Math.Tanh((0.5 * 1) + (-0.25 * 2) + (0.75 * -1) + 0.1), 1e-12);
    }

    [Fact]
    public void DisabledGene_CompilesToZero_AndKeepsItsWeightOnTheNextSave()
    {
        var saved = DirectBrain.ToBrainDef(_ports, _genome, previous: null);
        var speed = saved.Connections[1];
        var withDisabled = new BrainDef(
            saved.Neurons,
            saved.Connections.Select(gene => gene == speed ? new ConnectionGeneDef(gene.From, gene.To, gene.Weight, enabled: false) : gene).ToArray(),
            saved.NextNeuronId);

        DirectBrain.Compile(withDisabled, _ports).ShouldBe([0.5, 0, 0.75, 0.1]);
        DirectBrain.DisabledGenes(withDisabled, _ports).ShouldBe([1]);

        var resaved = DirectBrain.ToBrainDef(_ports, [0.6, 0, 0.8, 0.2], withDisabled);
        resaved.Connections[1].ShouldBe(new ConnectionGeneDef(speed.From, speed.To, -0.25, enabled: false));
        resaved.Connections[0].Weight.ShouldBe(0.6);
    }

    [Fact]
    public void Compile_APortWithNoNeuron_StartsSilent()
    {
        var brain = DirectBrain.ToBrainDef(_ports, _genome, previous: null);
        var grown = _ports with { Inputs = [.. _ports.Inputs, new BrainPort(7, "centre", PortDirection.Input)] };

        DirectBrain.Compile(brain, grown).ShouldBe([0.5, -0.25, 0.75, 0, 0.1]);
    }

    [Fact]
    public void ToBrainDef_KeepsIdsByPort_GivesNewPortsFreshIds_AndDropsGonePorts()
    {
        var first = DirectBrain.ToBrainDef(_ports, _genome, previous: null);
        var changed = new BrainPortLayout(
            [_ports.Inputs[2], new BrainPort(7, "centre", PortDirection.Input)],
            _ports.Outputs);

        var second = DirectBrain.ToBrainDef(changed, [0.1, 0.2, 0.3], first);

        second.Neurons.Select(neuron => (neuron.Id, neuron.PartId, neuron.Channel)).ShouldBe(
            [(3, (int?)6, "along"), (5, 7, "centre"), (4, 2, "target:5")]);
        second.NextNeuronId.ShouldBe(6);
        second.Connections.Select(gene => (gene.From, gene.To)).ShouldBe([(3, 4), (5, 4)]);
    }

    [Fact]
    public void Compile_AHiddenNeuron_IsNotSupportedYet()
    {
        var brain = new BrainDef(
            [new NeuronDef(1, NeuronKind.Input, 6, "along", 0, 0, NeuronActivation.Identity), new NeuronDef(2, NeuronKind.Hidden, null, null, 1, 0, NeuronActivation.Tanh)],
            [new ConnectionGeneDef(1, 2, 1, enabled: true)],
            nextNeuronId: 3);

        Should.Throw<NotSupportedException>(() => DirectBrain.Compile(brain, _ports));
    }

    [Fact]
    public void ToBrainDef_WithAGenomeOfTheWrongLength_Throws()
    {
        Should.Throw<ArgumentException>(() => DirectBrain.ToBrainDef(_ports, [1, 2], previous: null));
    }
}
