using NodeRunner.Domain;
using NodeRunner.ML.Brains;

namespace NodeRunner.ML.Tests.Brains;

public sealed class DirectBrainTests
{
    private static readonly BrainPortLayout _ports = new(
        [BrainPort.Input(2, "angle:5"), BrainPort.Input(2, "speed:5"), BrainPort.Input(6, "along")],
        [BrainPort.Output(2, "target:5", PortSignal.Velocity)]);

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
        var grown = _ports with { Inputs = [.. _ports.Inputs, BrainPort.Input(7, "centre")] };

        DirectBrain.Compile(brain, grown).ShouldBe([0.5, -0.25, 0.75, 0, 0.1]);
    }

    [Fact]
    public void ToBrainDef_KeepsIdsByPort_GivesNewPortsFreshIds_AndDropsGonePorts()
    {
        var first = DirectBrain.ToBrainDef(_ports, _genome, previous: null);
        var changed = new BrainPortLayout(
            [_ports.Inputs[2], BrainPort.Input(7, "centre")],
            _ports.Outputs);

        var second = DirectBrain.ToBrainDef(changed, [0.1, 0.2, 0.3], first);

        second.Neurons.Select(neuron => (neuron.Id, neuron.PartId, neuron.Channel)).ShouldBe(
            [(3, (int?)6, "along"), (5, 7, "centre"), (4, 2, "target:5")]);
        second.NextNeuronId.ShouldBe(6);
        second.Connections.Select(gene => (gene.From, gene.To)).ShouldBe([(3, 4), (5, 4)]);
    }

    [Fact]
    public void OutputActivations_FollowTheSignalEachPortDrives()
    {
        var ports = _ports with { Outputs = [.. _ports.Outputs, BrainPort.Output(9, "position", PortSignal.Position), BrainPort.Output(9, "strength", PortSignal.Strength)] };

        DirectBrain.OutputActivations(ports).ShouldBe([Activation.Tanh, Activation.Tanh, Activation.Sigmoid]);
        DirectBrain.ToBrainDef(ports, new double[12], previous: null).Neurons.TakeLast(3).Select(neuron => neuron.Activation)
            .ShouldBe([NeuronActivation.Tanh, NeuronActivation.Tanh, NeuronActivation.Sigmoid]);
    }

    [Fact]
    public void Compile_ANewPart_StartsAlmostPassive()
    {
        var trained = DirectBrain.ToBrainDef(_ports, _genome, previous: null);
        var grown = _ports with { Outputs = [.. _ports.Outputs, BrainPort.Output(9, "position", PortSignal.Position), BrainPort.Output(9, "strength", PortSignal.Strength)] };

        var genome = DirectBrain.Compile(trained, grown);

        // Weights output by output (3 inputs each), then the three biases.
        genome.ShouldBe([0.5, -0.25, 0.75, 0, 0, 0, 0, 0, 0, 0.1, 0, PortSignals.PassiveStrengthBias]);
        var outputs = NeuralNetwork.FromGenome(DirectBrain.LayerSizes(grown), genome, Activation.Tanh, DirectBrain.OutputActivations(grown))
            .Forward([1, -1, 1]);
        outputs[1].ShouldBe(0);
        outputs[2].ShouldBe(0.018, tolerance: 0.001);
    }

    [Fact]
    public void Refit_WithTheSamePorts_KeepsTheBrain()
    {
        var brain = DirectBrain.ToBrainDef(_ports, _genome, previous: null);

        var refitted = DirectBrain.Refit(brain, _ports);

        refitted.Neurons.ShouldBe(brain.Neurons);
        refitted.Connections.ShouldBe(brain.Connections);
        refitted.NextNeuronId.ShouldBe(brain.NextNeuronId);
    }

    [Fact]
    public void Refit_KeepsMatchedPorts_StartsNewOnesAlmostPassive_AndDropsRemovedOnes()
    {
        var brain = DirectBrain.ToBrainDef(_ports, _genome, previous: null);
        // Part 2's angle input is gone and a new part 9 brings a position and a strength output.
        var rebuilt = new BrainPortLayout(
            [_ports.Inputs[1], _ports.Inputs[2]],
            [.. _ports.Outputs, BrainPort.Output(9, "position", PortSignal.Position), BrainPort.Output(9, "strength", PortSignal.Strength)]);

        var refitted = DirectBrain.Refit(brain, rebuilt);

        refitted.Neurons.Select(neuron => (neuron.Id, neuron.PartId, neuron.Channel)).ShouldBe(
            [(2, (int?)2, "speed:5"), (3, 6, "along"), (4, 2, "target:5"), (5, 9, "position"), (6, 9, "strength")]);
        refitted.Connections.ShouldNotContain(gene => gene.From == 1);
        // The kept output keeps its two remaining weights and its bias; the new outputs start silent.
        DirectBrain.Compile(refitted, rebuilt).ShouldBe([-0.25, 0.75, 0, 0, 0, 0, 0.1, 0, PortSignals.PassiveStrengthBias]);
    }

    [Fact]
    public void Refit_ToACreatureWithNoOutputs_KeepsOnlyItsInputs()
    {
        var brain = DirectBrain.ToBrainDef(_ports, _genome, previous: null);
        var sensorsOnly = new BrainPortLayout(_ports.Inputs, []);

        var refitted = DirectBrain.Refit(brain, sensorsOnly);

        refitted.Neurons.Select(neuron => neuron.Id).ShouldBe([1, 2, 3]);
        refitted.Connections.ShouldBeEmpty();
        DirectBrain.Refit(refitted, BrainPortLayout.Empty).Neurons.ShouldBeEmpty();
    }

    [Fact]
    public void Compile_AnOutputSavedWithAnotherActivation_IsNotSupported()
    {
        var brain = DirectBrain.ToBrainDef(_ports, _genome, previous: null);
        var strength = new BrainPortLayout(_ports.Inputs, [BrainPort.Output(2, "target:5", PortSignal.Strength)]);

        Should.Throw<NotSupportedException>(() => DirectBrain.Compile(brain, strength));
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
