using NodeRunner.Domain;

namespace NodeRunner.ML.Brains;

/// <summary>
/// The first generation of a new Creation (#537, #810). Its base brain has no weights and holds the
/// built pose at <see cref="FullStrengthBias"/>: each length output's bias asks for the Piston's
/// drawn length (#870, #1129), like a robot that starts stiff in its default pose
/// and learns offsets from it. Generation 0 never changes it. The last shadow runs it unchanged, as a
/// reference near 0; every other shadow is a wide random perturbation of it. Parts added to a
/// trained brain later start almost passive instead (<see cref="DirectBrain"/>, #535), and every
/// child of a trained elite redraws them as generation 0 would (<see cref="NewPortExploration"/>,
/// #1180). See docs/TRAINING_LOOP.md.
/// </summary>
public static class GenerationZero
{
    /// <summary>Standard deviation of the noise added to every weight; twice it into a length output (#1129).</summary>
    public const double WeightSpread = 1.5;

    /// <summary>Standard deviation of the noise added to every bias; twice it on a length output (#1129).</summary>
    public const double BiasSpread = 0.5;

    /// <summary>The bias every strength output starts at: about 95% of its Strength.</summary>
    public const double FullStrengthBias = 3;

    // A length output's sigmoid takes twice the input a tanh would for the same length, as
    // (tanh(x) + 1) / 2 = sigmoid(2x) (#1129). Its bias limit and noise are scaled by it, so a
    // Piston starts and explores as it did when its output was tanh.
    private const double _lengthScale = 2;

    /// <summary>The brain a new Creation starts from, as a genome for <paramref name="ports"/>.</summary>
    /// <param name="ports">The creature's brain ports.</param>
    /// <param name="drawnPositions">
    /// By part id, the length output, 0…1, that asks for the part's drawn length: a Piston's
    /// <c>Piston.DrawnPosition</c> in NodeRunner.Mechanics. A part not in it starts in the middle of
    /// its travel.
    /// </param>
    public static double[] BaseGenome(BrainPortLayout ports, IReadOnlyDictionary<int, double>? drawnPositions = null)
    {
        ArgumentNullException.ThrowIfNull(ports);

        var genome = DirectBrain.Compile(new BrainDef([], [], nextNeuronId: 1), ports);
        var biasStart = ports.Inputs.Count * ports.Outputs.Count;
        for (var o = 0; o < ports.Outputs.Count; o++)
        {
            var output = ports.Outputs[o];
            if (output.Signal == PortSignal.Strength)
            {
                genome[biasStart + o] = FullStrengthBias;
            }
            else if (output.Signal == PortSignal.Length && drawnPositions?.TryGetValue(output.PartId, out var drawn) == true)
            {
                genome[biasStart + o] = LengthBias(drawn);
            }
        }

        return genome;
    }

    /// <summary>
    /// <paramref name="size"/> genomes: perturbations of <see cref="BaseGenome"/>, then the base
    /// genome itself last.
    /// </summary>
    public static double[][] Population(BrainPortLayout ports, int size, Random random, IReadOnlyDictionary<int, double>? drawnPositions = null)
    {
        ArgumentNullException.ThrowIfNull(ports);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentOutOfRangeException.ThrowIfLessThan(size, 1);

        var baseGenome = BaseGenome(ports, drawnPositions);
        var population = new double[size][];
        for (var i = 0; i < size - 1; i++)
        {
            population[i] = Perturb(baseGenome, ports, random);
        }

        population[size - 1] = baseGenome;
        return population;
    }

    // The sigmoid bias that outputs drawn, its logit. An end of the travel would need an endless
    // bias, so it stops at tanh(3) of the way from the middle to the end, about 0.25% short of it.
    private static double LengthBias(double drawn)
    {
        var limit = _lengthScale * FullStrengthBias;
        var share = Math.Clamp(drawn, Sigmoid(-limit), Sigmoid(limit));
        return Math.Log(share / (1 - share));
    }

    private static double Sigmoid(double x) => 1 / (1 + Math.Exp(-x));

    private static double[] Perturb(double[] baseGenome, BrainPortLayout ports, Random random)
    {
        var genome = baseGenome.ToArray();
        for (var i = 0; i < genome.Length; i++)
        {
            genome[i] += Noise(i, ports, random);
        }

        return genome;
    }

    /// <summary>
    /// The noise generation 0 adds to gene <paramref name="gene"/>: <see cref="WeightSpread"/> on a
    /// weight, <see cref="BiasSpread"/> on a bias, twice that into a length output (#1129).
    /// </summary>
    internal static double Noise(int gene, BrainPortLayout ports, Random random)
    {
        var biasStart = ports.Inputs.Count * ports.Outputs.Count;
        var output = ports.Outputs[gene < biasStart ? gene / ports.Inputs.Count : gene - biasStart];
        var scale = output.Signal == PortSignal.Length ? _lengthScale : 1;
        return Gaussian.Next(random) * scale * (gene < biasStart ? WeightSpread : BiasSpread);
    }
}
