namespace NodeRunner.Domain;

/// <summary>
/// The Run lengths a Ranged generation draws from, both ends included, on whole
/// <see cref="TrainSettingsDef.RunLengthStepSeconds"/> steps. An end off the scale is clamped and
/// snapped to the nearest step (a tie goes up), and inverted ends are swapped, never rejected, so a
/// hand-edited file still loads and always holds a step.
/// </summary>
public sealed record RunLengthRangeDef
{
    public RunLengthRangeDef(int lowSeconds, int highSeconds)
    {
        var low = Snap(lowSeconds);
        var high = Snap(highSeconds);
        LowSeconds = Math.Min(low, high);
        HighSeconds = Math.Max(low, high);
    }

    public int LowSeconds { get; }

    public int HighSeconds { get; }

    private static int Snap(int seconds)
    {
        var step = TrainSettingsDef.RunLengthStepSeconds;
        var clamped = Math.Clamp(seconds, TrainSettingsDef.MinRunLengthSeconds, TrainSettingsDef.MaxRunLengthSeconds);
        return (clamped + step / 2) / step * step;
    }
}
