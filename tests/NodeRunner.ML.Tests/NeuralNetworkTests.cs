using System.Reflection;

namespace NodeRunner.ML.Tests;

public sealed class NeuralNetworkTests
{
    private static NeuralNetwork Network(int[] layers, int seed)
    {
        var random = new Random(seed);
        var genome = Enumerable.Range(0, NeuralNetwork.GenomeLength(layers)).Select(_ => (random.NextDouble() * 2) - 1).ToArray();
        return NeuralNetwork.FromGenome(layers, genome, Activation.Tanh);
    }

    [Fact]
    public void Forward_ReturnsOutputLayerShape()
    {
        var network = Network(new[] { 3, 4, 2 }, 123);

        var output = network.Forward(new[] { 0.1, -0.2, 0.3 });

        output.Length.ShouldBe(2);
    }

    [Fact]
    public void Forward_WithSameSeedAndInput_IsDeterministic()
    {
        var first = Network(new[] { 2, 3, 1 }, 123);
        var second = Network(new[] { 2, 3, 1 }, 123);
        var input = new[] { 0.25, -0.75 };

        first.Forward(input).ShouldBe(second.Forward(input), tolerance: 0.000000000001);
    }

    [Fact]
    public void Forward_WithoutOutputActivations_UsesTanhOutputs()
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
    public void Forward_UsesEachOutputsOwnActivation()
    {
        var network = NeuralNetwork.FromGenome([1, 2], [1.0, 1.0, 0.0, -4.0], Activation.Tanh, [Activation.Tanh, Activation.Sigmoid]);

        var output = network.Forward([0.5]);

        output[0].ShouldBe(Math.Tanh(0.5), tolerance: 1e-12);
        output[1].ShouldBe(1 / (1 + Math.Exp(3.5)), tolerance: 1e-12);
        network.CaptureActivations([0.5])[1].ShouldBe(output);
        network.Clone().OutputActivations.ShouldBe([Activation.Tanh, Activation.Sigmoid]);
    }

    [Fact]
    public void FromGenome_WithTheWrongNumberOfOutputActivations_Throws()
    {
        Should.Throw<ArgumentException>(() => NeuralNetwork.FromGenome([1, 2], [1.0, 1.0, 0.0, 0.0], Activation.Tanh, [Activation.Sigmoid]));
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
    public void CaptureActivations_ReturnsInputHiddenAndOutputLayers()
    {
        var network = NeuralNetwork.FromGenome(
            new[] { 2, 2, 1 },
            new[] { 1.0, 0.0, 0.0, 1.0, 0.0, 0.0, 1.0, -1.0, 0.0 },
            Activation.Tanh);

        var activations = network.CaptureActivations(new[] { 0.25, -0.75 });

        activations.Length.ShouldBe(3);
        activations[0].ShouldBe(new[] { 0.25, -0.75 });
        activations[1].ShouldBe(new[] { Math.Tanh(0.25), Math.Tanh(-0.75) }, tolerance: 0.000000000001);
        activations[2][0].ShouldBe(Math.Tanh(Math.Tanh(0.25) - Math.Tanh(-0.75)), tolerance: 0.000000000001);
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
    public void Forward_WithAnEmptyLayer_ReturnsNoOutputs()
    {
        // A creature with no powered parts has no outputs, and one with no sensors or parts no inputs (#845).
        NeuralNetwork.GenomeLength([2, 0]).ShouldBe(0);
        NeuralNetwork.FromGenome([2, 0], [], Activation.Tanh).Forward([0.5, -0.5]).ShouldBeEmpty();
        NeuralNetwork.FromGenome([0, 0], [], Activation.Tanh).Forward([]).ShouldBeEmpty();
        NeuralNetwork.FromGenome([0, 1], [0.25], Activation.Tanh).Forward([])[0].ShouldBe(Math.Tanh(0.25), tolerance: 1e-12);
    }

    [Fact]
    public void InvalidArguments_ThrowClearExceptions()
    {
        Should.Throw<ArgumentException>(() => NeuralNetwork.FromGenome(new[] { 1 }, [], Activation.Tanh));
        Should.Throw<ArgumentOutOfRangeException>(() => NeuralNetwork.FromGenome(new[] { 1, -1 }, [], Activation.Tanh));
        Should.Throw<ArgumentNullException>(() => NeuralNetwork.FromGenome(new[] { 1, 1 }, null!, Activation.Tanh));
        Should.Throw<ArgumentOutOfRangeException>(() => NeuralNetwork.FromGenome(new[] { 1, 1 }, new[] { 1.0, 0.0 }, (Activation)999));
        Should.Throw<ArgumentException>(() => NeuralNetwork.FromGenome(new[] { 1, 1 }, new[] { 1.0 }, Activation.Tanh));

        var network = Network(new[] { 2, 1 }, 1);
        Should.Throw<ArgumentException>(() => network.Forward(new[] { 1.0 }));
        Should.Throw<ArgumentException>(() => network.Forward(new[] { 1.0, 2.0 }, new double[2]));

        var sameShapeNetwork = Network(new[] { 2, 2 }, 1);
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
