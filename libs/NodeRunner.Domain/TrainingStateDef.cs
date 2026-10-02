namespace NodeRunner.Domain;

/// <summary>
/// A Creation's saved training: the best brain found so far, as a graph keyed by port ids
/// (<see cref="BrainDef"/>), and how far training has come.
/// </summary>
public sealed record TrainingStateDef
{
    public TrainingStateDef(BrainDef brain, int generation, double bestFitness, TrainingRunDef bestRun)
    {
        ArgumentNullException.ThrowIfNull(brain);
        ArgumentNullException.ThrowIfNull(bestRun);

        if (generation < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(generation), "Generation cannot be negative.");
        }

        if (!double.IsFinite(bestFitness))
        {
            throw new ArgumentOutOfRangeException(nameof(bestFitness), "Best fitness must be finite.");
        }

        Brain = brain;
        Generation = generation;
        BestFitness = bestFitness;
        BestRun = bestRun;
    }

    /// <summary>The best brain found so far.</summary>
    public BrainDef Brain { get; }

    public int Generation { get; }

    public double BestFitness { get; }

    /// <summary>The winning run behind <see cref="Brain"/>.</summary>
    public TrainingRunDef BestRun { get; }
}
