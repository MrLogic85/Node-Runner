using System.Reflection;

namespace NodeRunner.ML.Tests;

public sealed class NeuralNetworkTests
{
    private const double _tolerance = 1e-12;

    // Hand-worked forward passes: hidden layers use the network's activation, outputs Tanh unless each output names
    // its own, and the genome is every layer's weights then biases.
    [Fact]
    public void Forward_MatchesHandWorkedValues()
    {
        foreach (var (layers, genome, activation, input, expected) in new (int[], double[], Activation, double[], double)[]
        {
            ([1, 1, 1], [1, 0, 1, 0], Activation.Tanh, [1], Math.Tanh(Math.Tanh(1))),
            ([1, 1, 1], [-1, 0, 1, 0], Activation.ReLU, [1], 0),
            ([1, 1, 1], [1, 0, 1, 0], Activation.Sigmoid, [0], Math.Tanh(0.5)),
            ([1, 1], [100, 0], Activation.ReLU, [1], Math.Tanh(100)),
            ([2, 1], [1, -1, 0], Activation.Tanh, [0.75, 0.25], Math.Tanh(0.5)),
            ([0, 1], [0.25], Activation.Tanh, [], Math.Tanh(0.25)),
        })
        {
            NeuralNetwork.FromGenome(layers, genome, activation).Forward(input)[0].ShouldBe(expected, _tolerance);
        }

        var perOutput = NeuralNetwork.FromGenome([1, 2], [1, 1, 0, -4], Activation.Tanh, [Activation.Tanh, Activation.Sigmoid]);
        var output = perOutput.Forward([0.5]);
        output[0].ShouldBe(Math.Tanh(0.5), _tolerance);
        output[1].ShouldBe(1 / (1 + Math.Exp(3.5)), _tolerance);
        perOutput.CaptureActivations([0.5])[1].ShouldBe(output);
        perOutput.Clone().OutputActivations.ShouldBe([Activation.Tanh, Activation.Sigmoid]);

        var deep = NeuralNetwork.FromGenome([2, 2, 1], [1, 0, 0, 1, 0, 0, 1, -1, 0], Activation.Tanh);
        var activations = deep.CaptureActivations([0.25, -0.75]);
        activations.Length.ShouldBe(3);
        activations[0].ShouldBe([0.25, -0.75]);
        activations[1].ShouldBe([Math.Tanh(0.25), Math.Tanh(-0.75)], _tolerance);
        activations[2][0].ShouldBe(Math.Tanh(Math.Tanh(0.25) - Math.Tanh(-0.75)), _tolerance);
        var buffer = new double[1];
        deep.Forward([0.25, -0.75], buffer, new double[2], new double[2]);
        buffer[0].ShouldBe(activations[2][0], _tolerance);
        deep.Forward([0.75, 0.25], buffer);
        buffer[0].ShouldBe(Math.Tanh(Math.Tanh(0.75) - Math.Tanh(0.25)), _tolerance);
    }

    // A creature with no powered parts has no outputs, and one with no sensors or parts no inputs (#845).
    [Fact]
    public void Forward_WithAnEmptyLayer_ReturnsNoOutputs()
    {
        NeuralNetwork.GenomeLength([2, 0]).ShouldBe(0);
        NeuralNetwork.FromGenome([2, 0], [], Activation.Tanh).Forward([0.5, -0.5]).ShouldBeEmpty();
        NeuralNetwork.FromGenome([0, 0], [], Activation.Tanh).Forward([]).ShouldBeEmpty();
    }

    [Fact]
    public void Genome_RoundTripsInAStableLayout_AndIsCopiedIn()
    {
        int[] layers = [2, 2, 1];
        double[] genome = [0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9];
        var network = NeuralNetwork.FromGenome(layers, genome, Activation.Tanh);

        network.Weights[0].ShouldBe([0.1, 0.2, 0.3, 0.4]);
        network.Biases[0].ShouldBe([0.5, 0.6]);
        network.Weights[1].ShouldBe([0.7, 0.8]);
        network.Biases[1].ShouldBe([0.9]);
        NeuralNetwork.FromGenome(network.LayerSizes, network.FlattenGenome(), Activation.Tanh).FlattenGenome().ShouldBe(genome);

        layers[0] = 99;
        genome[0] = 99;
        network.LayerSizes.ShouldBe([2, 2, 1]);
        network.FlattenGenome()[0].ShouldBe(0.1);
    }

    [Fact]
    public void Clone_IsIndependent()
    {
        var original = NeuralNetwork.FromGenome([1, 1], [2, 0], Activation.Tanh);
        var clone = original.Clone();

        var weights = typeof(NeuralNetwork).GetField("_weights", BindingFlags.Instance | BindingFlags.NonPublic)!;
        ((double[][])weights.GetValue(clone)!)[0][0] = 99;

        clone.Forward([1])[0].ShouldNotBe(original.Forward([1])[0]);
    }

    [Fact]
    public void ItRefuses_BadShapesAndBuffers()
    {
        Should.Throw<ArgumentException>(() => NeuralNetwork.FromGenome([1], [], Activation.Tanh));
        Should.Throw<ArgumentOutOfRangeException>(() => NeuralNetwork.FromGenome([1, -1], [], Activation.Tanh));
        Should.Throw<ArgumentNullException>(() => NeuralNetwork.FromGenome([1, 1], null!, Activation.Tanh));
        Should.Throw<ArgumentOutOfRangeException>(() => NeuralNetwork.FromGenome([1, 1], [1, 0], (Activation)999));
        Should.Throw<ArgumentException>(() => NeuralNetwork.FromGenome([1, 1], [1], Activation.Tanh));
        Should.Throw<ArgumentException>(() => NeuralNetwork.FromGenome([1, 2], [1, 1, 0, 0], Activation.Tanh, [Activation.Sigmoid]));

        Should.Throw<ArgumentException>(() => Seeded([2, 1], 1).Forward([1]));
        Should.Throw<ArgumentException>(() => Seeded([2, 1], 1).Forward([1, 2], new double[2]));
        var network = Seeded([2, 2], 1);
        double[] input = [1, 2];
        var output = new double[2];
        var scratch = new double[2];
        Should.Throw<ArgumentException>(() => network.Forward(input, input));
        foreach (var (scratchA, scratchB) in new[] { (input, new double[2]), (new double[2], input), (output, new double[2]), (new double[2], output), (scratch, scratch), (new double[1], new double[2]) })
        {
            Should.Throw<ArgumentException>(() => network.Forward(input, output, scratchA, scratchB));
        }
    }

    private static NeuralNetwork Seeded(int[] layers, int seed)
    {
        var random = new Random(seed);
        var genome = Enumerable.Range(0, NeuralNetwork.GenomeLength(layers)).Select(_ => (random.NextDouble() * 2) - 1).ToArray();
        return NeuralNetwork.FromGenome(layers, genome, Activation.Tanh);
    }
}
