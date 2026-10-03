namespace NodeRunner.App.ViewModels;

/// <summary>
/// The selection panel for several selected parts (#558, #704): the counted title, the settings
/// they all share, the Move, Rotate and Scale help while a frame shows, and Delete, which a locked
/// Creation hides. <see cref="EmptyNote"/> shows when there are neither settings nor a frame.
/// </summary>
public sealed record SelectionPanelPresentation(
    string Title,
    IReadOnlyList<SharedSlider> Settings,
    string SettingsNote,
    string EmptyNote,
    bool ShowFrameRows,
    string DeleteText,
    string DeleteNote,
    bool CanDelete);
