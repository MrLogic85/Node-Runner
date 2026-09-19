namespace NodeRunner.Sim;

/// <summary>
/// Session-scoped training settings exposed by the 0.7.0 training HUD.
/// </summary>
public sealed record TrainingProfile
{
    public TrainingProfile(string name, int populationSize, int trialDurationTicks, int maxGenerations)
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

        Name = name;
        PopulationSize = populationSize;
        TrialDurationTicks = trialDurationTicks;
        MaxGenerations = maxGenerations;
    }

    public string Name { get; }
    public int PopulationSize { get; }
    public int TrialDurationTicks { get; }
    public int MaxGenerations { get; }
}
