using System.Text.Json.Serialization;

namespace NodeRunner.Domain;

/// <summary>
/// What a Creation trains with, chosen in Train setup: how many shadows race each
/// generation and how long each run lasts. The genetic algorithm's other settings are fixed.
/// </summary>
public sealed record TrainSettingsDef
{
    /// <summary>Training needs one shadow to keep the best network and one to try something new.</summary>
    public const int MinShadows = 2;

    public const int MaxShadows = 100;

    public const int MinRunLengthSeconds = 5;

    public const int MaxRunLengthSeconds = 60;

    /// <summary>The Run length slider's step, and the steps a Ranged generation draws on.</summary>
    public const int RunLengthStepSeconds = 5;

    /// <summary>The Shadows default in Settings until the player changes it.</summary>
    public const int DefaultShadows = 8;

    /// <summary>The Shadows and Run length used when neither the Creation nor Settings gives one.</summary>
    public static TrainSettingsDef Default { get; } = new(DefaultShadows, 10);

    /// <summary>Fixed at <paramref name="runLengthSeconds"/>, with no range yet.</summary>
    public TrainSettingsDef(int shadows, int runLengthSeconds)
        : this(shadows, runLengthSeconds, RunLengthMode.Fixed, null)
    {
    }

    /// <summary>A Ranged Run length needs its range; a Fixed one keeps it, or null if Ranged was never chosen.</summary>
    [JsonConstructor]
    public TrainSettingsDef(int shadows, int runLengthSeconds, RunLengthMode runLengthMode, RunLengthRangeDef? runLengthRange)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(shadows, MinShadows);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(shadows, MaxShadows);
        ArgumentOutOfRangeException.ThrowIfLessThan(runLengthSeconds, MinRunLengthSeconds);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(runLengthSeconds, MaxRunLengthSeconds);
        if (!Enum.IsDefined(runLengthMode))
        {
            throw new ArgumentOutOfRangeException(nameof(runLengthMode));
        }

        if (runLengthMode == RunLengthMode.Ranged && runLengthRange is null)
        {
            throw new ArgumentException("A Ranged Run length needs a range.", nameof(runLengthRange));
        }

        Shadows = shadows;
        RunLengthSeconds = runLengthSeconds;
        RunLengthMode = runLengthMode;
        RunLengthRange = runLengthRange;
    }

    public int Shadows { get; }

    /// <summary>The Fixed Run length, kept while <see cref="RunLengthMode"/> is Ranged.</summary>
    public int RunLengthSeconds { get; }

    public RunLengthMode RunLengthMode { get; }

    /// <summary>The Ranged lengths; null until Ranged is first chosen, then kept while Fixed.</summary>
    public RunLengthRangeDef? RunLengthRange { get; }
}
