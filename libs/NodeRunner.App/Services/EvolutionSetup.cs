using NodeRunner.Domain;
using NodeRunner.ML.Ga;

namespace NodeRunner.App.Services;

/// <summary>
/// The Evolver's population, genetic algorithm and trial length for a Creation's Train setup
/// values (#617). Training has no profiles: only Shadows and Run length vary.
/// </summary>
public sealed record EvolutionSetup(int Population, GeneticAlgorithm Algorithm, int TrialTicks)
{
    private const int _tournamentSize = 3;
    private const double _mutationRate = 0.1;
    private const double _mutationStrength = 0.3;

    /// <param name="settings">The Creation's values, or null before its first Train setup.</param>
    /// <param name="physicsTicksPerSecond">The engine's fixed physics rate.</param>
    public static EvolutionSetup For(TrainSettingsDef? settings, int physicsTicksPerSecond)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(physicsTicksPerSecond, 1);
        var values = settings ?? TrainSettingsDef.Default;
        // A tournament cannot draw more candidates than there are shadows.
        var algorithm = new GeneticAlgorithm(
            Math.Min(_tournamentSize, values.Shadows),
            _mutationRate,
            _mutationStrength,
            crossoverStrategy: CrossoverStrategy.Uniform);
        return new EvolutionSetup(values.Shadows, algorithm, values.RunLengthSeconds * physicsTicksPerSecond);
    }
}
