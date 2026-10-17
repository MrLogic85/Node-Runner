using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

/// <summary>
/// The Part settings panel for one selected part: its Name first, then what it is joined to,
/// a short note, and Delete, dimmed while <see cref="CanDelete"/> is false. Structure is read-only here; it is drawn
/// and changed on the canvas. <see cref="Settings"/> are the part's panel sliders; a joint, Piston or
/// Spring lists no connections, as they are drawn on the canvas, so its
/// <see cref="ConnectionsLabel"/> and <see cref="ConnectionsValue"/> are null. <see cref="Name"/> is
/// the player's own name as written, or <see cref="DefaultName"/>. The panel's title is
/// <see cref="Title"/>, what kind of part it is, so a renamed part still says what it is.
/// <see cref="Readouts"/> are read-only values the part's settings derive, shown after its basic
/// settings, like a Wheel's Weight. <see cref="OnThisJoint"/> are the "On this joint" tabs, top → down, when the
/// part's joint holds two or more parts; null otherwise.
/// </summary>
public sealed record PartSettingsPresentation(
    int Id,
    PartKind Kind,
    UiText Name,
    UiText DefaultName,
    UiText? ConnectionsLabel,
    UiText? ConnectionsValue,
    UiText Note,
    bool CanDelete,
    IReadOnlyList<ParameterSlider> Settings,
    IReadOnlyList<PartPickerPresentation>? Pickers = null,
    IReadOnlyList<PartReadout>? Readouts = null,
    IReadOnlyList<JointPartTab>? OnThisJoint = null)
{
    /// <summary>The index of this part's tab in <see cref="OnThisJoint"/>, or null without tabs.</summary>
    public int? OnThisJointIndex => OnThisJoint?.Select((tab, index) => (tab, index)).First(entry => entry.tab.Part.Id == Id).index;

    /// <summary>The <see cref="Settings"/> shown above the Advanced section.</summary>
    public IReadOnlyList<ParameterSlider> BasicSettings => [.. Settings.Where(setting => !setting.Advanced)];

    /// <summary>The <see cref="Settings"/> in the Advanced section, which is left out when there are none.</summary>
    public IReadOnlyList<ParameterSlider> AdvancedSettings => [.. Settings.Where(setting => setting.Advanced)];

    public UiText Title => PartKindDisplays.Of(Kind).Title;
}

/// <summary>One "On this joint" tab: the part it selects and the kind whose glyph it shows.</summary>
public sealed record JointPartTab(PartKind Kind, CreatureElementSelection Part);

/// <summary>A read-only row in the Part settings panel: a label and the value it reads, like "Weight" and "1.2 kg".</summary>
public sealed record PartReadout(UiText Label, UiText Value);

/// <summary>The role a link has on its hinge part today, a Servo's, a Velocity motor's, a Brake's or an Angle sensor's.</summary>
public enum ServoLinkRole
{
    None,
    Fixed,
    Target,
}

/// <summary>
/// One link a motor picker offers, or its joint's Wheel, a one-sided link: its <see cref="Role"/> on the Servo today, and the <see cref="Note"/>
/// "swaps" when picking it swaps the Fixed and Target links.
/// </summary>
public sealed record PartPickerOption(int LinkId, UiText Name, CreatureElementKind Kind, ServoLinkRole Role, UiText? Note = null);

/// <summary>
/// A motor's link picker: the links it offers, which one is chosen, and the <see cref="Placeholder"/>
/// shown instead when none is (a deleted held link), which is never one of the options.
/// </summary>
public sealed record PartPickerPresentation(
    UiText Label,
    IReadOnlyList<PartPickerOption> Options,
    int? SelectedIndex,
    UiText? Note,
    UiText? Placeholder = null)
{
    public int? LinkIdAt(int selectedIndex) =>
        selectedIndex >= 0 && selectedIndex < Options.Count ? Options[selectedIndex].LinkId : null;
}
