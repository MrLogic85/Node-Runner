namespace NodeRunner.Domain;

/// <summary>
/// A Creation's saved training (#479): the latest finished generation's best brain, as a graph keyed
/// by port ids (<see cref="BrainDef"/>), and what it measured, plus the best ever reached. Latest is
/// what the creature can do now: the card, Build, Simulate and the warm start (#538) use it, and it
/// may go down after a noisy generation. <see cref="Best"/> never goes down.
/// </summary>
public sealed record TrainingStateDef
{
    public TrainingStateDef(BrainDef brain, int generation, TrainingRunDef latest, TrainingBestDef best)
    {
        ArgumentNullException.ThrowIfNull(brain);
        ArgumentNullException.ThrowIfNull(latest);
        ArgumentNullException.ThrowIfNull(best);
        ArgumentOutOfRangeException.ThrowIfLessThan(generation, 1);
        if (best.Generation > generation)
        {
            throw new ArgumentOutOfRangeException(nameof(best), best.Generation, "The best cannot come from a generation that hasn't finished.");
        }

        Brain = brain;
        Generation = generation;
        Latest = latest;
        Best = best;
    }

    /// <summary>The best brain of the latest finished generation.</summary>
    public BrainDef Brain { get; }

    /// <summary>Finished generations; the latest is this one.</summary>
    public int Generation { get; }

    /// <summary>What <see cref="Brain"/>'s run measured in the latest generation.</summary>
    public TrainingRunDef Latest { get; }

    /// <summary>The best ever reached on the map it trained on.</summary>
    public TrainingBestDef Best { get; }

    /// <summary>
    /// The training after <paramref name="generation"/> finished with <paramref name="latest"/> as its
    /// best run: latest is always replaced, and the best only when <paramref name="latest"/> goes
    /// further on the same map.
    /// </summary>
    public static TrainingStateDef Record(TrainingStateDef? previous, BrainDef brain, int generation, TrainingRunDef latest)
    {
        ArgumentNullException.ThrowIfNull(latest);
        var best = previous?.Best is { } kept && kept.MapId == latest.MapId && kept.Distance >= latest.Distance
            ? kept
            : new TrainingBestDef(generation, latest.Distance, latest.MapId);
        return new TrainingStateDef(brain, generation, latest, best);
    }
}
