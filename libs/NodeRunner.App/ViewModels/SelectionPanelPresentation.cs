namespace NodeRunner.App.ViewModels;

/// <summary>
/// The selection panel for several selected parts (#558, #704): the counted title, the settings
/// they all share, the Move, Rotate and Scale help while a frame shows, and Delete, which a locked
/// Creation hides. <see cref="EmptyNote"/> shows when there are neither settings nor a frame; a
/// note with nothing to say is null.
/// </summary>
public sealed record SelectionPanelPresentation(
    UiText Title,
    IReadOnlyList<ParameterSlider> Settings,
    UiText? SettingsNote,
    UiText? EmptyNote,
    bool ShowFrameRows,
    UiText DeleteText,
    UiText? DeleteNote,
    bool CanDelete);
