namespace NodeRunner.Domain;

/// <summary>
/// What a Creation trains with, chosen in Train setup (#194, #617): how many shadows race each
/// generation and how long each run lasts. Shadows and run length replace the old training
/// profiles; the genetic algorithm's other settings are fixed.
/// </summary>
public sealed record TrainSettingsDef
{
    /// <summary>Training needs one shadow to keep the best brain and one to try something new.</summary>
    public const int MinShadows = 2;

    public const int MaxShadows = 32;

    public const int MinRunLengthSeconds = 5;

    public const int MaxRunLengthSeconds = 60;

    /// <summary>What Train setup offers a Creation that has not trained yet, until Settings stores a default (#379).</summary>
    public static TrainSettingsDef Default { get; } = new(8, 10);

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
