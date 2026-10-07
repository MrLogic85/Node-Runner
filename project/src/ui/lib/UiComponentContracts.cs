using System.Globalization;

namespace NodeRunner.Ui.Lib;

/// <summary>Pure, testable contracts for the UI component-library inventory.</summary>
public static class UiComponentContracts
{
    public const int FullPercent = 100;

    public enum CanonicalComponent
    {
        Button,
        IconButton,
        Slider,
        Range,
        Toggle,
        Checkbox,
        Segmented,
        Picker,
        Menu,
        Chip,
        Callout,
        ProgressBar,
        TextField,
        NameField,
        Note,
        ValueRow,
        PowerRow,
        MeterRow,
        PartRow,
        IconTabs,
        SelectionHandle,
        InfoRow,
        Card,
        ProgressRing,
        Number,
        StageCard,
        CardActions,
        ExpandSection,
        HintCard,
    }

    public static IReadOnlyList<CanonicalComponent> AllCanonicalComponents { get; } =
        Enum.GetValues<CanonicalComponent>();

    public static string ControlTypeFor(CanonicalComponent component) =>
        component switch
        {
            CanonicalComponent.Button => nameof(UiButton),
            CanonicalComponent.IconButton => nameof(UiButton),
            CanonicalComponent.Slider => nameof(UiSlider),
            CanonicalComponent.Range => nameof(UiSlider),
            CanonicalComponent.Toggle => nameof(UiToggleRow),
            CanonicalComponent.Checkbox => nameof(UiCheckRow),
            CanonicalComponent.Segmented => nameof(UiSegmentedSwitch),
            CanonicalComponent.Picker => nameof(UiPicker),
            CanonicalComponent.Menu => nameof(UiMenu),
            CanonicalComponent.Chip => nameof(UiChip),
            CanonicalComponent.Callout => nameof(UiCallout),
            CanonicalComponent.ProgressBar => nameof(UiSlider),
            CanonicalComponent.TextField => nameof(UiTextField),
            CanonicalComponent.NameField => nameof(UiNameField),
            CanonicalComponent.Note => nameof(UiNoteRow),
            CanonicalComponent.ValueRow => nameof(UiValueRow),
            CanonicalComponent.PowerRow => nameof(UiValueRow),
            CanonicalComponent.MeterRow => nameof(UiSlider),
            CanonicalComponent.PartRow => nameof(UiPartRow),
            CanonicalComponent.IconTabs => nameof(UiIconTabs),
            CanonicalComponent.SelectionHandle => nameof(UiSelectionHandle),
            CanonicalComponent.InfoRow => nameof(UiInfoRow),
            CanonicalComponent.Card => nameof(UiCard),
            CanonicalComponent.ProgressRing => nameof(UiProgressRing),
            CanonicalComponent.Number => nameof(UiNumber),
            CanonicalComponent.StageCard => nameof(UiStageCard),
            CanonicalComponent.CardActions => nameof(UiCardActions),
            CanonicalComponent.ExpandSection => nameof(UiExpandSection),
            CanonicalComponent.HintCard => nameof(UiHintCard),
            _ => throw new ArgumentOutOfRangeException(nameof(component), component, null),
        };

    /// <summary>The <c>c_*</c> entry in <c>reference design/library.md</c> that a component implements.</summary>
    public static string ReferenceEntryFor(CanonicalComponent component) =>
        component switch
        {
            CanonicalComponent.Button => "c_btn",
            CanonicalComponent.IconButton => "c_ib",
            CanonicalComponent.Slider => "c_slider",
            CanonicalComponent.Range => "c_range",
            CanonicalComponent.Toggle => "c_toggle",
            CanonicalComponent.Checkbox => "c_check",
            CanonicalComponent.Segmented => "c_seg",
            CanonicalComponent.Picker => "c_pick",
            CanonicalComponent.Menu => "c_menu",
            CanonicalComponent.Chip => "c_chip",
            CanonicalComponent.Callout => "c_call",
            CanonicalComponent.ProgressBar => "c_prog",
            CanonicalComponent.TextField => "c_textfield",
            CanonicalComponent.NameField => "c_name",
            CanonicalComponent.Note => "c_note",
            CanonicalComponent.ValueRow => "c_value",
            CanonicalComponent.PowerRow => "c_power",
            CanonicalComponent.MeterRow => "c_meter",
            CanonicalComponent.PartRow => "c_row",
            CanonicalComponent.IconTabs => "c_tabs",
            CanonicalComponent.SelectionHandle => "c_round_button",
            CanonicalComponent.InfoRow => "c_info_row",
            CanonicalComponent.Card => "c_card",
            CanonicalComponent.ProgressRing => "c_ring",
            CanonicalComponent.Number => "c_num",
            CanonicalComponent.StageCard => "c_stage",
            CanonicalComponent.CardActions => "c_card_actions",
            CanonicalComponent.ExpandSection => "c_expand",
            CanonicalComponent.HintCard => "c_hint",
            _ => throw new ArgumentOutOfRangeException(nameof(component), component, null),
        };

    /// <summary>
    /// Reference entries with no component of their own; <c>docs/UI_DIRECTION.md</c>
    /// ("Reference component mapping") says what builds each one instead.
    /// </summary>
    public static IReadOnlyList<string> ReferenceEntriesWithoutComponent { get; } =
        ["c_panel_head", "c_inspector", "c_rows", "c_hold", "c_status"];

    public static bool SharesImplementation(CanonicalComponent component, CanonicalComponent other) =>
        ControlTypeFor(component) == ControlTypeFor(other);

    /// <summary>Clamps a tab index into range; keeps a pending index when the tab count is unknown.</summary>
    public static int NormalizeTabIndex(int index, int tabCount)
    {
        if (tabCount <= 0)
        {
            return Math.Max(index, 0);
        }

        return Math.Clamp(index, 0, tabCount - 1);
    }

    /// <summary>
    /// The LineEdit limit for a text field with a cap (#868): none without a cap, and never below the
    /// text already there, so a longer saved name is not cut and the limit closes in as it shrinks.
    /// </summary>
    public static int EditorMaxLength(int limit, int currentLength) =>
        limit <= 0 ? 0 : Math.Max(limit, currentLength);

    public static double ClampSliderPosition(double position)
    {
        if (!double.IsFinite(position))
        {
            return position > 0 ? 1 : 0;
        }

        return Math.Clamp(position, 0, 1);
    }

    /// <summary>
    /// <paramref name="position"/> on the nearest whole <paramref name="step"/>, like Godot's
    /// <c>Range.step</c>; a step of 0 leaves it where it is (#711).
    /// </summary>
    public static double SnapSliderPosition(double position, double step)
    {
        position = ClampSliderPosition(position);
        return step > 0 ? ClampSliderPosition(Math.Round(position / step) * step) : position;
    }

    public static double ClampProgress(double progress) => ClampSliderPosition(progress);

    public static int ProgressPercent(double progress) =>
        Math.Clamp((int)Math.Round(ClampProgress(progress) * FullPercent, MidpointRounding.AwayFromZero), 0, FullPercent);

    public static bool IsProgressComplete(double progress) =>
        ProgressPercent(progress) >= FullPercent;

    public static string FormatProgressPercent(double progress)
    {
        var roundedPercent = ProgressPercent(progress);
        if (roundedPercent >= FullPercent)
        {
            return "99%";
        }

        return roundedPercent.ToString("0", CultureInfo.InvariantCulture) + "%";
    }

    public static string FormatNumber(double value, string suffix = "") =>
        FiniteOrZero(value).ToString("0.#", CultureInfo.InvariantCulture) + suffix;

    private static double FiniteOrZero(double value) => double.IsFinite(value) ? value : 0;
}
