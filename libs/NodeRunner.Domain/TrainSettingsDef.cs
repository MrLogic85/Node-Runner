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

    /// <summary>The Shadows default in Settings until the player changes it.</summary>
    public const int DefaultShadows = 8;

    /// <summary>The Shadows and Run length used when neither the Creation nor Settings gives one.</summary>
    public static TrainSettingsDef Default { get; } = new(DefaultShadows, 10);

    public TrainSettingsDef(int shadows, int runLengthSeconds)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(shadows, MinShadows);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(shadows, MaxShadows);
        ArgumentOutOfRangeException.ThrowIfLessThan(runLengthSeconds, MinRunLengthSeconds);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(runLengthSeconds, MaxRunLengthSeconds);

        Shadows = shadows;
        RunLengthSeconds = runLengthSeconds;
    }

    public int Shadows { get; }

    public int RunLengthSeconds { get; }
}
