namespace NodeRunner.ML.Ga;

/// <summary>
/// Pure genetic-algorithm math: tournament selection, uniform crossover, and
/// Gaussian mutation over flat genome vectors (see
/// <see cref="NeuralNetwork.FlattenGenome"/> / <see cref="NeuralNetwork.FromGenome"/>).
/// Godot-agnostic and fully deterministic given a seeded <see cref="Random"/>.
/// </summary>
public sealed class GeneticAlgorithm
{
    private readonly int _tournamentSize;
    private readonly double _mutationRate;
    private readonly double _mutationStrength;
    private readonly int _elitismCount;

    /// <param name="tournamentSize">How many candidates compete per parent selection. Must be at least 1.</param>
    /// <param name="mutationRate">Per-gene probability of mutation, in [0, 1].</param>
    /// <param name="mutationStrength">Standard deviation of the Gaussian noise added to a mutated gene.</param>
    /// <param name="elitismCount">How many of the fittest genomes carry over to the next generation unchanged. Default 1.</param>
    public GeneticAlgorithm(int tournamentSize, double mutationRate, double mutationStrength, int elitismCount = 1)
    {
        if (tournamentSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(tournamentSize), "Tournament size must be at least 1.");
        }

        if (mutationRate is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(mutationRate), "Mutation rate must be in [0, 1].");
        }

        if (mutationStrength < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(mutationStrength), "Mutation strength must not be negative.");
        }

        if (elitismCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(elitismCount), "Elitism count must not be negative.");
        }

        _tournamentSize = tournamentSize;
        _mutationRate = mutationRate;
        _mutationStrength = mutationStrength;
        _elitismCount = elitismCount;
    }

    /// <summary>
    /// Produces the next generation of genomes from the current generation's
    /// fitness scores: the fittest <c>elitismCount</c> genomes carry over
    /// unchanged, and the rest are filled by tournament-selecting two
    /// parents, uniform-crossing them, and mutating the result.
    /// </summary>
    public double[][] NextGeneration(double[][] genomes, double[] fitness, Random random)
    {
        ArgumentNullException.ThrowIfNull(genomes);
        ArgumentNullException.ThrowIfNull(fitness);
        ArgumentNullException.ThrowIfNull(random);

        if (genomes.Length == 0)
        {
            throw new ArgumentException("Population must not be empty.", nameof(genomes));
        }

        if (genomes.Length != fitness.Length)
        {
            throw new ArgumentException("Genome and fitness counts must match.", nameof(fitness));
        }

        if (_tournamentSize > genomes.Length)
        {
            throw new ArgumentException("Tournament size cannot exceed population size.", nameof(genomes));
        }

        if (_elitismCount > genomes.Length)
        {
            throw new ArgumentException("Elitism count cannot exceed population size.", nameof(genomes));
        }

        var genomeLength = genomes[0].Length;
        for (var i = 1; i < genomes.Length; i++)
        {
            if (genomes[i].Length != genomeLength)
            {
                throw new ArgumentException("All genomes must have the same length.", nameof(genomes));
            }
        }

        var nextGeneration = new double[genomes.Length][];
        var slot = 0;

        foreach (var eliteIndex in RankByFitnessDescending(fitness).Take(_elitismCount))
        {
            nextGeneration[slot] = (double[])genomes[eliteIndex].Clone();
            slot++;
        }

        while (slot < genomes.Length)
        {
            var parentA = TournamentSelect(genomes, fitness, random);
            var parentB = TournamentSelect(genomes, fitness, random);
            var child = UniformCrossover(parentA, parentB, random);
            Mutate(child, random);
            nextGeneration[slot] = child;
            slot++;
        }

        return nextGeneration;
    }

    private double[] TournamentSelect(double[][] genomes, double[] fitness, Random random)
    {
        var bestIndex = random.Next(genomes.Length);
        for (var i = 1; i < _tournamentSize; i++)
        {
            var candidateIndex = random.Next(genomes.Length);
            if (fitness[candidateIndex] > fitness[bestIndex])
            {
                bestIndex = candidateIndex;
            }
        }

        return genomes[bestIndex];
    }

    private static double[] UniformCrossover(double[] parentA, double[] parentB, Random random)
    {
        var child = new double[parentA.Length];
        for (var i = 0; i < child.Length; i++)
        {
            child[i] = random.NextDouble() < 0.5 ? parentA[i] : parentB[i];
        }

        return child;
    }

    private void Mutate(double[] genome, Random random)
    {
        for (var i = 0; i < genome.Length; i++)
        {
            if (random.NextDouble() < _mutationRate)
            {
                genome[i] += NextGaussian(random) * _mutationStrength;
            }
        }
    }

    // Box-Muller transform: turns two uniform samples into one standard
    // normal sample. u1 is drawn from (0, 1] (never exactly 0) so Log(u1)
    // is always defined.
    private static double NextGaussian(Random random)
    {
        var u1 = 1.0 - random.NextDouble();
        var u2 = random.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }

    private static IEnumerable<int> RankByFitnessDescending(double[] fitness)
    {
        return Enumerable.Range(0, fitness.Length).OrderByDescending(i => fitness[i]);
    }
}
