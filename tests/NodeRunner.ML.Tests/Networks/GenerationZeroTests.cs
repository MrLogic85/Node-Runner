using NodeRunner.Domain;
using NodeRunner.ML.Networks;

namespace NodeRunner.ML.Tests.Networks;

public sealed class GenerationZeroTests
{
    // Two inputs and two Pistons: weights output by output (4 outputs × 2 inputs), then 4 biases.
    private static readonly NetworkPortLayout _ports = new(
        [NetworkPort.Input(5, "along"), NetworkPort.Input(9, NetworkPorts.PistonLengthChannel)],
        [.. NetworkPorts.PistonOutputs(9), .. NetworkPorts.PistonOutputs(10)]);

    private const int _biasStart = 8;
    private const double _full = GenerationZero.FullStrengthBias;

    // The base genome holds the built pose at full strength: no weights, position biases 0, strength biases full
    // (a Servo's too), and a Stepper starts still (#1263).
    [Fact]
    public void BaseGenome_HoldsTheBuiltPoseAtFullStrength()
    {
        GenerationZero.BaseGenome(_ports).ShouldBe([0, 0, 0, 0, 0, 0, 0, 0, 0, _full, 0, _full]);
        GenerationZero.BaseGenome(new(NetworkPorts.ServoInputs(4).ToArray(), NetworkPorts.ServoOutputs(4).ToArray())).ShouldBe([0, 0, 0, 0, 0, _full]);
        GenerationZero.BaseGenome(new(NetworkPorts.StepperInputs(4).ToArray(), NetworkPorts.StepperOutputs(4).ToArray())).ShouldBe([0, 0, 0, 0]);

        var drawn = GenerationZero.BaseGenome(_ports, new Dictionary<int, double> { [9] = 0.25, [10] = 1 });
        Sigmoid(drawn[_biasStart]).ShouldBe(0.25, 1e-12);
        // An end needs an endless bias: it stops where tanh(3) stopped before #1129, 0.25% short.
        Sigmoid(drawn[_biasStart + 2]).ShouldBe((Math.Tanh(_full) + 1) / 2, 1e-12);
        drawn[_biasStart + 1].ShouldBe(_full);
        drawn[..8].ShouldAllBe(weight => weight == 0);
    }

    // The base genome runs unchanged last, with its drawn positions; every other shadow moves every output's
    // weights and bias, with strength kept near full. The same for a seed.
    [Fact]
    public void Population_PerturbsAllButTheLastShadow()
    {
        var baseGenome = GenerationZero.BaseGenome(_ports);
        var drawn = new Dictionary<int, double> { [9] = 0.4 };

        var population = GenerationZero.Population(_ports, 32, new Random(2));

        population.Length.ShouldBe(32);
        population[^1].ShouldBe(baseGenome);
        GenerationZero.Population(_ports, 8, new Random(1), drawn)[^1].ShouldBe(GenerationZero.BaseGenome(_ports, drawn));
        GenerationZero.Population(_ports, 1, new Random(6)).ShouldBe([baseGenome]);
        population.ShouldBe(GenerationZero.Population(_ports, 32, new Random(2)));
        foreach (var genome in population[..^1])
        {
            genome.Length.ShouldBe(baseGenome.Length);
            for (var output = 0; output < 4; output++)
            {
                genome.Skip(output * 2).Take(2).ShouldNotBe(baseGenome.Skip(output * 2).Take(2), $"output {output} weights");
                genome[_biasStart + output].ShouldNotBe(baseGenome[_biasStart + output], $"output {output} bias");
            }
        }

        var strengths = population.SelectMany(genome => new[] { genome[_biasStart + 1], genome[_biasStart + 3] }).ToArray();
        strengths.Average().ShouldBe(_full, GenerationZero.BiasSpread / 2);
        strengths.ShouldAllBe(bias => Math.Abs(bias - _full) < GenerationZero.BiasSpread * 5);
    }

    // #1129: (tanh(x) + 1) / 2 = sigmoid(2x), so a length output's noise is twice a position output's, and its
    // shadows ask for the same lengths as when it was tanh. Without strength outputs it still perturbs.
    [Fact]
    public void Population_SpreadsALengthOutputAsWidelyAsTheTanhItReplaced()
    {
        static NetworkPortLayout Layout(PortSignal signal) => new([NetworkPort.Input(5, "along")], [NetworkPort.Output(9, NetworkPorts.PistonPositionChannel, signal)]);

        var lengths = GenerationZero.Population(Layout(PortSignal.Length), 8, new Random(11));
        var positions = GenerationZero.Population(Layout(PortSignal.Position), 8, new Random(11));

        for (var shadow = 0; shadow < 8; shadow++)
        {
            lengths[shadow].ShouldBe(positions[shadow].Select(gene => 2 * gene).ToArray(), 1e-12);
        }

        lengths[0].ShouldNotBe(lengths[^1]);
        lengths[^1].ShouldBe([0, 0]);
    }

    [Fact]
    public void Population_WithoutOutputs_IsEmptyGenomes_AndOfNoneIsRefused()
    {
        GenerationZero.Population(new([NetworkPort.Input(5, "along")], []), 4, new Random(9)).ShouldAllBe(genome => genome.Length == 0);
        Should.Throw<ArgumentOutOfRangeException>(() => GenerationZero.Population(_ports, 0, new Random(8)));
    }

    private static double Sigmoid(double x) => 1 / (1 + Math.Exp(-x));
}
