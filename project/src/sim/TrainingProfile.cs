using NodeRunner.ML.Ga;

namespace NodeRunner.Sim;

/// <summary>
/// Session-scoped training settings exposed by the 0.7.0 training HUD.
/// </summary>
public sealed record TrainingProfile
{
    public TrainingProfile(
        string name,
        int populationSize,
        int trialDurationTicks,
        int maxGenerations,
        double mutationRate,
        double mutationStrength,
        int tournamentSize,
        CrossoverStrategy crossoverStrategy)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A training profile needs a name.", nameof(name));
        }

        if (populationSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(populationSize));
        }

        if (trialDurationTicks < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(trialDurationTicks));
        }

        if (maxGenerations < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxGenerations));
        }

        if (!double.IsFinite(mutationRate) || mutationRate is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(mutationRate));
        }

        if (!double.IsFinite(mutationStrength) || mutationStrength < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(mutationStrength));
        }

        if (tournamentSize < 1 || tournamentSize > populationSize)
        {
            throw new ArgumentOutOfRangeException(nameof(tournamentSize));
        }

        if (!Enum.IsDefined(crossoverStrategy))
        {
            throw new ArgumentOutOfRangeException(nameof(crossoverStrategy));
        }

        Name = name;
        PopulationSize = populationSize;
        TrialDurationTicks = trialDurationTicks;
        MaxGenerations = maxGenerations;
        MutationRate = mutationRate;
        MutationStrength = mutationStrength;
        TournamentSize = tournamentSize;
        CrossoverStrategy = crossoverStrategy;
    }

    public string Name { get; }
    public int PopulationSize { get; }
    public int TrialDurationTicks { get; }
    public int MaxGenerations { get; }
    public double MutationRate { get; }
    public double MutationStrength { get; }
    public int TournamentSize { get; }
    public CrossoverStrategy CrossoverStrategy { get; }
}
