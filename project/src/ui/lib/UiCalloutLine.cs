namespace NodeRunner.Ui.Lib;

/// <summary>One live value in a callout line: a short label and a <see cref="UiMeterBar"/>.</summary>
/// <param name="Label">Shown as given, already in the player's language.</param>
/// <param name="Value">−1…1 when <paramref name="Centred"/>, else 0…1; NaN shows no fill.</param>
/// <param name="Centred">True for a bar centred on 0, false for one filling from empty.</param>
public readonly record struct UiCalloutMeter(string Label, double Value, bool Centred);

/// <summary>
/// A line under a callout's caption: meters filled in one colour, at most
/// <see cref="MetersPerRow"/> to a row and the rest wrapping below, or one muted note. A meter
/// line may start with a <see cref="Heading"/> naming its group.
/// </summary>
public sealed record UiCalloutLine
{
    /// <summary>The most meters a row holds; more wrap to the next row of the same line.</summary>
    public const int MetersPerRow = 3;

    private UiCalloutLine(UiTokens.Color color, IReadOnlyList<UiCalloutMeter> meters, string? note, string? heading)
    {
        Color = color;
        Meters = meters;
        Note = note;
        Heading = heading;
    }

    /// <summary>The meters' fill colour.</summary>
    public UiTokens.Color Color { get; }

    public IReadOnlyList<UiCalloutMeter> Meters { get; }

    /// <summary>A muted note shown instead of meters, already in the player's language; null for a meter line.</summary>
    public string? Note { get; }

    /// <summary>
    /// A muted word before the meters' first row, in the column under the caption's glyph, already
    /// in the player's language; null for none. It names the line, so colour is never the only cue.
    /// </summary>
    public string? Heading { get; }

    public static UiCalloutLine OfMeters(UiTokens.Color color, IReadOnlyList<UiCalloutMeter> meters, string? heading = null)
    {
        ArgumentNullException.ThrowIfNull(meters);
        return new UiCalloutLine(color, meters, null, heading);
    }

    public static UiCalloutLine OfNote(string note)
    {
        ArgumentNullException.ThrowIfNull(note);
        return new UiCalloutLine(UiTokens.Color.Muted, [], note, null);
    }

    /// <summary>
    /// Whether <paramref name="other"/> has the same line kinds and meter counts as <paramref name="lines"/>;
    /// <see cref="UiCalloutGrid.SameGrid"/> decides whether a callout updates in place.
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
