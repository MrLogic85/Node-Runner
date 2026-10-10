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
    Wheel,
}

/// <summary>
/// The Part settings panel for one selected part (#343): its Name first, then what it is joined to,
/// a short note, and Delete, dimmed while <see cref="CanDelete"/> is false (#896). Structure is read-only here; it is drawn
/// and changed on the canvas. <see cref="Settings"/> are the part's panel sliders (#704), like a
/// Piston's (#451); a joint (#913), Piston or Spring lists no connections, as they are drawn on the canvas, so its
/// <see cref="ConnectionsLabel"/> and <see cref="ConnectionsValue"/> are null. <see cref="Name"/> is
/// the player's own name as written, or <see cref="DefaultName"/>. The panel's title is
/// <see cref="Title"/>, what kind of part it is, so a renamed part still says what it is.
/// <see cref="PanelId"/> says which part the panel shows across edits. <see cref="Readouts"/> are
/// read-only values the part's settings derive, shown after its basic settings, like a Wheel's
/// Weight (#129). <see cref="OnThisJoint"/> are the "On this joint" tabs, outside → in, when the
/// part's joint holds two or more parts (#1044); null otherwise.
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
    IReadOnlyList<PartPickerPresentation>? Pickers = null,
    int? JointId = null,
    IReadOnlyList<PartReadout>? Readouts = null,
    IReadOnlyList<JointPartTab>? OnThisJoint = null)
{
    /// <summary>The index of this part's tab in <see cref="OnThisJoint"/>, or null without tabs.</summary>
    public int? OnThisJointIndex => OnThisJoint?.Select((tab, index) => (tab, index)).First(entry => entry.tab.Part.Id == Id).index;

    /// <summary>
    /// The part the panel shows, kept across edits: <see cref="Id"/>, or a Servo's <see cref="JointId"/>,
    /// since a link change gives the Servo a new id (<c>docs/CREATURE_MODEL.md</c> → "Editing identity
    /// rules") but it is the same Servo to the player (#910, #911).
    /// </summary>
    public int PanelId => JointId ?? Id;

    /// <summary>The <see cref="Settings"/> shown above the Advanced section (#903).</summary>
    public IReadOnlyList<ParameterSlider> BasicSettings => [.. Settings.Where(setting => !setting.Advanced)];

    /// <summary>The <see cref="Settings"/> in the Advanced section, which is left out when there are none.</summary>
    public IReadOnlyList<ParameterSlider> AdvancedSettings => [.. Settings.Where(setting => setting.Advanced)];

    public UiText Title => Kind switch
    {
        PartSettingsKind.Node => UiText.Plain("Joint"),
        PartSettingsKind.Beam => UiText.Plain("Beam"),
        PartSettingsKind.Accelerometer => UiText.Plain("Accelerometer"),
        PartSettingsKind.Camera => UiText.Plain("Camera"),
        PartSettingsKind.Servo => UiText.Plain("Servo"),
        PartSettingsKind.Piston => UiText.Plain("Piston"),
        PartSettingsKind.Spring => UiText.Plain("Spring"),
        PartSettingsKind.Wheel => UiText.Plain("Wheel"),
        _ => throw new ArgumentOutOfRangeException(nameof(Kind), Kind, "Unknown part kind."),
    };
}

/// <summary>One "On this joint" tab (#1044): the part it selects and the kind whose glyph it shows.</summary>
public sealed record JointPartTab(PartSettingsKind Kind, CreatureElementSelection Part);

/// <summary>A read-only row in the Part settings panel: a label and the value it reads, like "Weight" and "1.2 kg".</summary>
public sealed record PartReadout(UiText Label, UiText Value);

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
