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
    public void BaseGenome_IsThePassiveBrain()
    {
        GenerationZero.BaseGenome(_ports).ShouldBe(
        [
            0, 0, 0, 0, 0, 0, 0, 0,
            0, PortSignals.PassiveStrengthBias, 0, PortSignals.PassiveStrengthBias,
        ]);
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

            genome[_biasStart].ShouldNotBe(baseGenome[_biasStart]);
            genome[_biasStart + 2].ShouldNotBe(baseGenome[_biasStart + 2]);
        }
    }

    [Fact]
    public void Population_WakesAStrengthOutputInEveryPerturbedShadow()
    {
        foreach (var genome in GenerationZero.Population(_ports, 32, new Random(3))[..^1])
        {
            _strengthOutputs.ShouldContain(o =>
                genome[_biasStart + o] >= GenerationZero.MovingStrengthBias
                && genome[_biasStart + o] <= GenerationZero.FullStrengthBias);
        }
    }

    [Fact]
    public void Population_WakesEachStrengthOutputSomewhere()
    {
        var population = GenerationZero.Population(_ports, 32, new Random(4));

        foreach (var o in _strengthOutputs)
        {
            population.ShouldContain(genome => genome[_biasStart + o] >= GenerationZero.MovingStrengthBias);
        }
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
        var ports = new BrainPortLayout([BrainPort.Input(5, "along")], [BrainPort.Output(9, BrainPorts.PistonPositionChannel, PortSignal.Position)]);

        var population = GenerationZero.Population(ports, 4, new Random(7));

        population[0].ShouldNotBe(population[^1]);
        population[^1].ShouldBe([0, 0]);
    }

    [Fact]
    public void Population_RejectsAnEmptyPopulation()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => GenerationZero.Population(_ports, 0, new Random(8)));
    }
}
