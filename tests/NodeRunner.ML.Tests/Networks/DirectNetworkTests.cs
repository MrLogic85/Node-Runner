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
    private static readonly double _sum = (0.5 * 1) + (-0.25 * 2) + (0.75 * -1) + 0.1;

    private static NeuralNetworkDef Saved => DirectNetwork.ToNeuralNetworkDef(_ports, _genome, previous: null);

    private static NetworkPortLayout WithOutputs(params NetworkPort[] outputs) => _ports with { Outputs = [.. _ports.Outputs, .. outputs] };

    private static (int Id, int? PartId, string? Channel)[] Ids(NeuralNetworkDef network) =>
        network.Neurons.Select(neuron => (neuron.Id, neuron.PartId, neuron.Channel)).ToArray();

    // The saved network compiles back to the genome whatever order its lists are in, runs the direct weights, and
    // gives each output the activation of the signal it drives.
    [Fact]
    public void ASavedNetwork_RoundTripsTheGenome_AndRunsTheDirectWeights()
    {
        var network = Saved;

        DirectNetwork.LayerSizes(_ports).ShouldBe([3, 1]);
        network.Neurons.Select(neuron => neuron.Id).ShouldBe([1, 2, 3, 4]);
        network.NextNeuronId.ShouldBe(5);
        network.Connections.ShouldAllBe(gene => gene.Enabled);
        network.Neurons.Last().Activation.ShouldBe(NeuronActivation.Tanh);
        DirectNetwork.Compile(network, _ports).ShouldBe(_genome);
        DirectNetwork.Compile(new NeuralNetworkDef(network.Neurons.Reverse().ToArray(), network.Connections.Reverse().ToArray(), network.NextNeuronId), _ports).ShouldBe(_genome);
        NeuralNetwork.FromGenome(DirectNetwork.LayerSizes(_ports), DirectNetwork.Compile(network, _ports), Activation.Tanh).Forward([1, 2, -1])[0].ShouldBe(Math.Tanh(_sum), 1e-12);

        var strengthPorts = _ports with { Outputs = [NetworkPort.Output(2, "strength:5", PortSignal.Strength)] };
        DirectNetwork.Build(DirectNetwork.ToNeuralNetworkDef(strengthPorts, _genome, previous: null), strengthPorts).Forward([1, 2, -1])[0].ShouldBe(1 / (1 + Math.Exp(-_sum)), 1e-12);
        var mixed = WithOutputs(NetworkPort.Output(9, "position", PortSignal.Length), NetworkPort.Output(9, "strength", PortSignal.Strength));
        DirectNetwork.OutputActivations(mixed).ShouldBe([Activation.Tanh, Activation.Sigmoid, Activation.Sigmoid]);
        DirectNetwork.ToNeuralNetworkDef(mixed, new double[12], previous: null).Neurons.TakeLast(3).Select(neuron => neuron.Activation)
            .ShouldBe([NeuronActivation.Tanh, NeuronActivation.Sigmoid, NeuronActivation.Sigmoid]);
    }

    [Fact]
    public void ADisabledGene_CompilesToZero_AndKeepsItsWeightOnTheNextSave()
    {
        var saved = Saved;
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

    // A part added to a trained creature changes nothing yet: new inputs (#1043 Touch, #1204 Angle sensor) get zero
    // weights, a new output starts silent, a Strength output almost passive, and a new Stepper does not step
    // (#1036, #1263). Weights are output by output, then the biases.
    [Fact]
    public void NewPorts_StartWithoutChangingTheTrainedGait()
    {
        var withSensor = _ports with { Inputs = [.. _ports.Inputs, .. NetworkPorts.AngleSensorInputs(9)] };
        var mixed = WithOutputs(NetworkPort.Output(9, "position", PortSignal.Length), NetworkPort.Output(9, "strength", PortSignal.Strength));
        var stepper = new NetworkPortLayout([.. _ports.Inputs, .. NetworkPorts.StepperInputs(9)], [.. _ports.Outputs, .. NetworkPorts.StepperOutputs(9)]);
        foreach (var (grown, expected) in new (NetworkPortLayout, double[])[]
        {
            (_ports with { Inputs = [.. _ports.Inputs, NetworkPort.Input(7, "centre")] }, [0.5, -0.25, 0.75, 0, 0.1]),
            (_ports with { Inputs = [.. _ports.Inputs, NetworkPorts.TouchInput(9)] }, [0.5, -0.25, 0.75, 0, 0.1]),
            (withSensor, [0.5, -0.25, 0.75, 0, 0, 0, 0.1]),
            (mixed, [0.5, -0.25, 0.75, 0, 0, 0, 0, 0, 0, 0.1, 0, PortSignals.PassiveStrengthBias]),
            (stepper, [0.5, -0.25, 0.75, 0, 0, 0, .. new double[6], 0.1, 0]),
        })
        {
            DirectNetwork.Compile(Saved, grown).ShouldBe(expected);
            DirectNetwork.Compile(DirectNetwork.Refit(Saved, grown), grown).ShouldBe(expected);
        }

        double[] readings = [0.3, -0.6, 0.2];
        NeuralNetwork.FromGenome(DirectNetwork.LayerSizes(withSensor), DirectNetwork.Compile(Saved, withSensor), Activation.Tanh).Forward([.. readings, 0.7, -0.4, 0.9])
            .ShouldBe(NeuralNetwork.FromGenome([3, 1], _genome, Activation.Tanh).Forward(readings));
        var outputs = DirectNetwork.Build(Saved, mixed).Forward([1, -1, 1]);
        outputs[1].ShouldBe(0.5);
        outputs[2].ShouldBe(0.018, 0.001);
        DirectNetwork.Build(DirectNetwork.Refit(Saved, stepper), stepper).Forward([1, -1, 1, 0.5, 0.5, 0.5])[1].ShouldBe(0);

        var touch = DirectNetwork.Refit(Saved, _ports with { Inputs = [.. _ports.Inputs, NetworkPorts.TouchInput(9)] });
        Ids(touch).ShouldBe([(1, 2, "angle:5"), (2, 2, "speed:5"), (3, 6, "along"), (5, 9, "touch"), (4, 2, "target:5")]);
        touch.Connections.Where(gene => gene.From != 5).ShouldBe(Saved.Connections, ignoreOrder: true);
    }

    // Neuron ids follow the port: kept ports keep theirs, new ones get fresh ids (or a later network's, never reused)
    // and gone ports are dropped with their genes.
    [Fact]
    public void Ids_FollowThePorts()
    {
        var saved = Saved;
        var refitted = DirectNetwork.Refit(saved, _ports);
        refitted.Neurons.ShouldBe(saved.Neurons);
        refitted.Connections.ShouldBe(saved.Connections);
        refitted.NextNeuronId.ShouldBe(saved.NextNeuronId);

        var changed = DirectNetwork.ToNeuralNetworkDef(new NetworkPortLayout([_ports.Inputs[2], NetworkPort.Input(7, "centre")], _ports.Outputs), [0.1, 0.2, 0.3], saved);
        Ids(changed).ShouldBe([(3, 6, "along"), (5, 7, "centre"), (4, 2, "target:5")]);
        changed.NextNeuronId.ShouldBe(6);
        changed.Connections.Select(gene => (gene.From, gene.To)).ShouldBe([(3, 4), (5, 4)]);

        // Part 2's angle input is gone and a new part 9 brings a position and a strength output.
        var rebuilt = new NetworkPortLayout(
            [_ports.Inputs[1], _ports.Inputs[2]],
            [.. _ports.Outputs, NetworkPort.Output(9, "position", PortSignal.Length), NetworkPort.Output(9, "strength", PortSignal.Strength)]);
        var regrown = DirectNetwork.Refit(saved, rebuilt);
        Ids(regrown).ShouldBe([(2, 2, "speed:5"), (3, 6, "along"), (4, 2, "target:5"), (5, 9, "position"), (6, 9, "strength")]);
        regrown.Connections.ShouldNotContain(gene => gene.From == 1);
        DirectNetwork.Compile(regrown, rebuilt).ShouldBe([-0.25, 0.75, 0, 0, 0, 0, 0.1, 0, PortSignals.PassiveStrengthBias]);

        var centre = NetworkPort.Input(7, "centre");
        var withCentre = DirectNetwork.Refit(saved, new NetworkPortLayout([.. _ports.Inputs, centre], _ports.Outputs));
        var withoutAlong = DirectNetwork.Refit(saved, new NetworkPortLayout([_ports.Inputs[0], _ports.Inputs[1], centre], _ports.Outputs), idsFrom: withCentre);
        var restored = DirectNetwork.Refit(saved, new NetworkPortLayout([.. _ports.Inputs, centre, NetworkPort.Input(8, "tilt")], _ports.Outputs), idsFrom: withoutAlong);
        Ids(restored).ShouldBe([(1, 2, "angle:5"), (2, 2, "speed:5"), (3, 6, "along"), (5, 7, "centre"), (6, 8, "tilt"), (4, 2, "target:5")]);
        restored.NextNeuronId.ShouldBe(7);
    }

    [Fact]
    public void ACreatureWithNoOutputs_KeepsItsInputs_AndDrivesNothing()
    {
        var sensorsOnly = new NetworkPortLayout(_ports.Inputs, []);

        var refitted = DirectNetwork.Refit(Saved, sensorsOnly);

        refitted.Neurons.Select(neuron => neuron.Id).ShouldBe([1, 2, 3]);
        refitted.Connections.ShouldBeEmpty();
        DirectNetwork.Refit(refitted, NetworkPortLayout.Empty).Neurons.ShouldBeEmpty();
        var network = DirectNetwork.ToNeuralNetworkDef(sensorsOnly, [], previous: null);
        DirectNetwork.Compile(network, sensorsOnly).ShouldBeEmpty();
        DirectNetwork.Build(network, sensorsOnly).Forward([0.5, 0.5, 0.5]).ShouldBeEmpty();
        DirectNetwork.Build(DirectNetwork.ToNeuralNetworkDef(NetworkPortLayout.Empty, [], previous: null), NetworkPortLayout.Empty).Forward([]).ShouldBeEmpty();
    }

    [Fact]
    public void ItRefuses_AnotherOutputActivation_AHiddenNeuron_AndAGenomeOfTheWrongLength()
    {
        var hidden = new NeuralNetworkDef(
            [new NeuronDef(1, NeuronKind.Input, 6, "along", 0, 0, NeuronActivation.Identity), new NeuronDef(2, NeuronKind.Hidden, null, null, 1, 0, NeuronActivation.Tanh)],
            [new ConnectionGeneDef(1, 2, 1, enabled: true)],
            nextNeuronId: 3);

        Should.Throw<NotSupportedException>(() => DirectNetwork.Compile(Saved, new NetworkPortLayout(_ports.Inputs, [NetworkPort.Output(2, "target:5", PortSignal.Strength)])));
        Should.Throw<NotSupportedException>(() => DirectNetwork.Compile(hidden, _ports));
        Should.Throw<ArgumentException>(() => DirectNetwork.ToNeuralNetworkDef(_ports, [1, 2], previous: null));
    }
}
