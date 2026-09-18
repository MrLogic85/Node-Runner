using System.Reflection;

namespace NodeRunner.ML.Tests;

public sealed class NeuralNetworkTests
{
    [Fact]
    public void Forward_ReturnsOutputLayerShape()
    {
        var network = new NeuralNetwork(new[] { 3, 4, 2 }, Activation.Tanh, new Random(123));

        var output = network.Forward(new[] { 0.1, -0.2, 0.3 });

        output.Length.ShouldBe(2);
    }

    [Fact]
    public void Constructor_WithSameSeed_InitializesDeterministically()
    {
        var first = new NeuralNetwork(new[] { 2, 3, 1 }, Activation.Tanh, new Random(123));
        var second = new NeuralNetwork(new[] { 2, 3, 1 }, Activation.Tanh, new Random(123));

        first.FlattenGenome().ShouldBe(second.FlattenGenome());
    }

    [Fact]
    public void Forward_WithSameSeedAndInput_IsDeterministic()
    {
        var first = new NeuralNetwork(new[] { 2, 3, 1 }, Activation.Tanh, new Random(123));
        var second = new NeuralNetwork(new[] { 2, 3, 1 }, Activation.Tanh, new Random(123));
        var input = new[] { 0.25, -0.75 };

        first.Forward(input).ShouldBe(second.Forward(input), tolerance: 0.000000000001);
    }

    [Fact]
    public void Forward_AlwaysUsesTanhOutput_ForMuscleTargets()
    {
        var network = NeuralNetwork.FromGenome(
            new[] { 1, 1 },
            new[] { 100.0, 0.0 },
            Activation.ReLU);

        var output = network.Forward(new[] { 1.0 });

        output[0].ShouldBeInRange(-1, 1);
        output[0].ShouldBe(Math.Tanh(100), tolerance: 0.000000000001);
    }

    [Fact]
    public void Forward_UsesConfiguredActivationForHiddenLayers()
    {
        var reluNetwork = NeuralNetwork.FromGenome(
            new[] { 1, 1, 1 },
            new[] { -1.0, 0.0, 1.0, 0.0 },
            Activation.ReLU);
        var sigmoidNetwork = NeuralNetwork.FromGenome(
            new[] { 1, 1, 1 },
            new[] { 1.0, 0.0, 1.0, 0.0 },
            Activation.Sigmoid);

        reluNetwork.Forward(new[] { 1.0 })[0].ShouldBe(0, tolerance: 0.000000000001);
        sigmoidNetwork.Forward(new[] { 0.0 })[0].ShouldBe(Math.Tanh(0.5), tolerance: 0.000000000001);
    }

    [Fact]
    public void Forward_WritesToCallerProvidedOutputBuffer()
    {
        var network = NeuralNetwork.FromGenome(
            new[] { 2, 1 },
            new[] { 1.0, -1.0, 0.0 },
            Activation.Tanh);
        var output = new double[1];

        network.Forward(new[] { 0.75, 0.25 }, output);

        output[0].ShouldBe(Math.Tanh(0.5), tolerance: 0.000000000001);
    }

    [Fact]
    public void Forward_WithCallerProvidedScratch_WritesExpectedOutput()
    {
        var network = NeuralNetwork.FromGenome(
            new[] { 2, 2, 1 },
            new[] { 1.0, 0.0, 0.0, 1.0, 0.0, 0.0, 1.0, 1.0, 0.0 },
            Activation.Tanh);
        var output = new double[1];
        var scratchA = new double[2];
        var scratchB = new double[2];

        network.Forward(new[] { 0.25, 0.75 }, output, scratchA, scratchB);

        output[0].ShouldBe(Math.Tanh(Math.Tanh(0.25) + Math.Tanh(0.75)), tolerance: 0.000000000001);
    }

    [Fact]
    public void FlattenGenome_UsesStableWeightsThenBiasesLayout()
    {
        var layers = new[] { 2, 2, 1 };
        var genome = new[] { 0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9 };
        var network = NeuralNetwork.FromGenome(layers, genome, Activation.Tanh);

        network.FlattenGenome().ShouldBe(genome);
        network.Weights[0].ShouldBe(new[] { 0.1, 0.2, 0.3, 0.4 });
        network.Biases[0].ShouldBe(new[] { 0.5, 0.6 });
        network.Weights[1].ShouldBe(new[] { 0.7, 0.8 });
        network.Biases[1].ShouldBe(new[] { 0.9 });
    }

    [Fact]
    public void Clone_IsIndependent()
    {
        var original = NeuralNetwork.FromGenome(new[] { 1, 1 }, new[] { 2.0, 0.0 }, Activation.Tanh);
        var clone = original.Clone();

        InternalWeights(clone)[0][0] = 99;

        clone.Forward(new[] { 1.0 })[0].ShouldNotBe(original.Forward(new[] { 1.0 })[0]);
    }

    [Fact]
    public void FromGenome_CopiesCallerOwnedArrays()
    {
        var layers = new[] { 1, 1 };
        var genome = new[] { 2.0, 0.0 };
        var network = NeuralNetwork.FromGenome(layers, genome, Activation.Tanh);

        layers[0] = 99;
        genome[0] = 99;

        network.LayerSizes.ShouldBe(new[] { 1, 1 });
        network.FlattenGenome().ShouldBe(new[] { 2.0, 0.0 });
    }

    [Fact]
    public void InvalidArguments_ThrowClearExceptions()
    {
        Should.Throw<ArgumentException>(() => new NeuralNetwork(new[] { 1 }, Activation.Tanh, new Random(1)));
        Should.Throw<ArgumentOutOfRangeException>(() => new NeuralNetwork(new[] { 1, 0 }, Activation.Tanh, new Random(1)));
        Should.Throw<ArgumentNullException>(() => new NeuralNetwork(new[] { 1, 1 }, Activation.Tanh, null!));
        Should.Throw<ArgumentOutOfRangeException>(() => new NeuralNetwork(new[] { 1, 1 }, (Activation)999, new Random(1)));
        Should.Throw<ArgumentOutOfRangeException>(() => NeuralNetwork.FromGenome(new[] { 1, 1 }, new[] { 1.0, 0.0 }, (Activation)999));
        Should.Throw<ArgumentException>(() => NeuralNetwork.FromGenome(new[] { 1, 1 }, new[] { 1.0 }, Activation.Tanh));

        var network = new NeuralNetwork(new[] { 2, 1 }, Activation.Tanh, new Random(1));
        Should.Throw<ArgumentException>(() => network.Forward(new[] { 1.0 }));
        Should.Throw<ArgumentException>(() => network.Forward(new[] { 1.0, 2.0 }, new double[2]));

        var sameShapeNetwork = new NeuralNetwork(new[] { 2, 2 }, Activation.Tanh, new Random(1));
        var aliasedBuffer = new[] { 1.0, 2.0 };
        Should.Throw<ArgumentException>(() => sameShapeNetwork.Forward(aliasedBuffer, aliasedBuffer));

        var input = new[] { 1.0, 2.0 };
        var output = new double[2];
        var scratch = new double[2];
        Should.Throw<ArgumentException>(() => sameShapeNetwork.Forward(input, output, input, new double[2]));
        Should.Throw<ArgumentException>(() => sameShapeNetwork.Forward(input, output, new double[2], input));
        Should.Throw<ArgumentException>(() => sameShapeNetwork.Forward(input, output, output, new double[2]));
        Should.Throw<ArgumentException>(() => sameShapeNetwork.Forward(input, output, new double[2], output));
        Should.Throw<ArgumentException>(() => sameShapeNetwork.Forward(input, output, scratch, scratch));

        var scratchA = new double[1];
        var scratchB = new double[2];
        Should.Throw<ArgumentException>(() => sameShapeNetwork.Forward(new[] { 1.0, 2.0 }, new double[2], scratchA, scratchB));
    }

    [Fact]
    public void ActivationFunctions_MatchHandWorkedValues()
    {
        NeuralNetwork.FromGenome(new[] { 1, 1, 1 }, new[] { 1.0, 0.0, 1.0, 0.0 }, Activation.Tanh)
            .Forward(new[] { 1.0 })[0]
            .ShouldBe(Math.Tanh(Math.Tanh(1)), tolerance: 0.000000000001);

        NeuralNetwork.FromGenome(new[] { 1, 1, 1 }, new[] { -1.0, 0.0, 1.0, 0.0 }, Activation.ReLU)
            .Forward(new[] { 1.0 })[0]
            .ShouldBe(0, tolerance: 0.000000000001);

        NeuralNetwork.FromGenome(new[] { 1, 1, 1 }, new[] { 1.0, 0.0, 1.0, 0.0 }, Activation.Sigmoid)
            .Forward(new[] { 0.0 })[0]
            .ShouldBe(Math.Tanh(0.5), tolerance: 0.000000000001);
    }

    private static double[][] InternalWeights(NeuralNetwork network)
    {
        var field = typeof(NeuralNetwork).GetField("_weights", BindingFlags.Instance | BindingFlags.NonPublic);
        field.ShouldNotBeNull();
        return (double[][])field.GetValue(network)!;
    }
}
