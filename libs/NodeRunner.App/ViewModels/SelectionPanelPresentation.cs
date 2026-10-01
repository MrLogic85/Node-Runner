namespace NodeRunner.App.ViewModels;

/// <summary>
/// The selection panel for several selected joints (#558). The scene explains the Move, Rotate and
/// Scale handles; this carries the counted title and the group Delete, which a locked Creation hides.
/// </summary>
public sealed record SelectionPanelPresentation(
    string Title,
    string DeleteText,
    string DeleteNote,
    bool CanDelete);
