using NodeRunner.Domain;

namespace NodeRunner.ML.Brains;

/// <summary>
/// The first generation of a new Creation (#537). Its base brain is the passive brain every new
/// port starts with (<see cref="DirectBrain"/>, #535): no weights, so it holds the built pose with
/// almost no force. Generation 0 never changes it. The last shadow runs it unchanged, as a
/// reference near 0; every other shadow is a wide random perturbation of it. Each perturbed
/// shadow wakes one strength output past <see cref="MovingStrengthBias"/>, so every generation 0
/// has shadows that move. See docs/TRAINING_LOOP.md.
/// </summary>
public static class GenerationZero
{
    /// <summary>Standard deviation of the noise added to every weight.</summary>
    public const double WeightSpread = 1.5;

    /// <summary>Standard deviation of the noise added to every bias.</summary>
    public const double BiasSpread = 0.5;

    /// <summary>The lowest bias a woken strength output gets: about 27% of its Strength.</summary>
    public const double MovingStrengthBias = -1;

    /// <summary>The highest bias a woken strength output gets: about 95% of its Strength.</summary>
    public const double FullStrengthBias = 3;

    /// <summary>The passive brain a new Creation starts from, as a genome for <paramref name="ports"/>.</summary>
    public static double[] BaseGenome(BrainPortLayout ports) =>
        DirectBrain.Compile(new BrainDef([], [], nextNeuronId: 1), ports);

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

        var strengthOutputs = Enumerable.Range(0, ports.Outputs.Count)
            .Where(o => ports.Outputs[o].Signal == PortSignal.Strength)
            .ToArray();
        if (strengthOutputs.Length > 0)
        {
            var woken = strengthOutputs[random.Next(strengthOutputs.Length)];
            genome[biasStart + woken] = MovingStrengthBias + (random.NextDouble() * (FullStrengthBias - MovingStrengthBias));
        }

        return genome;
    }
}
