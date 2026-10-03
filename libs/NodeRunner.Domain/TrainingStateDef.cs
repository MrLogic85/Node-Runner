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
    /// best run: latest is always replaced, and the best only when <paramref name="latest"/> scores
    /// higher on the same map: its centre went further. The best's front distance is the furthest
    /// any latest run's front got on that map, whichever run holds the score (#725).
    /// </summary>
    public static TrainingStateDef Record(TrainingStateDef? previous, BrainDef brain, int generation, TrainingRunDef latest)
    {
        ArgumentNullException.ThrowIfNull(latest);
        var kept = previous?.Best is { } previousBest && previousBest.MapId == latest.MapId ? previousBest : null;
        var front = Furthest(kept?.FrontDistance, latest.FrontDistance);
        var best = kept is not null && kept.Distance >= latest.Distance
            ? new TrainingBestDef(kept.Generation, kept.Distance, kept.MapId, front)
            : new TrainingBestDef(generation, latest.Distance, latest.MapId, front);
        return new TrainingStateDef(brain, generation, latest, best);
    }

    private static double? Furthest(double? kept, double? latest) =>
        kept is { } a && latest is { } b ? Math.Max(a, b) : kept ?? latest;
}
