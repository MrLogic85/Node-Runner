using NodeRunner.Domain;
using NodeRunner.ML.Brains;

namespace NodeRunner.ML.Tests.Brains;

public sealed class GenerationZeroTests
{
    // Two inputs and two Pistons: weights output by output (4 outputs × 2 inputs), then 4 biases.
    private static readonly BrainPortLayout _ports = new(
        [BrainPort.Input(5, "along"), BrainPort.Input(9, BrainPorts.PistonLengthChannel)],
        [.. BrainPorts.PistonOutputs(9), .. BrainPorts.PistonOutputs(10)]);

    private const int _biasStart = 8;
    private static readonly int[] _strengthOutputs = [1, 3];

    [Fact]
    public void BaseGenome_HoldsTheBuiltPoseAtFullStrength()
    {
        GenerationZero.BaseGenome(_ports).ShouldBe(
        [
            0, 0, 0, 0, 0, 0, 0, 0,
            0, GenerationZero.FullStrengthBias, 0, GenerationZero.FullStrengthBias,
        ]);
    }

    [Fact]
    public void BaseGenome_AsksEachPistonInTheDrawnPositionsForItsDrawnLength()
    {
        var genome = GenerationZero.BaseGenome(_ports, new Dictionary<int, double> { [9] = 0.25, [10] = 1 });

        Sigmoid(genome[_biasStart]).ShouldBe(0.25, tolerance: 1e-12);
        // An end needs an endless bias: it stops where tanh(3) stopped before #1129, 0.25% short.
        Sigmoid(genome[_biasStart + 2]).ShouldBe((Math.Tanh(GenerationZero.FullStrengthBias) + 1) / 2, tolerance: 1e-12);
        genome[_biasStart + 1].ShouldBe(GenerationZero.FullStrengthBias);
        genome[..8].ShouldAllBe(weight => weight == 0);
    }

    // #1129: (tanh(x) + 1) / 2 = sigmoid(2x), so a length output's noise is twice a position
    // output's, and its shadows ask for the same lengths as when it was tanh.
    [Fact]
    public void Population_SpreadsALengthOutputAsWidelyAsTheTanhItReplaced()
    {
        static BrainPortLayout Layout(PortSignal signal) => new([BrainPort.Input(5, "along")], [BrainPort.Output(9, BrainPorts.PistonPositionChannel, signal)]);

        var lengths = GenerationZero.Population(Layout(PortSignal.Length), 8, new Random(11));
        var positions = GenerationZero.Population(Layout(PortSignal.Position), 8, new Random(11));

        for (var shadow = 0; shadow < 8; shadow++)
        {
            lengths[shadow].ShouldBe(positions[shadow].Select(gene => 2 * gene).ToArray(), tolerance: 1e-12);
        }
    }

    [Fact]
    public void Population_RunsTheBaseGenomeWithItsDrawnPositionsUnchangedLast()
    {
        var drawn = new Dictionary<int, double> { [9] = 0.4 };

        GenerationZero.Population(_ports, 8, new Random(1), drawn)[^1].ShouldBe(GenerationZero.BaseGenome(_ports, drawn));
    }

    [Fact]
    public void Population_RunsTheBaseGenomeUnchangedLast()
    {
        var population = GenerationZero.Population(_ports, 32, new Random(1));

        population.Length.ShouldBe(32);
        population[^1].ShouldBe(GenerationZero.BaseGenome(_ports));
    }

    [Fact]
    public void Population_PerturbsEveryOtherShadowInBothPositionAndStrength()
    {
        var baseGenome = GenerationZero.BaseGenome(_ports);

        foreach (var genome in GenerationZero.Population(_ports, 32, new Random(2))[..^1])
        {
            genome.Length.ShouldBe(baseGenome.Length);
            for (var o = 0; o < 4; o++)
            {
                genome.Skip(o * 2).Take(2).ShouldNotBe(baseGenome.Skip(o * 2).Take(2), $"output {o} weights");
            }

            for (var o = 0; o < 4; o++)
            {
                genome[_biasStart + o].ShouldNotBe(baseGenome[_biasStart + o], $"output {o} bias");
            }
        }
    }

    [Fact]
    public void Population_KeepsEveryStrengthOutputNearFullStrength()
    {
        var population = GenerationZero.Population(_ports, 32, new Random(3));
        var biases = population.SelectMany(genome => _strengthOutputs.Select(o => genome[_biasStart + o])).ToArray();

        biases.Average().ShouldBe(GenerationZero.FullStrengthBias, tolerance: GenerationZero.BiasSpread / 2);
        biases.ShouldAllBe(bias => Math.Abs(bias - GenerationZero.FullStrengthBias) < GenerationZero.BiasSpread * 5);
    }

    [Fact]
    public void Population_IsDeterministicForASeed()
    {
        GenerationZero.Population(_ports, 8, new Random(5)).ShouldBe(GenerationZero.Population(_ports, 8, new Random(5)));
    }

    [Fact]
    public void Population_OfOne_IsJustTheBaseGenome()
    {
        GenerationZero.Population(_ports, 1, new Random(6)).ShouldBe([GenerationZero.BaseGenome(_ports)]);
    }

    [Fact]
    public void Population_WithoutStrengthOutputs_StillPerturbs()
    {
        var ports = new BrainPortLayout([BrainPort.Input(5, "along")], [BrainPort.Output(9, BrainPorts.PistonPositionChannel, PortSignal.Length)]);

        var population = GenerationZero.Population(ports, 4, new Random(7));

        population[0].ShouldNotBe(population[^1]);
        population[^1].ShouldBe([0, 0]);
    }

    [Fact]
    public void Population_WithoutOutputs_IsEmptyGenomes()
    {
        var ports = new BrainPortLayout([BrainPort.Input(5, "along")], []);

        GenerationZero.Population(ports, 4, new Random(9)).ShouldAllBe(genome => genome.Length == 0);
    }

    [Fact]
    public void BaseGenome_TreatsServoStrengthLikeOtherStrengthOutputs()
    {
        var ports = new BrainPortLayout(BrainPorts.ServoInputs(4).ToArray(), BrainPorts.ServoOutputs(4).ToArray());

        GenerationZero.BaseGenome(ports).ShouldBe([0, 0, 0, 0, 0, GenerationZero.FullStrengthBias]);
    }

    // #1036: a Stepper starts at half its Max frequency and pushes neither way.
    [Fact]
    public void BaseGenome_StepsEachStepperAtHalfItsMaxFrequency_WithNoPower()
    {
        var ports = new BrainPortLayout(BrainPorts.StepperInputs(4).ToArray(), BrainPorts.StepperOutputs(4).ToArray());

        GenerationZero.BaseGenome(ports).ShouldBe([0, 0, 0, 0, 0, 0, GenerationZero.HalfFrequencyBias, 0]);
        Sigmoid(GenerationZero.HalfFrequencyBias).ShouldBe(0.5);
    }

    [Fact]
    public void Population_RejectsAnEmptyPopulation()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => GenerationZero.Population(_ports, 0, new Random(8)));
    }

    private static double Sigmoid(double x) => 1 / (1 + Math.Exp(-x));
}
