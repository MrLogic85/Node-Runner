namespace NodeRunner.App.ViewModels;

/// <summary>Which kind of part the Part settings panel shows; the screen picks its glyph from this.</summary>
public enum PartSettingsKind
{
    Node,
    Beam,
    Accelerometer,
    Camera,
    Servo,
    Piston,
    Spring,
}

/// <summary>
/// The Part settings panel for one selected part (#343): its Name first, then what it is joined to,
/// a short note, and Delete unless the Creation is locked. Structure is read-only here; it is drawn
/// and changed on the canvas. <see cref="Settings"/> are the part's panel sliders (#704), like a
/// Piston's (#451); a Piston or Spring lists no connections, as its ends are drawn on the canvas, so its
/// <see cref="ConnectionsLabel"/> and <see cref="ConnectionsValue"/> are null. <see cref="Name"/> is
/// the player's own name as written, or <see cref="DefaultName"/>.
/// </summary>
public sealed record PartSettingsPresentation(
    int Id,
    PartSettingsKind Kind,
    UiText Name,
    UiText DefaultName,
    UiText? ConnectionsLabel,
    UiText? ConnectionsValue,
    UiText Note,
    bool CanDelete,
    IReadOnlyList<ParameterSlider> Settings,
    IReadOnlyList<PartPickerPresentation>? Pickers = null);

public sealed record PartPickerPresentation(
    UiText Label,
    IReadOnlyList<int> LinkIds,
    IReadOnlyList<UiText> Options,
    int SelectedIndex,
    bool IsLocked,
    UiText? Note);
