namespace NodeRunner.App.ViewModels;

/// <summary>
/// The selection panel for several selected parts (#558, #704): the counted title, the settings
/// they all share, the Move, Rotate and Scale help while a frame shows, then Copy (#937), which a
/// locked Creation hides, and Delete, dimmed while <see cref="CanDelete"/> is false (#896).
/// <see cref="EmptyNote"/> shows when there are neither settings nor a frame; a note with nothing to
/// say is null.
/// </summary>
public sealed record SelectionPanelPresentation(
    UiText Title,
    IReadOnlyList<ParameterSlider> Settings,
    UiText? SettingsNote,
    UiText? EmptyNote,
    bool ShowFrameRows,
    UiText DeleteText,
    UiText? DeleteNote,
    bool CanDelete,
    UiText CopyText,
    bool ShowCopy,
    bool CanCopy)
{
    /// <summary>The <see cref="Settings"/> shown above the Advanced section (#903).</summary>
    public IReadOnlyList<ParameterSlider> BasicSettings => [.. Settings.Where(setting => !setting.Advanced)];

    /// <summary>The <see cref="Settings"/> in the Advanced section, which is left out when there are none.</summary>
    public IReadOnlyList<ParameterSlider> AdvancedSettings => [.. Settings.Where(setting => setting.Advanced)];
}
