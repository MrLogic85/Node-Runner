using NodeRunner.Domain;
using NodeRunner.ML.Brains;

namespace NodeRunner.ML.Tests.Brains;

public sealed class NewPortExplorationTests
{
    // A trained Touch sensor (5) and a new one (7); a trained Servo (3), a new Servo (4) and a new
    // Piston (9). Weights output by output (6 outputs × 2 inputs), then 6 biases.
    private static readonly BrainPortLayout _ports = new(
        [BrainPorts.TouchInput(5), BrainPorts.TouchInput(7)],
        [.. BrainPorts.ServoOutputs(3), .. BrainPorts.ServoOutputs(4), .. BrainPorts.PistonOutputs(9)]);

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

    // The trained Servo's weights from the trained sensor, and its biases.
    private static readonly int[] _trainedGenes = [0, 2, _biasStart, _biasStart + 1];

    [Fact]
    public void Explore_RedrawsTheNewPortsGenesInEveryChild_AndKeepsEveryOtherGene()
    {
        var generation = Generation(_elite, 16);

        var explored = NewPortExploration.Explore(generation, 1, _ports, [], new Random(1), _drawn);

        for (var slot = 1; slot < explored.Length; slot++)
        {
            for (var gene = 0; gene < _elite.Length; gene++)
            {
                if (_trainedGenes.Contains(gene))
                {
                    explored[slot][gene].ShouldBe(generation[slot][gene], $"shadow {slot + 1}, gene {gene}");
                }
                else
                {
                    explored[slot][gene].ShouldNotBe(generation[slot][gene], $"shadow {slot + 1}, gene {gene}");
                }
            }
        }
    }

    [Fact]
    public void Explore_DrawsTheNewGenesAsGenerationZeroDoes()
    {
        var explored = NewPortExploration.Explore(Generation(_elite, 4001), 1, _ports, [], new Random(2), _drawn)[1..];
        var baseGenome = GenerationZero.BaseGenome(_ports, _drawn);

        ShouldSpread(explored, _biasStart + 3, GenerationZero.FullStrengthBias, GenerationZero.BiasSpread);
        ShouldSpread(explored, _biasStart + 5, GenerationZero.FullStrengthBias, GenerationZero.BiasSpread);
        ShouldSpread(explored, _biasStart + 2, 0, GenerationZero.BiasSpread);
        ShouldSpread(explored, _biasStart + 4, baseGenome[_biasStart + 4], 2 * GenerationZero.BiasSpread);
        ShouldSpread(explored, (2 * _inputs) + 0, 0, GenerationZero.WeightSpread);
        ShouldSpread(explored, (4 * _inputs) + 1, 0, 2 * GenerationZero.WeightSpread);
        ShouldSpread(explored, (0 * _inputs) + 1, 0, GenerationZero.WeightSpread);
        Sigmoid(baseGenome[_biasStart + 4]).ShouldBe(_drawnLength, tolerance: 1e-12);
    }

    [Fact]
    public void Explore_LeavesEveryEliteUnchanged()
    {
        var generation = Generation(_elite, 8);
        var secondElite = generation[1].ToArray();

        var explored = NewPortExploration.Explore(generation, 2, _ports, [], new Random(3), _drawn);

        explored[0].ShouldBe(_elite);
        explored[1].ShouldBe(secondElite);
        explored[2].ShouldNotBe(generation[2]);
    }

    [Fact]
    public void Explore_NeverTouchesATrainedPartOrSensor_AndDrawsNothing()
    {
        // Every part's strength has moved off −4, if only slightly, and every input has a weight.
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
        var generation = Generation(trained, 8);
        var random = new Random(4);

        var explored = NewPortExploration.Explore(generation, 1, _ports, [], random, _drawn);

        explored.ShouldBe(generation);
        random.Next().ShouldBe(new Random(4).Next());
    }

    [Fact]
    public void Explore_ReadsWhichPortsAreNewFromTheBestElite()
    {
        var trained = _elite.ToArray();
        trained[_biasStart + 3] = 2;
        trained[_biasStart + 5] = 2;
        trained[1] = 0.5;
        var generation = Generation(trained, 8);
        generation[1] = _elite.ToArray();

        NewPortExploration.Explore(generation, 2, _ports, [], new Random(5), _drawn).ShouldBe(generation);
    }

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

    [Fact]
    public void Explore_NeverRedrawsADisabledGene()
    {
        // The new sensor's weight into the trained Servo, and the trained sensor's into the new Piston.
        int[] disabled = [1, 4 * _inputs];
        var generation = Generation(_elite, 8);

        var explored = NewPortExploration.Explore(generation, 1, _ports, disabled, new Random(7), _drawn);

        for (var slot = 1; slot < explored.Length; slot++)
        {
            explored[slot][1].ShouldBe(generation[slot][1]);
            explored[slot][4 * _inputs].ShouldBe(generation[slot][4 * _inputs]);
            explored[slot][(4 * _inputs) + 1].ShouldNotBe(generation[slot][(4 * _inputs) + 1]);
        }
    }

    [Fact]
    public void Explore_IsDeterministicForASeed()
    {
        NewPortExploration.Explore(Generation(_elite, 8), 1, _ports, [], new Random(8), _drawn)
            .ShouldBe(NewPortExploration.Explore(Generation(_elite, 8), 1, _ports, [], new Random(8), _drawn));
    }

    [Fact]
    public void Explore_RejectsGenomesThatDoNotMatchThePorts()
    {
        Should.Throw<ArgumentException>(() => NewPortExploration.Explore([[0, 0, 0]], 1, _ports, [], new Random(9)));
    }

    [Fact]
    public void Explore_WithoutAnElite_ExploresNothing()
    {
        var generation = Generation(_elite, 4);
        var random = new Random(10);

        NewPortExploration.Explore(generation, 0, _ports, [], random, _drawn).ShouldBe(generation);
        random.Next().ShouldBe(new Random(10).Next());
    }

    [Fact]
    public void Explore_RejectsAnEliteCountOutsideTheGeneration()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => NewPortExploration.Explore(Generation(_elite, 4), -1, _ports, [], new Random(11)));
        Should.Throw<ArgumentOutOfRangeException>(() => NewPortExploration.Explore(Generation(_elite, 4), 5, _ports, [], new Random(11)));
    }

    // #1263: a new Stepper reads as new while its frequency has bias 0 and no weight, and its children step either way.
    [Fact]
    public void Explore_RedrawsANewStepper_FromStill_AndLeavesTrainedOnes()
    {
        var ports = new BrainPortLayout(
            [BrainPorts.TouchInput(5), BrainPorts.TouchInput(6)],
            [.. BrainPorts.StepperOutputs(3), .. BrainPorts.StepperOutputs(4), .. BrainPorts.StepperOutputs(7)]);
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

        average.ShouldBe(mean, tolerance: spread * 0.1, $"gene {gene} mean");
        deviation.ShouldBe(spread, tolerance: spread * 0.1, $"gene {gene} spread");
    }

    private static double Sigmoid(double x) => 1 / (1 + Math.Exp(-x));
}
