namespace NodeRunner.Domain;

/// <summary>
/// Persisted neural-network state for a Creation. The layer and activation
/// metadata make the genome self-describing without coupling Domain to ML.
/// </summary>
public sealed record TrainingStateDef
{
    public TrainingStateDef(int[] layerSizes, double[] bestGenome, int generation, string activation, double bestFitness, TrainingRunDef bestRun)
    {
        ArgumentNullException.ThrowIfNull(layerSizes);
        ArgumentNullException.ThrowIfNull(bestGenome);
        ArgumentException.ThrowIfNullOrWhiteSpace(activation);
        ArgumentNullException.ThrowIfNull(bestRun);

        if (layerSizes.Length < 2 || layerSizes.Any(size => size <= 0))
        {
            throw new ArgumentException("Training layers must contain at least two positive sizes.", nameof(layerSizes));
        }

        if (generation < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(generation), "Generation cannot be negative.");
        }

        if (!double.IsFinite(bestFitness))
        {
            throw new ArgumentOutOfRangeException(nameof(bestFitness), "Best fitness must be finite.");
        }

        LayerSizes = layerSizes.ToArray();
        BestGenome = bestGenome.ToArray();
        Generation = generation;
        Activation = activation;
        BestFitness = bestFitness;
        BestRun = bestRun;
    }

    public int[] LayerSizes { get; }

    public double[] BestGenome { get; }

    public int Generation { get; }

    public string Activation { get; }

    public double BestFitness { get; }

    /// <summary>The winning run behind <see cref="BestGenome"/>.</summary>
    public TrainingRunDef BestRun { get; }
}
