namespace NodeRunner.ML;

public sealed class NeuralNetwork
{
    private readonly int[] _layerSizes;
    private readonly double[][] _weights;
    private readonly double[][] _biases;
    private readonly Activation[] _outputActivations;

    private NeuralNetwork(int[] layerSizes, Activation activation, double[][] weights, double[][] biases, IReadOnlyList<Activation>? outputActivations)
    {
        ValidateLayerSizes(layerSizes);
        ValidateActivation(activation);
        ValidateShape(layerSizes, weights, biases);

        _layerSizes = layerSizes.ToArray();
        Activation = activation;
        _outputActivations = OutputActivationsFor(_layerSizes, outputActivations);
        _weights = DeepCopy(weights);
        _biases = DeepCopy(biases);
    }

    public int[] LayerSizes => _layerSizes.ToArray();

    public double[][] Weights => DeepCopy(_weights);

    public double[][] Biases => DeepCopy(_biases);

    /// <summary>The hidden layers' activation.</summary>
    public Activation Activation { get; }

    /// <summary>Each output's own activation, in output order: tanh for velocity and position, sigmoid for strength (#535).</summary>
    public IReadOnlyList<Activation> OutputActivations => _outputActivations;

    public double[] Forward(double[] input)
    {
        ValidateInput(input);

        var output = new double[_layerSizes[^1]];
        Forward(input, output);
        return output;
    }

    public void Forward(double[] input, double[] output)
    {
        var scratchA = new double[MaxLayerSize()];
        var scratchB = new double[MaxLayerSize()];
        Forward(input, output, scratchA, scratchB);
    }

    public void Forward(double[] input, double[] output, double[] scratchA, double[] scratchB)
    {
        ValidateInput(input);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(scratchA);
        ArgumentNullException.ThrowIfNull(scratchB);

        if (output.Length != _layerSizes[^1])
        {
            throw new ArgumentException("Output length must match the network output layer size.", nameof(output));
        }

        if (ReferenceEquals(input, output))
        {
            throw new ArgumentException("Input and output buffers must not be the same array.", nameof(output));
        }

        ValidateDistinctBuffer(input, scratchA, nameof(scratchA), "Input and scratch buffers must not be the same array.");
        ValidateDistinctBuffer(input, scratchB, nameof(scratchB), "Input and scratch buffers must not be the same array.");
        ValidateDistinctBuffer(output, scratchA, nameof(scratchA), "Output and scratch buffers must not be the same array.");
        ValidateDistinctBuffer(output, scratchB, nameof(scratchB), "Output and scratch buffers must not be the same array.");
        ValidateDistinctBuffer(scratchA, scratchB, nameof(scratchB), "Scratch buffers must not be the same array.");

        var maxLayerSize = MaxLayerSize();
        if (scratchA.Length < maxLayerSize)
        {
            throw new ArgumentException("Scratch buffer must fit the largest network layer.", nameof(scratchA));
        }

        if (scratchB.Length < maxLayerSize)
        {
            throw new ArgumentException("Scratch buffer must fit the largest network layer.", nameof(scratchB));
        }

        var previous = input;
        var useScratchA = true;

        for (var layer = 0; layer < _weights.Length; layer++)
        {
            var inputCount = _layerSizes[layer];
            var outputCount = _layerSizes[layer + 1];
            var isOutputLayer = layer == _weights.Length - 1;
            var current = isOutputLayer ? output : useScratchA ? scratchA : scratchB;

            for (var neuron = 0; neuron < outputCount; neuron++)
            {
                var sum = _biases[layer][neuron];
                for (var inputIndex = 0; inputIndex < inputCount; inputIndex++)
                {
                    sum += _weights[layer][WeightIndex(neuron, inputIndex, inputCount)] * previous[inputIndex];
                }

                current[neuron] = ApplyActivation(sum, isOutputLayer ? _outputActivations[neuron] : Activation);
            }

            previous = current;
            useScratchA = !useScratchA;
        }
    }

    public double[][] CaptureActivations(double[] input)
    {
        ValidateInput(input);

        var activations = new double[_layerSizes.Length][];
        activations[0] = input.ToArray();
        var previous = activations[0];

        for (var layer = 0; layer < _weights.Length; layer++)
        {
            var inputCount = _layerSizes[layer];
            var outputCount = _layerSizes[layer + 1];
            var isOutputLayer = layer == _weights.Length - 1;
            var current = new double[outputCount];

            for (var neuron = 0; neuron < outputCount; neuron++)
            {
                var sum = _biases[layer][neuron];
                for (var inputIndex = 0; inputIndex < inputCount; inputIndex++)
                {
                    sum += _weights[layer][WeightIndex(neuron, inputIndex, inputCount)] * previous[inputIndex];
                }

                current[neuron] = ApplyActivation(sum, isOutputLayer ? _outputActivations[neuron] : Activation);
            }

            activations[layer + 1] = current;
            previous = current;
        }

        return activations;
    }

    public NeuralNetwork Clone() => new(_layerSizes, Activation, _weights, _biases, _outputActivations);

    public double[] FlattenGenome()
    {
        var genome = new double[GenomeLength(_layerSizes)];
        var index = 0;

        for (var layer = 0; layer < _weights.Length; layer++)
        {
            Array.Copy(_weights[layer], 0, genome, index, _weights[layer].Length);
            index += _weights[layer].Length;
            Array.Copy(_biases[layer], 0, genome, index, _biases[layer].Length);
            index += _biases[layer].Length;
        }

        return genome;
    }

    /// <summary>The network a flat genome describes. <paramref name="outputActivations"/> gives one activation per output (#535); <c>null</c> means tanh for every output.</summary>
    public static NeuralNetwork FromGenome(int[] layers, double[] genome, Activation activation, IReadOnlyList<Activation>? outputActivations = null)
    {
        ValidateLayerSizes(layers);
        ValidateActivation(activation);
        ArgumentNullException.ThrowIfNull(genome);

        var expectedLength = GenomeLength(layers);
        if (genome.Length != expectedLength)
        {
            throw new ArgumentException($"Genome length must be {expectedLength}.", nameof(genome));
        }

        var weights = new double[layers.Length - 1][];
        var biases = new double[layers.Length - 1][];
        var genomeIndex = 0;

        for (var layer = 0; layer < weights.Length; layer++)
        {
            var inputCount = layers[layer];
            var outputCount = layers[layer + 1];
            weights[layer] = new double[inputCount * outputCount];
            biases[layer] = new double[outputCount];

            Array.Copy(genome, genomeIndex, weights[layer], 0, weights[layer].Length);
            genomeIndex += weights[layer].Length;
            Array.Copy(genome, genomeIndex, biases[layer], 0, biases[layer].Length);
            genomeIndex += biases[layer].Length;
        }

        return new NeuralNetwork(layers, activation, weights, biases, outputActivations);
    }

    public static int GenomeLength(int[] layers)
    {
        ValidateLayerSizes(layers);

        var length = 0;
        for (var layer = 0; layer < layers.Length - 1; layer++)
        {
            length += (layers[layer] * layers[layer + 1]) + layers[layer + 1];
        }

        return length;
    }

    private static int WeightIndex(int output, int input, int inputCount) => (output * inputCount) + input;

    private static double ApplyActivation(double value, Activation activation)
    {
        return activation switch
        {
            Activation.Tanh => Math.Tanh(value),
            Activation.ReLU => Math.Max(0, value),
            Activation.Sigmoid => 1 / (1 + Math.Exp(-value)),
            _ => throw new ArgumentOutOfRangeException(nameof(activation), activation, "Unknown activation."),
        };
    }

    private static Activation[] OutputActivationsFor(int[] layerSizes, IReadOnlyList<Activation>? outputActivations)
    {
        if (outputActivations is null)
        {
            return Enumerable.Repeat(Activation.Tanh, layerSizes[^1]).ToArray();
        }

        if (outputActivations.Count != layerSizes[^1])
        {
            throw new ArgumentException("There must be one output activation per output.", nameof(outputActivations));
        }

        foreach (var activation in outputActivations)
        {
            ValidateActivation(activation);
        }

        return outputActivations.ToArray();
    }

    private static void ValidateActivation(Activation activation)
    {
        if (!Enum.IsDefined(typeof(Activation), activation))
        {
            throw new ArgumentOutOfRangeException(nameof(activation), activation, "Unknown activation.");
        }
    }

    private static void ValidateLayerSizes(int[]? layerSizes)
    {
        ArgumentNullException.ThrowIfNull(layerSizes);

        if (layerSizes.Length < 2)
        {
            throw new ArgumentException("A network must have at least input and output layers.", nameof(layerSizes));
        }

        for (var i = 0; i < layerSizes.Length; i++)
        {
            if (layerSizes[i] <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(layerSizes), "Layer sizes must be positive.");
            }
        }
    }

    private static void ValidateShape(int[] layerSizes, double[][] weights, double[][] biases)
    {
        ArgumentNullException.ThrowIfNull(weights);
        ArgumentNullException.ThrowIfNull(biases);

        if (weights.Length != layerSizes.Length - 1)
        {
            throw new ArgumentException("Weights must have one entry per layer transition.", nameof(weights));
        }

        if (biases.Length != layerSizes.Length - 1)
        {
            throw new ArgumentException("Biases must have one entry per layer transition.", nameof(biases));
        }

        for (var layer = 0; layer < weights.Length; layer++)
        {
            var expectedWeights = layerSizes[layer] * layerSizes[layer + 1];
            var expectedBiases = layerSizes[layer + 1];
            if (weights[layer].Length != expectedWeights)
            {
                throw new ArgumentException("Weight shape does not match layer sizes.", nameof(weights));
            }

            if (biases[layer].Length != expectedBiases)
            {
                throw new ArgumentException("Bias shape does not match layer sizes.", nameof(biases));
            }
        }
    }

    private static void ValidateDistinctBuffer(
        double[] first,
        double[] second,
        string parameterName,
        string message)
    {
        if (ReferenceEquals(first, second))
        {
            throw new ArgumentException(message, parameterName);
        }
    }

    private void ValidateInput(double[] input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.Length != _layerSizes[0])
        {
            throw new ArgumentException("Input length must match the network input layer size.", nameof(input));
        }
    }

    private int MaxLayerSize() => _layerSizes.Max();

    private static double[][] DeepCopy(double[][] source)
    {
        var copy = new double[source.Length][];
        for (var i = 0; i < source.Length; i++)
        {
            copy[i] = source[i].ToArray();
        }

        return copy;
    }
}
