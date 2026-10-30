using NodeRunner.Domain;
using NodeRunner.ML.Ga;

namespace NodeRunner.App.Services;

/// <summary>
/// The Evolver's population, genetic algorithm and trial length for a Creation's Train setup
/// values. Training has no profiles: only Shadows and Run length vary. The Evolver asks
/// <see cref="NextTrialTicks"/> for each generation's length with its own seeded RNG
/// (docs/TRAINING_LOOP.md → "Generations").
/// </summary>
public sealed record EvolutionSetup(int Population, GeneticAlgorithm Algorithm, Func<Random, int> NextTrialTicks)
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
        Func<Random, int> nextTrialTicks = values.RunLengthRange is { } range && values.RunLengthMode == RunLengthMode.Ranged
            ? rng => DrawSeconds(range, rng) * physicsTicksPerSecond
            : _ => values.RunLengthSeconds * physicsTicksPerSecond;
        return new EvolutionSetup(values.Shadows, algorithm, nextTrialTicks);
    }

    /// <summary>One whole step in <paramref name="range"/>, both ends included, uniform over the steps; one value from <paramref name="rng"/>.</summary>
    public static int DrawSeconds(RunLengthRangeDef range, Random rng)
    {
        ArgumentNullException.ThrowIfNull(range);
        ArgumentNullException.ThrowIfNull(rng);
        var steps = (range.HighSeconds - range.LowSeconds) / TrainSettingsDef.RunLengthStepSeconds + 1;
        return range.LowSeconds + TrainSettingsDef.RunLengthStepSeconds * rng.Next(steps);
    }
}
