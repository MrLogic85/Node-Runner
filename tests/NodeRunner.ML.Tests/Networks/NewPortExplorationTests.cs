using NodeRunner.Domain;
using NodeRunner.ML.Networks;

namespace NodeRunner.ML.Tests.Networks;

public sealed class NewPortExplorationTests
{
    // A trained Touch sensor (5) and a new one (7); a trained Servo (3), a new Servo (4) and a new
    // Piston (9). Weights output by output (6 outputs × 2 inputs), then 6 biases.
    private static readonly NetworkPortLayout _ports = new(
        [NetworkPorts.TouchInput(5), NetworkPorts.TouchInput(7)],
        [.. NetworkPorts.ServoOutputs(3), .. NetworkPorts.ServoOutputs(4), .. NetworkPorts.PistonOutputs(9)]);

    private const int _inputs = 2;
    private const int _biasStart = 12;
    private const double _drawnLength = 0.3;
    private static readonly Dictionary<int, double> _drawn = new() { [9] = _drawnLength };

    // As a refit leaves it: the trained Servo and sensor keep their values, and every new port
    // has no weights and its almost passive bias.
    private static readonly double[] _elite =
    [
        0.8, 0,
        -1.2, 0,
        0, 0,
        0, 0,
        0, 0,
        0, 0,
        0.4, 2.1,
        0, PortSignals.PassiveStrengthBias,
        0, PortSignals.PassiveStrengthBias,
    ];

    // Every child's new-port genes are redrawn, except disabled ones; the trained Servo's weights from the trained
    // sensor and its biases, and the elites, are kept. The same for a seed.
    [Fact]
    public void Explore_RedrawsOnlyTheNewPortsGenes_InEveryChild()
    {
        int[] trainedGenes = [0, 2, _biasStart, _biasStart + 1];
        // The new sensor's weight into the trained Servo, and the trained sensor's into the new Piston.
        int[] disabled = [1, 4 * _inputs];
        var generation = Generation(_elite, 16);
        var secondElite = generation[1].ToArray();

        NewPortExploration.Explore(generation, 2, _ports, [], new Random(3), _drawn)[..2].ShouldBe([_elite, secondElite]);
        var explored = NewPortExploration.Explore(generation, 1, _ports, disabled, new Random(1), _drawn);

        explored.ShouldBe(NewPortExploration.Explore(generation, 1, _ports, disabled, new Random(1), _drawn));
        for (var slot = 1; slot < explored.Length; slot++)
        {
            for (var gene = 0; gene < _elite.Length; gene++)
            {
                var kept = trainedGenes.Contains(gene) || disabled.Contains(gene);
                (explored[slot][gene] == generation[slot][gene]).ShouldBe(kept, $"shadow {slot + 1}, gene {gene}");
            }
        }
    }

    [Fact]
    public void Explore_DrawsTheNewGenesAsGenerationZeroDoes()
    {
        var explored = NewPortExploration.Explore(Generation(_elite, 4001), 1, _ports, [], new Random(2), _drawn)[1..];
        var baseGenome = GenerationZero.BaseGenome(_ports, _drawn);

        foreach (var (gene, mean, spread) in new[]
        {
            (_biasStart + 3, GenerationZero.FullStrengthBias, GenerationZero.BiasSpread),
            (_biasStart + 5, GenerationZero.FullStrengthBias, GenerationZero.BiasSpread),
            (_biasStart + 2, 0, GenerationZero.BiasSpread),
            (_biasStart + 4, baseGenome[_biasStart + 4], 2 * GenerationZero.BiasSpread),
            ((2 * _inputs) + 0, 0, GenerationZero.WeightSpread),
            ((4 * _inputs) + 1, 0, 2 * GenerationZero.WeightSpread),
            ((0 * _inputs) + 1, 0, GenerationZero.WeightSpread),
        })
        {
            ShouldSpread(explored, gene, mean, spread);
        }

        (1 / (1 + Math.Exp(-baseGenome[_biasStart + 4]))).ShouldBe(_drawnLength, 1e-12);
    }

    // Nothing is new when every part's strength has moved off −4, if only slightly, and every input has a weight;
    // when the best elite says so, even if a later one looks new; or when there is no elite. Then nothing is drawn.
    [Fact]
    public void Explore_WithNothingNew_LeavesTheGenerationAndDrawsNothing()
    {
        double[] trained =
        [
            0.8, 0,
            -1.2, 0,
            0, 0.1,
            0, 0,
            0, 0,
            0, 0,
            0.4, 2.1,
            0, PortSignals.PassiveStrengthBias + 1e-9,
            0, -3.9,
        ];
        var trainedBest = _elite.ToArray();
        trainedBest[_biasStart + 3] = 2;
        trainedBest[_biasStart + 5] = 2;
        trainedBest[1] = 0.5;
        var newSecond = Generation(trainedBest, 8);
        newSecond[1] = _elite.ToArray();
        foreach (var (generation, elites) in new[] { (Generation(trained, 8), 1), (newSecond, 2), (Generation(_elite, 4), 0) })
        {
            var random = new Random(4);
            NewPortExploration.Explore(generation, elites, _ports, [], random, _drawn).ShouldBe(generation);
            random.Next().ShouldBe(new Random(4).Next());
        }
    }

    // An all-zero base reads every input as new, but no output, whose biases are the base's.
    [Fact]
    public void Explore_OfAnAllZeroBase_RedrawsEveryInputsWeights_ButNoTrainedOutput()
    {
        var baseGenome = GenerationZero.BaseGenome(_ports, _drawn);
        var generation = Generation(baseGenome, 8);

        var explored = NewPortExploration.Explore(generation, 1, _ports, [], new Random(6), _drawn);

        explored[0].ShouldBe(baseGenome);
        for (var slot = 1; slot < explored.Length; slot++)
        {
            explored[slot][.._biasStart].ShouldAllBe(weight => weight != 0);
            explored[slot][_biasStart..].ShouldBe(generation[slot][_biasStart..]);
        }
    }

    // A new Stepper reads as new while its frequency has bias 0 and no weight, and its children step either way.
    [Fact]
    public void Explore_RedrawsANewStepper_FromStill_AndLeavesTrainedOnes()
    {
        var ports = new NetworkPortLayout(
            [NetworkPorts.TouchInput(5), NetworkPorts.TouchInput(6)],
            [.. NetworkPorts.StepperOutputs(3), .. NetworkPorts.StepperOutputs(4), .. NetworkPorts.StepperOutputs(7)]);
        // Weights (3 outputs × 2 inputs), then biases: Stepper 3 trained, Stepper 4 as a refit leaves it,
        // Stepper 7 trained to bias 0 but weighted.
        double[] elite = [0.6, -0.9, 0, 0, 0.4, 0, 1.2, 0, 0];
        var generation = Generation(elite, 4001);

        var explored = NewPortExploration.Explore(generation, 1, ports, [], new Random(12))[1..];

        for (var slot = 0; slot < explored.Length; slot++)
        {
            foreach (var trained in (int[])[0, 1, 4, 5, 6, 8])
            {
                explored[slot][trained].ShouldBe(generation[slot + 1][trained]);
            }
        }

        ShouldSpread(explored, 7, 0, GenerationZero.BiasSpread);
        ShouldSpread(explored, 2, 0, GenerationZero.WeightSpread);
        ShouldSpread(explored, 3, 0, GenerationZero.WeightSpread);
    }

    [Fact]
    public void Explore_RefusesGenomesThatDoNotMatchThePorts_AndAnEliteCountOutsideTheGeneration()
    {
        Should.Throw<ArgumentException>(() => NewPortExploration.Explore([[0, 0, 0]], 1, _ports, [], new Random(9)));
        Should.Throw<ArgumentOutOfRangeException>(() => NewPortExploration.Explore(Generation(_elite, 4), -1, _ports, [], new Random(11)));
        Should.Throw<ArgumentOutOfRangeException>(() => NewPortExploration.Explore(Generation(_elite, 4), 5, _ports, [], new Random(11)));
    }

    // The elite, then children as the genetic algorithm leaves them: the elite's genes, a few nudged.
    private static double[][] Generation(double[] elite, int size)
    {
        var random = new Random(99);
        return Enumerable.Range(0, size)
            .Select(slot => slot == 0 ? elite.ToArray() : elite.Select(gene => random.NextDouble() < 0.1 ? gene + 0.3 : gene).ToArray())
            .ToArray();
    }

    private static void ShouldSpread(double[][] genomes, int gene, double mean, double spread)
    {
        var values = genomes.Select(genome => genome[gene]).ToArray();
        var average = values.Average();
        var deviation = Math.Sqrt(values.Select(value => (value - average) * (value - average)).Average());

        average.ShouldBe(mean, spread * 0.1, $"gene {gene} mean");
        deviation.ShouldBe(spread, spread * 0.1, $"gene {gene} spread");
    }
}
