using NodeRunner.Domain;

namespace NodeRunner.ML.Brains;

/// <summary>
/// The first generation of a new Creation (#537, #810). Its base brain has no weights and holds the
/// built pose at <see cref="FullStrengthBias"/>, like a robot that starts stiff in its default pose
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
    public static double[] BaseGenome(BrainPortLayout ports)
    {
        ArgumentNullException.ThrowIfNull(ports);

        var genome = DirectBrain.Compile(new BrainDef([], [], nextNeuronId: 1), ports);
        var biasStart = ports.Inputs.Count * ports.Outputs.Count;
        for (var o = 0; o < ports.Outputs.Count; o++)
        {
            if (ports.Outputs[o].Signal == PortSignal.Strength)
            {
                genome[biasStart + o] = FullStrengthBias;
            }
        }

        return genome;
    }

    /// <summary>
    /// <paramref name="size"/> genomes: perturbations of <see cref="BaseGenome"/>, then the base
    /// genome itself last.
    /// </summary>
    public static double[][] Population(BrainPortLayout ports, int size, Random random)
    {
        ArgumentNullException.ThrowIfNull(ports);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentOutOfRangeException.ThrowIfLessThan(size, 1);

        var baseGenome = BaseGenome(ports);
        var population = new double[size][];
        for (var i = 0; i < size - 1; i++)
        {
            population[i] = Perturb(baseGenome, ports, random);
        }

        population[size - 1] = baseGenome;
        return population;
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
