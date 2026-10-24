using NodeRunner.Domain;

namespace NodeRunner.ML.Networks;

/// <summary>
/// The direct network: every input port connects straight to every output port, with no
/// hidden layer. It is saved as a <see cref="NeuralNetworkDef"/> graph and trained as a flat genome in
/// <see cref="NeuralNetwork"/>'s layout for <see cref="LayerSizes"/>: one weight per
/// (output, input) pair, output by output in port order, then one bias per output. The port order
/// comes from <see cref="NetworkPorts"/>, so compiling never depends on the order of the saved lists.
/// Each output uses its port's activation (<see cref="PortSignals"/>). A new port starts
/// almost passive: its incoming weights compile to 0 and a new output takes its signal's passive bias.
/// </summary>
public static class DirectNetwork
{
    public static int[] LayerSizes(NetworkPortLayout ports)
    {
        ArgumentNullException.ThrowIfNull(ports);
        return [ports.Inputs.Count, ports.Outputs.Count];
    }

    /// <summary>Each output's activation, in port order, from the signal its port drives.</summary>
    public static Activation[] OutputActivations(NetworkPortLayout ports)
    {
        ArgumentNullException.ThrowIfNull(ports);
        return ports.Outputs.Select(port => ToActivation(PortSignals.Activation(port.Signal))).ToArray();
    }

    /// <summary>The runtime network the saved <paramref name="network"/> graph compiles to on these ports, as Simulate plays it.</summary>
    public static NeuralNetwork Build(NeuralNetworkDef network, NetworkPortLayout ports) =>
        NeuralNetwork.FromGenome(LayerSizes(ports), Compile(network, ports), Activation.Tanh, OutputActivations(ports));

    /// <summary>The genome <paramref name="network"/> gives this creature's ports. A disabled gene compiles to 0.</summary>
    public static double[] Compile(NeuralNetworkDef network, NetworkPortLayout ports)
    {
        ArgumentNullException.ThrowIfNull(network);
        ArgumentNullException.ThrowIfNull(ports);

        var graph = new Graph(network);
        var genome = new double[GenomeLength(ports)];
        var biasStart = ports.Inputs.Count * ports.Outputs.Count;
        for (var o = 0; o < ports.Outputs.Count; o++)
        {
            var port = ports.Outputs[o];
            if (graph.Neuron(port) is not { } output)
            {
                genome[biasStart + o] = PortSignals.PassiveBias(port.Signal);
                continue;
            }

            if (output.Activation != PortSignals.Activation(port.Signal))
            {
                throw new NotSupportedException($"Output {port.Channel} drives {port.Signal}, which uses {PortSignals.Activation(port.Signal)}, not {output.Activation}.");
            }

            genome[biasStart + o] = output.Bias;
            for (var i = 0; i < ports.Inputs.Count; i++)
            {
                if (graph.Gene(ports.Inputs[i], output) is { Enabled: true } gene)
                {
                    genome[(o * ports.Inputs.Count) + i] = gene.Weight;
                }
            }
        }

        return genome;
    }

    /// <summary>The genome positions of <paramref name="network"/>'s disabled genes, which training must leave at 0.</summary>
    public static int[] DisabledGenes(NeuralNetworkDef network, NetworkPortLayout ports)
    {
        ArgumentNullException.ThrowIfNull(network);
        ArgumentNullException.ThrowIfNull(ports);

        var graph = new Graph(network);
        var disabled = new List<int>();
        for (var o = 0; o < ports.Outputs.Count; o++)
        {
            if (graph.Neuron(ports.Outputs[o]) is not { } output)
            {
                continue;
            }

            for (var i = 0; i < ports.Inputs.Count; i++)
            {
                if (graph.Gene(ports.Inputs[i], output) is { Enabled: false })
                {
                    disabled.Add((o * ports.Inputs.Count) + i);
                }
            }
        }

        return disabled.ToArray();
    }

    /// <summary>
    /// The graph for a trained <paramref name="genome"/>. Neurons keep their ids from
    /// <paramref name="previous"/> by port, and new ports get fresh ids; a disabled gene keeps its
    /// stored weight. Neurons and genes for ports the creature no longer has are dropped. A port
    /// <paramref name="previous"/> lacks keeps its id from <paramref name="idsFrom"/>, a later network of
    /// the same creature, if that has one; fresh ids start past both, so no id is ever reused.
    /// </summary>
    public static NeuralNetworkDef ToNeuralNetworkDef(NetworkPortLayout ports, double[] genome, NeuralNetworkDef? previous, NeuralNetworkDef? idsFrom = null)
    {
        ArgumentNullException.ThrowIfNull(ports);
        ArgumentNullException.ThrowIfNull(genome);
        if (genome.Length != GenomeLength(ports))
        {
            throw new ArgumentException("Genome length must match the ports.", nameof(genome));
        }

        var graph = previous is null ? null : new Graph(previous);
        var later = idsFrom is null ? null : new Graph(idsFrom);
        var nextId = Math.Max(previous?.NextNeuronId ?? 1, idsFrom?.NextNeuronId ?? 1);
        int IdFor(NetworkPort port) => graph?.Neuron(port)?.Id ?? later?.Neuron(port)?.Id ?? nextId++;

        var inputs = ports.Inputs
            .Select(port => new NeuronDef(IdFor(port), NeuronKind.Input, port.PartId, port.Channel, layer: 0, bias: 0, NeuronActivation.Identity))
            .ToArray();
        var biasStart = ports.Inputs.Count * ports.Outputs.Count;
        var outputs = ports.Outputs
            .Select((port, o) => new NeuronDef(IdFor(port), NeuronKind.Output, port.PartId, port.Channel, layer: 1, genome[biasStart + o], PortSignals.Activation(port.Signal)))
            .ToArray();

        var genes = new List<ConnectionGeneDef>(biasStart);
        for (var o = 0; o < outputs.Length; o++)
        {
            for (var i = 0; i < inputs.Length; i++)
            {
                var stored = graph?.Gene(inputs[i].Id, outputs[o].Id);
                genes.Add(stored is { Enabled: false }
                    ? stored
                    : new ConnectionGeneDef(inputs[i].Id, outputs[o].Id, genome[(o * inputs.Length) + i], enabled: true));
            }
        }

        return new NeuralNetworkDef([.. inputs, .. outputs], genes, nextId);
    }

    /// <summary>
    /// <paramref name="network"/> fitted to a rebuilt creature's <paramref name="ports"/>. Ports
    /// match by part id, channel and direction: a kept port keeps its neuron, connections and bias, a
    /// new port starts almost passive, and a removed port's neuron and connections are dropped. Part
    /// ids are never reused, so a new part never inherits a removed part's weights. For
    /// <paramref name="idsFrom"/>, see <see cref="ToNeuralNetworkDef"/>.
    /// </summary>
    public static NeuralNetworkDef Refit(NeuralNetworkDef network, NetworkPortLayout ports, NeuralNetworkDef? idsFrom = null) =>
        ToNeuralNetworkDef(ports, Compile(network, ports), network, idsFrom);

    /// <summary>
    /// The network's learnable parameters: a weight per (output, input) pair and a bias per output,
    /// <see cref="NeuralNetwork"/>'s genome length for these layer sizes. It is also defined, as
    /// empty, for a creature with no inputs or no outputs: one rebuilt down to bare sensors or bare Pistons.
    /// </summary>
    public static int GenomeLength(NetworkPortLayout ports)
    {
        ArgumentNullException.ThrowIfNull(ports);
        return (ports.Inputs.Count * ports.Outputs.Count) + ports.Outputs.Count;
    }

    private static Activation ToActivation(NeuronActivation activation) => activation switch
    {
        NeuronActivation.Tanh => Activation.Tanh,
        NeuronActivation.Sigmoid => Activation.Sigmoid,
        NeuronActivation.Relu => Activation.ReLU,
        _ => throw new NotSupportedException($"An output cannot use {activation}."),
    };

    private sealed class Graph
    {
        private readonly Dictionary<(NeuronKind, int?, string?), NeuronDef> _neuronByPort = [];
        private readonly Dictionary<(int, int), ConnectionGeneDef> _geneByKey = [];

        public Graph(NeuralNetworkDef network)
        {
            foreach (var neuron in network.Neurons)
            {
                if (neuron.Kind == NeuronKind.Hidden)
                {
                    throw new NotSupportedException("A direct network has no hidden neurons; hidden layers come with the network graph milestone (#543).");
                }

                _neuronByPort.Add((neuron.Kind, neuron.PartId, neuron.Channel), neuron);
            }

            foreach (var gene in network.Connections)
            {
                _geneByKey.Add((gene.From, gene.To), gene);
            }
        }

        public NeuronDef? Neuron(NetworkPort port) =>
            _neuronByPort.GetValueOrDefault((port.Direction == PortDirection.Input ? NeuronKind.Input : NeuronKind.Output, port.PartId, port.Channel));

        public ConnectionGeneDef? Gene(NetworkPort input, NeuronDef output) =>
            Neuron(input) is { } neuron ? Gene(neuron.Id, output.Id) : null;

        public ConnectionGeneDef? Gene(int from, int to) => _geneByKey.GetValueOrDefault((from, to));
    }
}
