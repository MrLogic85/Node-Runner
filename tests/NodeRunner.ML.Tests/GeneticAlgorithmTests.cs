using NodeRunner.ML.Ga;

namespace NodeRunner.ML.Tests;

public sealed class GeneticAlgorithmTests
{
    [Fact]
    public void ItRefuses_BadSettingsAndPopulations()
    {
        foreach (var create in new Func<GeneticAlgorithm>[]
        {
            () => new(0, 0.1, 0.1), () => new(2, -0.1, 0.1), () => new(2, 1.1, 0.1), () => new(2, 0.1, -0.1),
            () => new(2, 0.1, 0.1, elitismCount: -1), () => new(2, 0.1, 0.1, crossoverStrategy: (CrossoverStrategy)99),
        })
        {
            Should.Throw<ArgumentOutOfRangeException>(() => create());
        }

        double[][] pair = [[1, 2], [3, 4]];
        foreach (var (ga, genomes, fitness) in new (GeneticAlgorithm, double[][], double[])[]
        {
            (new(2, 0.1, 0.1), [], []),
            (new(2, 0.1, 0.1), pair, [1]),
            (new(5, 0.1, 0.1), pair, [1, 2]),
            (new(1, 0.1, 0.1, elitismCount: 5), pair, [1, 2]),
            (new(1, 0.1, 0.1), [[1, 2], [3, 4, 5]], [1, 2]),
        })
        {
            Should.Throw<ArgumentException>(() => ga.NextGeneration(genomes, fitness, new Random(1)));
        }

        var elitesGa = new GeneticAlgorithm(3, 0.1, 0.1);
        Should.Throw<ArgumentException>(() => elitesGa.FromElites([], 4, new Random(5)));
        Should.Throw<ArgumentException>(() => elitesGa.FromElites([[1, 2], [1]], 4, new Random(5)));
        Should.Throw<ArgumentOutOfRangeException>(() => elitesGa.FromElites([[1, 2]], 0, new Random(5)));
    }

    // A creature with no powered parts has nothing to evolve, but its generations still run.
    [Fact]
    public void Generations_OfEmptyGenomes_KeepThePopulation()
    {
        var ga = new GeneticAlgorithm(2, 0.5, 1);

        var next = ga.NextGeneration([[], [], []], [0.1, 0.3, 0.2], new Random(1));

        next.Length.ShouldBe(3);
        next.ShouldAllBe(genome => genome.Length == 0);
        ga.FromElites([[]], 3, new Random(2)).ShouldAllBe(genome => genome.Length == 0);
    }

    // The fittest valid genomes open the next generation unchanged, however hard the rest mutate; an invalid one
    // (NaN or infinite) is never kept. It keeps the population's size and genome length, leaves its input alone and
    // is the same for a seed.
    [Fact]
    public void NextGeneration_CarriesTheFittestValidGenomesOverUnchanged()
    {
        var ga = new GeneticAlgorithm(2, mutationRate: 1, mutationStrength: 5, elitismCount: 2);
        double[][] genomes = [[1, 1], [2, 2], [100, 100]];
        foreach (var (fitness, elites) in new[]
        {
            (new[] { 0.1, 0.3, 0.2 }, new[] { 1, 2 }),
            ([1, 2, 999], [2, 1]),
            ([double.NaN, 0.3, double.NegativeInfinity], [1]),
            ([double.NaN, double.PositiveInfinity, double.NegativeInfinity], []),
        })
        {
            ga.EliteCount(fitness).ShouldBe(elites.Length);
            var next = ga.NextGeneration(genomes, fitness, new Random(3));
            next.Take(elites.Length).ShouldBe(elites.Select(index => genomes[index]));
            next.Skip(elites.Length).ShouldAllBe(child => child.Length == 2 && !child.SequenceEqual(genomes[0]) && !child.SequenceEqual(genomes[2]));
            next.Length.ShouldBe(3);
            next.ShouldBe(ga.NextGeneration(genomes, fitness, new Random(3)));
        }

        genomes.ShouldBe([[1, 1], [2, 2], [100, 100]]);
    }

    // Without mutation every child gene comes from a parent: uniform crossover takes one parent's gene, Blend one
    // strictly between them. With Random(2) both tournaments land on distinct parents for
    // both children, so a uniform fallback, which can only copy a parent's gene, fails it.
    [Fact]
    public void Crossover_TakesEveryGeneFromTheParents()
    {
        var uniform = new GeneticAlgorithm(3, mutationRate: 0, mutationStrength: 1, elitismCount: 0);
        uniform.NextGeneration([[1, 1, 1], [2, 2, 2], [3, 3, 3]], [1, 2, 3], new Random(7)).SelectMany(genome => genome).ShouldAllBe(gene => gene == 1 || gene == 2 || gene == 3);

        var blend = new GeneticAlgorithm(1, mutationRate: 0, mutationStrength: 1, elitismCount: 0, crossoverStrategy: CrossoverStrategy.Blend);
        double[][] parents = [[0, 10], [10, 20]];
        foreach (var child in blend.NextGeneration(parents, [1, 2], new Random(2)))
        {
            for (var gene = 0; gene < 2; gene++)
            {
                child[gene].ShouldBeInRange(parents[0][gene], parents[1][gene]);
                child[gene].ShouldNotBe(parents[0][gene]);
                child[gene].ShouldNotBe(parents[1][gene]);
            }
        }
    }

    // A generation from saved elites opens with copies of them; the rest are their mutated children, which with one
    // parent keep some of its genes and without mutation only recombine theirs. The same for a seed.
    [Fact]
    public void FromElites_OpensWithTheElites_AndFillsTheRestWithTheirChildren()
    {
        double[][] elites = [[1, 2, 3, 4, 5, 6], [4, 5, 6, 7, 8, 9]];
        var ga = new GeneticAlgorithm(3, 0.5, 1);

        var generation = ga.FromElites(elites, 8, new Random(1));

        generation.Length.ShouldBe(8);
        generation[..2].ShouldBe(elites);
        generation[0].ShouldNotBeSameAs(elites[0]);
        generation.ShouldBe(ga.FromElites(elites, 8, new Random(1)));
        foreach (var child in ga.FromElites([elites[0]], 8, new Random(2))[1..])
        {
            child.ShouldNotBe(elites[0]);
            child.Where((gene, i) => gene == elites[0][i]).ShouldNotBeEmpty();
        }

        new GeneticAlgorithm(3, 0, 0).FromElites([[0, 0, 0, 0], [1, 1, 1, 1]], 16, new Random(3)).SelectMany(genome => genome).ShouldAllBe(gene => gene == 0 || gene == 1);
    }

    // Genome i is [i] with fitness i, and without mutation each child gene is one tournament winner's:
    // k draws with replacement from N pick the fittest with probability 1 - (1 - 1/N)^k, and a winner's
    // mean is the expected best of k draws. So even a tournament as large as the population can miss it.
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(10)]
    public void NextGeneration_TournamentPicksTheFittestOfKDrawsWithReplacement(int tournamentSize)
    {
        const int population = 10;
        var ga = new GeneticAlgorithm(tournamentSize, mutationRate: 0.0, mutationStrength: 1.0, elitismCount: 0);
        var genomes = Enumerable.Range(0, population).Select(i => new double[] { i }).ToArray();
        var fitness = Enumerable.Range(0, population).Select(i => (double)i).ToArray();
        var random = new Random(967);

        var winners = Enumerable.Range(0, 2000).SelectMany(_ => ga.NextGeneration(genomes, fitness, random)).Select(child => child[0]).ToArray();

        var expectedMean = Enumerable.Range(1, population - 1).Sum(j => 1 - Math.Pow((double)j / population, tournamentSize));
        winners.Average().ShouldBe(expectedMean, 0.1);
        ((double)winners.Count(gene => gene == population - 1) / winners.Length).ShouldBe(1 - Math.Pow(1 - (1.0 / population), tournamentSize), 0.02);
    }

    [Fact]
    public void NextGeneration_MutatesEachGeneAtTheRate_ByGaussianNoiseOfTheStrength()
    {
        const double rate = 0.25;
        const double sigma = 0.5;
        var ga = new GeneticAlgorithm(1, mutationRate: rate, mutationStrength: sigma, elitismCount: 0);
        double[][] genomes = [new double[1000], new double[1000]];

        var genes = Enumerable.Range(0, 40).SelectMany(seed => ga.NextGeneration(genomes, [0, 0], new Random(seed))).SelectMany(child => child).ToArray();

        var mutated = genes.Where(gene => gene != 0).ToArray();
        ((double)mutated.Length / genes.Length).ShouldBe(rate, 0.01);
        mutated.Average().ShouldBe(0, 0.02);
        Math.Sqrt(mutated.Average(gene => gene * gene)).ShouldBe(sigma, 0.02);
        ((double)mutated.Count(gene => Math.Abs(gene) <= sigma) / mutated.Length).ShouldBe(0.6827, 0.02);
    }
}
