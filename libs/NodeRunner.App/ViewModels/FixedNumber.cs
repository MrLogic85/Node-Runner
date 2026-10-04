namespace NodeRunner.App.ViewModels;

/// <summary>
/// A decimal number for a <see cref="UiText"/> argument, shown with <see cref="Decimals"/> digits
/// after the point, such as 2.5 for "{0} m". Whole numbers are plain <c>int</c> or <c>long</c>
/// arguments. The value is kept rounded as shown, so two numbers that read the same are equal;
/// a negative that rounds to zero reads 0.0, not -0.0. Godot writes the digits for the player's
/// language (#756).
/// </summary>
public readonly record struct FixedNumber
{
    public FixedNumber(double value, int decimals)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "A shown number must be finite.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(decimals);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(decimals, MaxDecimals);
        var shown = Math.Round(value, decimals, MidpointRounding.AwayFromZero);
        Value = shown == 0 ? 0 : shown;
        Decimals = decimals;
    }

    /// <summary>The most decimals a shown number has; more is noise on a phone.</summary>
    public const int MaxDecimals = 3;

    public double Value { get; }

    public int Decimals { get; }
}
