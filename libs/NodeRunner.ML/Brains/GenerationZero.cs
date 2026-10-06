using NodeRunner.Domain;

namespace NodeRunner.ML.Brains;

/// <summary>
/// The first generation of a new Creation (#537, #810). Its base brain has no weights and holds the
/// built pose at <see cref="FullStrengthBias"/>: each position output's bias asks for the part's
/// drawn pose (#870), like a robot that starts stiff in its default pose
/// and learns offsets from it. Generation 0 never changes it. The last shadow runs it unchanged, as a
/// reference near 0; every other shadow is a wide random perturbation of it. Parts added to a
/// trained brain later start almost passive instead (<see cref="DirectBrain"/>, #535). See
/// docs/TRAINING_LOOP.md.
/// </summary>
public static class GenerationZero
{
    /// <summary>Standard deviation of the noise added to every weight.</summary>
    public const double WeightSpread = 1.5;

    /// <summary>Standard deviation of the noise added to every bias.</summary>
    public const double BiasSpread = 0.5;

    /// <summary>The bias every strength output starts at: about 95% of its Strength.</summary>
    public const double FullStrengthBias = 3;

    /// <summary>The brain a new Creation starts from, as a genome for <paramref name="ports"/>.</summary>
    /// <param name="ports">The creature's brain ports.</param>
    /// <param name="drawnPositions">
    /// By part id, the position output that asks for the part's drawn pose, such as a Piston's
    /// <c>Piston.DrawnPosition</c> in NodeRunner.Mechanics. A part not in it holds at 0.
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
            else if (output.Signal == PortSignal.Position && drawnPositions?.TryGetValue(output.PartId, out var drawn) == true)
            {
                genome[biasStart + o] = PositionBias(drawn);
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

    // The tanh bias that outputs drawn. An end of the range would need an endless bias, so it stops
    // as close to the end as a strength output's bias comes to full strength.
    private static double PositionBias(double drawn)
    {
        var limit = Math.Tanh(FullStrengthBias);
        return Math.Atanh(Math.Clamp(drawn, -limit, limit));
    }

    private static double[] Perturb(double[] baseGenome, BrainPortLayout ports, Random random)
    {
        var genome = baseGenome.ToArray();
        var biasStart = ports.Inputs.Count * ports.Outputs.Count;
        for (var i = 0; i < genome.Length; i++)
        {
            genome[i] += Gaussian.Next(random) * (i < biasStart ? WeightSpread : BiasSpread);
        }

        return genome;
    }
}
