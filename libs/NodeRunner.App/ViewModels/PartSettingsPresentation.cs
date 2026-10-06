using NodeRunner.Domain;

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
/// Piston's (#451); a joint (#913), Piston or Spring lists no connections, as they are drawn on the canvas, so its
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

/// <summary>
/// A Servo link picker: the links it offers, which one is chosen, and the <see cref="Placeholder"/>
/// shown instead when none is (a deleted held link), which is never one of the options.
/// </summary>
public sealed record PartPickerPresentation(
    UiText Label,
    IReadOnlyList<int> LinkIds,
    IReadOnlyList<UiText> Options,
    int? SelectedIndex,
    bool IsLocked,
    UiText? Note,
    IReadOnlyList<CreatureElementKind>? LinkKinds = null,
    UiText? Placeholder = null)
{
    public int? LinkIdAt(int selectedIndex) =>
        selectedIndex >= 0 && selectedIndex < LinkIds.Count ? LinkIds[selectedIndex] : null;

    public CreatureElementKind? LinkKindAt(int selectedIndex) =>
        selectedIndex >= 0 && LinkKinds is not null && selectedIndex < LinkKinds.Count ? LinkKinds[selectedIndex] : null;
}
