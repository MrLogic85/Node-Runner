using NodeRunner.ML.Ga;

namespace NodeRunner.ML.Tests;

public sealed class GeneticAlgorithmTests
{
    [Fact]
    public void Constructor_RejectsInvalidParameters()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new GeneticAlgorithm(0, 0.1, 0.1));
        Should.Throw<ArgumentOutOfRangeException>(() => new GeneticAlgorithm(2, -0.1, 0.1));
        Should.Throw<ArgumentOutOfRangeException>(() => new GeneticAlgorithm(2, 1.1, 0.1));
        Should.Throw<ArgumentOutOfRangeException>(() => new GeneticAlgorithm(2, 0.1, -0.1));
        Should.Throw<ArgumentOutOfRangeException>(() => new GeneticAlgorithm(2, 0.1, 0.1, elitismCount: -1));
        Should.Throw<ArgumentOutOfRangeException>(() => new GeneticAlgorithm(2, 0.1, 0.1, crossoverStrategy: (CrossoverStrategy)99));
    }

    [Fact]
    public void NextGeneration_RejectsEmptyPopulation()
    {
        var ga = new GeneticAlgorithm(2, 0.1, 0.1);

        Should.Throw<ArgumentException>(() => ga.NextGeneration(Array.Empty<double[]>(), Array.Empty<double>(), new Random(1)));
    }

    [Fact]
    public void NextGeneration_RejectsMismatchedFitnessLength()
    {
        var ga = new GeneticAlgorithm(2, 0.1, 0.1);
        var genomes = new[] { new double[] { 1, 2 }, new double[] { 3, 4 } };

        Should.Throw<ArgumentException>(() => ga.NextGeneration(genomes, new[] { 1.0 }, new Random(1)));
    }

    [Fact]
    public void NextGeneration_RejectsTournamentSizeLargerThanPopulation()
    {
        var ga = new GeneticAlgorithm(5, 0.1, 0.1);
        var genomes = new[] { new double[] { 1, 2 }, new double[] { 3, 4 } };

        Should.Throw<ArgumentException>(() => ga.NextGeneration(genomes, new[] { 1.0, 2.0 }, new Random(1)));
    }

    [Fact]
    public void NextGeneration_RejectsElitismCountLargerThanPopulation()
    {
        var ga = new GeneticAlgorithm(1, 0.1, 0.1, elitismCount: 5);
        var genomes = new[] { new double[] { 1, 2 }, new double[] { 3, 4 } };

        Should.Throw<ArgumentException>(() => ga.NextGeneration(genomes, new[] { 1.0, 2.0 }, new Random(1)));
    }

    [Fact]
    public void NextGeneration_RejectsGenomesOfDifferentLengths()
    {
        var ga = new GeneticAlgorithm(1, 0.1, 0.1);
        var genomes = new[] { new double[] { 1, 2 }, new double[] { 3, 4, 5 } };

        Should.Throw<ArgumentException>(() => ga.NextGeneration(genomes, new[] { 1.0, 2.0 }, new Random(1)));
    }

    [Fact]
    public void NextGeneration_ReturnsSamePopulationSizeAndGenomeLength()
    {
        var ga = new GeneticAlgorithm(3, 0.1, 0.1);
        var genomes = new[]
        {
            new double[] { 1, 2, 3 },
            new double[] { 4, 5, 6 },
            new double[] { 7, 8, 9 },
            new double[] { 10, 11, 12 },
        };
        var fitness = new double[] { 1, 5, 3, 2 };

        var next = ga.NextGeneration(genomes, fitness, new Random(42));

        next.Length.ShouldBe(genomes.Length);
        foreach (var genome in next)
        {
            genome.Length.ShouldBe(3);
        }
    }

    [Fact]
    public void NextGeneration_WithZeroMutationRate_OnlyProducesGenesFromParents()
    {
        var ga = new GeneticAlgorithm(3, mutationRate: 0.0, mutationStrength: 1.0, elitismCount: 0);
        var genomes = new[]
        {
            new double[] { 1, 1, 1 },
            new double[] { 2, 2, 2 },
            new double[] { 3, 3, 3 },
        };
        var fitness = new double[] { 1, 2, 3 };

        var next = ga.NextGeneration(genomes, fitness, new Random(7));

        var possibleGeneValues = new HashSet<double> { 1, 2, 3 };
        foreach (var genome in next)
        {
            foreach (var gene in genome)
            {
                possibleGeneValues.ShouldContain(gene);
            }
        }
    }

    [Fact]
    public void NextGeneration_WithBlendCrossover_InterpolatesBetweenDistinctParentGenes()
    {
        // Regression guard for #112: with Random(2) both tournament
        // selections land on distinct parents for both children, so a
        // correct Blend crossover must produce genes strictly between the
        // two parent values. Uniform crossover (the accidental fallback
        // this guards against) can only ever reproduce a parent's gene
        // exactly, so it would fail this assertion -- unlike the previous
        // "stays within range" assertion, which uniform crossover also
        // always satisfies.
        var ga = new GeneticAlgorithm(1, mutationRate: 0.0, mutationStrength: 1.0, elitismCount: 0, crossoverStrategy: CrossoverStrategy.Blend);
        var genomes = new[]
        {
            new double[] { 0, 10 },
            new double[] { 10, 20 },
        };

        var next = ga.NextGeneration(genomes, new[] { 1.0, 2.0 }, new Random(2));

        for (var genomeIndex = 0; genomeIndex < next.Length; genomeIndex++)
        {
            for (var geneIndex = 0; geneIndex < next[genomeIndex].Length; geneIndex++)
            {
                var gene = next[genomeIndex][geneIndex];
                var parentAGene = genomes[0][geneIndex];
                var parentBGene = genomes[1][geneIndex];
                gene.ShouldBeInRange(parentAGene, parentBGene);
                gene.ShouldNotBe(parentAGene);
                gene.ShouldNotBe(parentBGene);
            }
        }
    }

    [Fact]
    public void NextGeneration_WithElitism_CarriesFittestGenomeUnchanged()
    {
        var ga = new GeneticAlgorithm(2, mutationRate: 1.0, mutationStrength: 5.0, elitismCount: 1);
        var genomes = new[]
        {
            new double[] { 1, 1 },
            new double[] { 2, 2 },
            new double[] { 100, 100 },
        };
        var fitness = new double[] { 1, 2, 999 };

        var next = ga.NextGeneration(genomes, fitness, new Random(3));

        next[0].ShouldBe(new double[] { 100, 100 });
    }

    [Fact]
    public void NextGeneration_WithSameSeed_IsDeterministic()
    {
        var ga = new GeneticAlgorithm(2, 0.3, 0.2, elitismCount: 1);
        var genomes = new[]
        {
            new double[] { 1, 2, 3 },
            new double[] { 4, 5, 6 },
            new double[] { 7, 8, 9 },
        };
        var fitness = new double[] { 0.2, 0.7, 0.4 };

        var first = ga.NextGeneration(genomes, fitness, new Random(99));
        var second = ga.NextGeneration(genomes, fitness, new Random(99));

        for (var i = 0; i < first.Length; i++)
        {
            first[i].ShouldBe(second[i]);
        }
    }

    [Fact]
    public void NextGeneration_DoesNotMutateInputGenomesArray()
    {
        var ga = new GeneticAlgorithm(2, 1.0, 1.0, elitismCount: 1);
        var genomes = new[]
        {
            new double[] { 1, 2 },
            new double[] { 3, 4 },
        };
        var original = new[] { (double[])genomes[0].Clone(), (double[])genomes[1].Clone() };
        var fitness = new double[] { 1, 2 };

        ga.NextGeneration(genomes, fitness, new Random(5));

        genomes[0].ShouldBe(original[0]);
        genomes[1].ShouldBe(original[1]);
    }
}
