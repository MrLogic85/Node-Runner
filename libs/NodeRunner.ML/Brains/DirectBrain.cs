using NodeRunner.Domain;

namespace NodeRunner.ML.Brains;

/// <summary>
/// The 0.13 direct brain (#536): every input port connects straight to every output port, with no
/// hidden layer. It is saved as a <see cref="BrainDef"/> graph and trained as a flat genome in
/// <see cref="NeuralNetwork"/>'s layout for <see cref="LayerSizes"/>: one weight per
/// (output, input) pair, output by output in port order, then one bias per output. The port order
/// comes from <see cref="BrainPorts"/>, so compiling never depends on the order of the saved lists.
/// A port with no neuron or connection yet compiles to 0, so a new part starts silent.
/// </summary>
public static class DirectBrain
{
    public static int[] LayerSizes(BrainPortLayout ports)
    {
        ArgumentNullException.ThrowIfNull(ports);
        return [ports.Inputs.Count, ports.Outputs.Count];
    }

    /// <summary>The genome <paramref name="brain"/> gives this creature's ports. A disabled gene compiles to 0.</summary>
    public static double[] Compile(BrainDef brain, BrainPortLayout ports)
    {
        ArgumentNullException.ThrowIfNull(brain);
        ArgumentNullException.ThrowIfNull(ports);

        var graph = new Graph(brain);
        var genome = new double[NeuralNetwork.GenomeLength(LayerSizes(ports))];
        var biasStart = ports.Inputs.Count * ports.Outputs.Count;
        for (var o = 0; o < ports.Outputs.Count; o++)
        {
            if (graph.Neuron(ports.Outputs[o]) is not { } output)
            {
                continue;
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

    /// <summary>The genome positions of <paramref name="brain"/>'s disabled genes, which training must leave at 0.</summary>
    public static int[] DisabledGenes(BrainDef brain, BrainPortLayout ports)
    {
        ArgumentNullException.ThrowIfNull(brain);
        ArgumentNullException.ThrowIfNull(ports);

        var graph = new Graph(brain);
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
    /// stored weight. Neurons and genes for ports the creature no longer has are dropped.
    /// </summary>
    public static BrainDef ToBrainDef(BrainPortLayout ports, double[] genome, BrainDef? previous)
    {
        ArgumentNullException.ThrowIfNull(ports);
        ArgumentNullException.ThrowIfNull(genome);
        if (genome.Length != NeuralNetwork.GenomeLength(LayerSizes(ports)))
        {
            throw new ArgumentException("Genome length must match the ports.", nameof(genome));
        }

        var graph = previous is null ? null : new Graph(previous);
        var nextId = previous?.NextNeuronId ?? 1;
        int IdFor(BrainPort port) => graph?.Neuron(port)?.Id ?? nextId++;

        var inputs = ports.Inputs
            .Select(port => new NeuronDef(IdFor(port), NeuronKind.Input, port.PartId, port.Channel, layer: 0, bias: 0, NeuronActivation.Identity))
            .ToArray();
        var biasStart = ports.Inputs.Count * ports.Outputs.Count;
        var outputs = ports.Outputs
            .Select((port, o) => new NeuronDef(IdFor(port), NeuronKind.Output, port.PartId, port.Channel, layer: 1, genome[biasStart + o], NeuronActivation.Tanh))
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

        return new BrainDef([.. inputs, .. outputs], genes, nextId);
    }

    private sealed class Graph
    {
        private readonly Dictionary<(NeuronKind, int?, string?), NeuronDef> _neuronByPort = [];
        private readonly Dictionary<(int, int), ConnectionGeneDef> _geneByKey = [];

        public Graph(BrainDef brain)
        {
            foreach (var neuron in brain.Neurons)
            {
                if (neuron.Kind == NeuronKind.Hidden)
                {
                    throw new NotSupportedException("A direct brain has no hidden neurons; hidden layers come with the brain graph milestone (#543).");
                }

                if (neuron.Kind == NeuronKind.Output && neuron.Activation != NeuronActivation.Tanh)
                {
                    throw new NotSupportedException($"A direct brain's outputs use tanh, not {neuron.Activation}.");
                }

                _neuronByPort.Add((neuron.Kind, neuron.PartId, neuron.Channel), neuron);
            }

            foreach (var gene in brain.Connections)
            {
                _geneByKey.Add((gene.From, gene.To), gene);
            }
        }

        public NeuronDef? Neuron(BrainPort port) =>
            _neuronByPort.GetValueOrDefault((port.Direction == PortDirection.Input ? NeuronKind.Input : NeuronKind.Output, port.PartId, port.Channel));

        public ConnectionGeneDef? Gene(BrainPort input, NeuronDef output) =>
            Neuron(input) is { } neuron ? Gene(neuron.Id, output.Id) : null;

        public ConnectionGeneDef? Gene(int from, int to) => _geneByKey.GetValueOrDefault((from, to));
    }
}
