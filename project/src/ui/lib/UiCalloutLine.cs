namespace NodeRunner.Ui.Lib;

/// <summary>One live value in a callout line (#1064): a short label and a <see cref="UiMeterBar"/>.</summary>
/// <param name="Label">Shown as given, already in the player's language.</param>
/// <param name="Value">−1…1 when <paramref name="Centred"/>, else 0…1; NaN shows no fill.</param>
/// <param name="Centred">True for a bar centred on 0, false for one filling from empty.</param>
public readonly record struct UiCalloutMeter(string Label, double Value, bool Centred);

/// <summary>
/// A line under a callout's caption (#1064): meters filled in one colour, at most
/// <see cref="MetersPerRow"/> to a row and the rest wrapping below, or one muted note.
/// </summary>
public sealed record UiCalloutLine
{
    /// <summary>The most meters a row holds; more wrap to the next row of the same line.</summary>
    public const int MetersPerRow = 3;

    private UiCalloutLine(UiTokens.Color color, IReadOnlyList<UiCalloutMeter> meters, string? note)
    {
        Color = color;
        Meters = meters;
        Note = note;
    }

    /// <summary>The meters' fill colour.</summary>
    public UiTokens.Color Color { get; }

    public IReadOnlyList<UiCalloutMeter> Meters { get; }

    /// <summary>A muted note shown instead of meters, already in the player's language; null for a meter line.</summary>
    public string? Note { get; }

    public static UiCalloutLine OfMeters(UiTokens.Color color, IReadOnlyList<UiCalloutMeter> meters)
    {
        ArgumentNullException.ThrowIfNull(meters);
        return new UiCalloutLine(color, meters, null);
    }

    public static UiCalloutLine OfNote(string note)
    {
        ArgumentNullException.ThrowIfNull(note);
        return new UiCalloutLine(UiTokens.Color.Muted, [], note);
    }

    /// <summary>
    /// Whether <paramref name="other"/> has the same lines and meter counts as
    /// <paramref name="lines"/>, so a callout updates its labels and values in place.
    /// </summary>
    public static bool SameShape(IReadOnlyList<UiCalloutLine>? lines, IReadOnlyList<UiCalloutLine>? other)
    {
        IReadOnlyList<UiCalloutLine> these = lines ?? [];
        IReadOnlyList<UiCalloutLine> those = other ?? [];
        return these.Count == those.Count
            && these.Zip(those).All(pair => (pair.First.Note is null) == (pair.Second.Note is null)
                && pair.First.Meters.Count == pair.Second.Meters.Count);
    }
}
