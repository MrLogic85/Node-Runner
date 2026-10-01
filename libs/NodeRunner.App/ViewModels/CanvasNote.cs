using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

/// <summary>How a canvas note reads; shares the callout's names (warn, danger, ok).</summary>
public enum CanvasNoteKind
{
    Warning,
    Danger,
    Ok,
}

/// <summary>
/// A short message about one part, shown in the drawing next to that part (a callout with a
/// leader line). The view model decides what to say and about which part; the canvas decides
/// where it goes.
/// </summary>
public sealed record CanvasNote(CanvasNoteKind Kind, CreatureElementSelection Target, string Text);
