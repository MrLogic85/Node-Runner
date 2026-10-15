using NodeRunner.Domain;
using NodeRunner.ML.Networks;

namespace NodeRunner.ML.Tests.Networks;

public sealed class DirectNetworkTests
{
    private static readonly NetworkPortLayout _ports = new(
        [NetworkPort.Input(2, "angle:5"), NetworkPort.Input(2, "speed:5"), NetworkPort.Input(6, "along")],
        [NetworkPort.Output(2, "target:5", PortSignal.Velocity)]);

    // Three weights (one per input) then the output's bias.
    private static readonly double[] _genome = [0.5, -0.25, 0.75, 0.1];

    [Fact]
    public void LayerSizes_IsInputsThenOutputs()
    {
        DirectNetwork.LayerSizes(_ports).ShouldBe([3, 1]);
    }

    [Fact]
    public void ToNeuralNetworkDef_ThenCompile_RoundTripsTheGenome()
    {
        var network = DirectNetwork.ToNeuralNetworkDef(_ports, _genome, previous: null);

        DirectNetwork.Compile(network, _ports).ShouldBe(_genome);
        network.Neurons.Select(neuron => neuron.Id).ShouldBe([1, 2, 3, 4]);
        network.NextNeuronId.ShouldBe(5);
        network.Connections.ShouldAllBe(gene => gene.Enabled);
        network.Neurons.Last().Activation.ShouldBe(NeuronActivation.Tanh);
    }

    [Fact]
    public void Compile_DoesNotDependOnListOrder()
    {
        var network = DirectNetwork.ToNeuralNetworkDef(_ports, _genome, previous: null);
        var shuffled = new NeuralNetworkDef(network.Neurons.Reverse().ToArray(), network.Connections.Reverse().ToArray(), network.NextNeuronId);

        DirectNetwork.Compile(shuffled, _ports).ShouldBe(_genome);
    }

    [Fact]
    public void Compile_MatchesTheForwardPassOfTheDirectWeights()
    {
        var definition = DirectNetwork.ToNeuralNetworkDef(_ports, _genome, previous: null);
        var network = NeuralNetwork.FromGenome(DirectNetwork.LayerSizes(_ports), DirectNetwork.Compile(definition, _ports), Activation.Tanh);

        network.Forward([1, 2, -1])[0].ShouldBe(Math.Tanh((0.5 * 1) + (-0.25 * 2) + (0.75 * -1) + 0.1), 1e-12);
    }

    [Fact]
    public void Build_RunsTheSavedNetworkWithEachOutputsActivation()
    {
        var ports = _ports with { Outputs = [NetworkPort.Output(2, "strength:5", PortSignal.Strength)] };
        var definition = DirectNetwork.ToNeuralNetworkDef(ports, _genome, previous: null);

        var network = DirectNetwork.Build(definition, ports);

        var sum = (0.5 * 1) + (-0.25 * 2) + (0.75 * -1) + 0.1;
        network.Forward([1, 2, -1])[0].ShouldBe(1 / (1 + Math.Exp(-sum)), 1e-12);
    }

    [Fact]
    public void DisabledGene_CompilesToZero_AndKeepsItsWeightOnTheNextSave()
    {
        var saved = DirectNetwork.ToNeuralNetworkDef(_ports, _genome, previous: null);
        var speed = saved.Connections[1];
        var withDisabled = new NeuralNetworkDef(
            saved.Neurons,
            saved.Connections.Select(gene => gene == speed ? new ConnectionGeneDef(gene.From, gene.To, gene.Weight, enabled: false) : gene).ToArray(),
            saved.NextNeuronId);

        DirectNetwork.Compile(withDisabled, _ports).ShouldBe([0.5, 0, 0.75, 0.1]);
        DirectNetwork.DisabledGenes(withDisabled, _ports).ShouldBe([1]);

        var resaved = DirectNetwork.ToNeuralNetworkDef(_ports, [0.6, 0, 0.8, 0.2], withDisabled);
        resaved.Connections[1].ShouldBe(new ConnectionGeneDef(speed.From, speed.To, -0.25, enabled: false));
        resaved.Connections[0].Weight.ShouldBe(0.6);
    }

    [Fact]
    public void Compile_APortWithNoNeuron_StartsSilent()
    {
        var network = DirectNetwork.ToNeuralNetworkDef(_ports, _genome, previous: null);
        var grown = _ports with { Inputs = [.. _ports.Inputs, NetworkPort.Input(7, "centre")] };

        DirectNetwork.Compile(network, grown).ShouldBe([0.5, -0.25, 0.75, 0, 0.1]);
    }

    [Fact]
    public void ToNeuralNetworkDef_KeepsIdsByPort_GivesNewPortsFreshIds_AndDropsGonePorts()
    {
        var first = DirectNetwork.ToNeuralNetworkDef(_ports, _genome, previous: null);
        var changed = new NetworkPortLayout(
            [_ports.Inputs[2], NetworkPort.Input(7, "centre")],
            _ports.Outputs);

        var second = DirectNetwork.ToNeuralNetworkDef(changed, [0.1, 0.2, 0.3], first);

        second.Neurons.Select(neuron => (neuron.Id, neuron.PartId, neuron.Channel)).ShouldBe(
            [(3, (int?)6, "along"), (5, 7, "centre"), (4, 2, "target:5")]);
        second.NextNeuronId.ShouldBe(6);
        second.Connections.Select(gene => (gene.From, gene.To)).ShouldBe([(3, 4), (5, 4)]);
    }

    [Fact]
    public void OutputActivations_FollowTheSignalEachPortDrives()
    {
        var ports = _ports with { Outputs = [.. _ports.Outputs, NetworkPort.Output(9, "position", PortSignal.Length), NetworkPort.Output(9, "strength", PortSignal.Strength)] };

        DirectNetwork.OutputActivations(ports).ShouldBe([Activation.Tanh, Activation.Sigmoid, Activation.Sigmoid]);
        DirectNetwork.ToNeuralNetworkDef(ports, new double[12], previous: null).Neurons.TakeLast(3).Select(neuron => neuron.Activation)
            .ShouldBe([NeuronActivation.Tanh, NeuronActivation.Sigmoid, NeuronActivation.Sigmoid]);
    }

    [Fact]
    public void Compile_ANewPart_StartsAlmostPassive()
    {
        var trained = DirectNetwork.ToNeuralNetworkDef(_ports, _genome, previous: null);
        var grown = _ports with { Outputs = [.. _ports.Outputs, NetworkPort.Output(9, "position", PortSignal.Length), NetworkPort.Output(9, "strength", PortSignal.Strength)] };

        var genome = DirectNetwork.Compile(trained, grown);

        // Weights output by output (3 inputs each), then the three biases.
        genome.ShouldBe([0.5, -0.25, 0.75, 0, 0, 0, 0, 0, 0, 0.1, 0, PortSignals.PassiveStrengthBias]);
        var outputs = NeuralNetwork.FromGenome(DirectNetwork.LayerSizes(grown), genome, Activation.Tanh, DirectNetwork.OutputActivations(grown))
            .Forward([1, -1, 1]);
        outputs[1].ShouldBe(0.5);
        outputs[2].ShouldBe(0.018, tolerance: 0.001);
    }

    [Fact]
    public void Refit_WithTheSamePorts_KeepsTheNetwork()
    {
        var network = DirectNetwork.ToNeuralNetworkDef(_ports, _genome, previous: null);

        var refitted = DirectNetwork.Refit(network, _ports);

        refitted.Neurons.ShouldBe(network.Neurons);
        refitted.Connections.ShouldBe(network.Connections);
        refitted.NextNeuronId.ShouldBe(network.NextNeuronId);
    }

    [Fact]
    public void Refit_KeepsMatchedPorts_StartsNewOnesAlmostPassive_AndDropsRemovedOnes()
    {
        var network = DirectNetwork.ToNeuralNetworkDef(_ports, _genome, previous: null);
        // Part 2's angle input is gone and a new part 9 brings a position and a strength output.
        var rebuilt = new NetworkPortLayout(
            [_ports.Inputs[1], _ports.Inputs[2]],
            [.. _ports.Outputs, NetworkPort.Output(9, "position", PortSignal.Length), NetworkPort.Output(9, "strength", PortSignal.Strength)]);

        var refitted = DirectNetwork.Refit(network, rebuilt);

        refitted.Neurons.Select(neuron => (neuron.Id, neuron.PartId, neuron.Channel)).ShouldBe(
            [(2, (int?)2, "speed:5"), (3, 6, "along"), (4, 2, "target:5"), (5, 9, "position"), (6, 9, "strength")]);
        refitted.Connections.ShouldNotContain(gene => gene.From == 1);
        // The kept output keeps its two remaining weights and its bias; the new outputs start silent.
        DirectNetwork.Compile(refitted, rebuilt).ShouldBe([-0.25, 0.75, 0, 0, 0, 0, 0.1, 0, PortSignals.PassiveStrengthBias]);
    }

    // #1043: a Touch sensor added to a trained creature brings one new input that changes nothing yet.
    [Fact]
    public void Refit_WithANewTouchInput_GivesItANewNeuronWithZeroWeights_AndKeepsTheRest()
    {
        var network = DirectNetwork.ToNeuralNetworkDef(_ports, _genome, previous: null);
        var withTouch = new NetworkPortLayout([.. _ports.Inputs, NetworkPorts.TouchInput(9)], _ports.Outputs);

        var refitted = DirectNetwork.Refit(network, withTouch);

        refitted.Neurons.Select(neuron => (neuron.Id, neuron.PartId, neuron.Channel)).ShouldBe(
            [(1, (int?)2, "angle:5"), (2, 2, "speed:5"), (3, 6, "along"), (5, 9, "touch"), (4, 2, "target:5")]);
        refitted.Connections.Where(gene => gene.From != 5).ShouldBe(network.Connections, ignoreOrder: true);
        DirectNetwork.Compile(refitted, withTouch).ShouldBe([0.5, -0.25, 0.75, 0, 0.1]);
    }

    // #1204: an Angle sensor added to a trained creature brings three new inputs whose weights start at 0, so it changes no output.
    [Fact]
    public void Refit_WithANewAngleSensor_GivesItsThreeInputsZeroWeights_AndLeavesTheOutputsUnchanged()
    {
        var network = DirectNetwork.ToNeuralNetworkDef(_ports, _genome, previous: null);
        var withSensor = new NetworkPortLayout([.. _ports.Inputs, .. NetworkPorts.AngleSensorInputs(9)], _ports.Outputs);

        var refitted = DirectNetwork.Refit(network, withSensor);

        var genome = DirectNetwork.Compile(refitted, withSensor);
        genome.ShouldBe([0.5, -0.25, 0.75, 0, 0, 0, 0.1]);
        int[] before = [_ports.Inputs.Count, _ports.Outputs.Count];
        int[] after = [withSensor.Inputs.Count, withSensor.Outputs.Count];
        double[] readings = [0.3, -0.6, 0.2];
        NeuralNetwork.FromGenome(after, genome, Activation.Tanh).Forward([.. readings, 0.7, -0.4, 0.9])
            .ShouldBe(NeuralNetwork.FromGenome(before, _genome, Activation.Tanh).Forward(readings));
    }

    // #1036, #1263: a Stepper added to a trained network does not step, so the trained gait plays on.
    [Fact]
    public void Compile_ANewStepper_DoesNotStep()
    {
        var trained = DirectNetwork.ToNeuralNetworkDef(_ports, _genome, previous: null);
        var grown = new NetworkPortLayout([.. _ports.Inputs, .. NetworkPorts.StepperInputs(9)], [.. _ports.Outputs, .. NetworkPorts.StepperOutputs(9)]);

        var genome = DirectNetwork.Compile(DirectNetwork.Refit(trained, grown), grown);

        // Weights output by output (6 inputs each), then the two biases.
        genome.ShouldBe([0.5, -0.25, 0.75, 0, 0, 0, .. new double[6], 0.1, 0]);
        var outputs = NeuralNetwork.FromGenome(DirectNetwork.LayerSizes(grown), genome, Activation.Tanh, DirectNetwork.OutputActivations(grown))
            .Forward([1, -1, 1, 0.5, 0.5, 0.5]);
        outputs[1].ShouldBe(0);
    }

    [Fact]
    public void Refit_WithIdsFrom_KeepsALaterNetworksIdsForNewPorts_AndNeverReusesAnId()
    {
        var opened = DirectNetwork.ToNeuralNetworkDef(_ports, _genome, previous: null);
        var centre = NetworkPort.Input(7, "centre");
        var saved = DirectNetwork.Refit(opened, new NetworkPortLayout([.. _ports.Inputs, centre], _ports.Outputs));
        var withoutAlong = DirectNetwork.Refit(opened, new NetworkPortLayout([_ports.Inputs[0], _ports.Inputs[1], centre], _ports.Outputs), idsFrom: saved);

        var restored = DirectNetwork.Refit(
            opened,
            new NetworkPortLayout([.. _ports.Inputs, centre, NetworkPort.Input(8, "tilt")], _ports.Outputs),
            idsFrom: withoutAlong);

        restored.Neurons.Select(neuron => (neuron.Id, neuron.PartId, neuron.Channel)).ShouldBe(
            [(1, (int?)2, "angle:5"), (2, 2, "speed:5"), (3, 6, "along"), (5, 7, "centre"), (6, 8, "tilt"), (4, 2, "target:5")]);
        restored.NextNeuronId.ShouldBe(7);
    }

    [Fact]
    public void Refit_ToACreatureWithNoOutputs_KeepsOnlyItsInputs()
    {
        var network = DirectNetwork.ToNeuralNetworkDef(_ports, _genome, previous: null);
        var sensorsOnly = new NetworkPortLayout(_ports.Inputs, []);

        var refitted = DirectNetwork.Refit(network, sensorsOnly);

        refitted.Neurons.Select(neuron => neuron.Id).ShouldBe([1, 2, 3]);
        refitted.Connections.ShouldBeEmpty();
        DirectNetwork.Refit(refitted, NetworkPortLayout.Empty).Neurons.ShouldBeEmpty();
    }

    [Fact]
    public void Network_WithNoOutputs_RunsAndDrivesNothing()
    {
        var sensorsOnly = new NetworkPortLayout(_ports.Inputs, []);
        var network = DirectNetwork.ToNeuralNetworkDef(sensorsOnly, [], previous: null);

        DirectNetwork.Compile(network, sensorsOnly).ShouldBeEmpty();
        DirectNetwork.Build(network, sensorsOnly).Forward([.. _ports.Inputs.Select(_ => 0.5)]).ShouldBeEmpty();
        DirectNetwork.Build(DirectNetwork.ToNeuralNetworkDef(NetworkPortLayout.Empty, [], previous: null), NetworkPortLayout.Empty).Forward([]).ShouldBeEmpty();
    }

    [Fact]
    public void Compile_AnOutputSavedWithAnotherActivation_IsNotSupported()
    {
        var network = DirectNetwork.ToNeuralNetworkDef(_ports, _genome, previous: null);
        var strength = new NetworkPortLayout(_ports.Inputs, [NetworkPort.Output(2, "target:5", PortSignal.Strength)]);

        Should.Throw<NotSupportedException>(() => DirectNetwork.Compile(network, strength));
    }

    [Fact]
    public void Compile_AHiddenNeuron_IsNotSupportedYet()
    {
        var network = new NeuralNetworkDef(
            [new NeuronDef(1, NeuronKind.Input, 6, "along", 0, 0, NeuronActivation.Identity), new NeuronDef(2, NeuronKind.Hidden, null, null, 1, 0, NeuronActivation.Tanh)],
            [new ConnectionGeneDef(1, 2, 1, enabled: true)],
            nextNeuronId: 3);

        Should.Throw<NotSupportedException>(() => DirectNetwork.Compile(network, _ports));
    }

    [Fact]
    public void ToNeuralNetworkDef_WithAGenomeOfTheWrongLength_Throws()
    {
        Should.Throw<ArgumentException>(() => DirectNetwork.ToNeuralNetworkDef(_ports, [1, 2], previous: null));
    }
}
