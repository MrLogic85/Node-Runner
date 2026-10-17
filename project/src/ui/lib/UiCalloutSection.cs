namespace NodeRunner.Ui.Lib;

/// <summary>
/// One more caption in a callout, under a divider: an optional glyph, a caption and its
/// <see cref="UiCalloutLine"/>s, laid out as the callout's own caption and lines.
/// </summary>
/// <param name="IconId">The glyph before the caption, or <see cref="UiIconId.None"/>.</param>
/// <param name="Text">Shown as given, already in the player's language.</param>
/// <param name="Lines">The lines under the caption, or null for none.</param>
public sealed record UiCalloutSection(UiIconId IconId, string Text, IReadOnlyList<UiCalloutLine>? Lines = null);
