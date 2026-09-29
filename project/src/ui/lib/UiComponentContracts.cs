using System.Globalization;

namespace NodeRunner.Ui.Lib;

/// <summary>Pure, testable contracts for the UI component-library inventory.</summary>
public static class UiComponentContracts
{
    public const float HoldCompletionSeconds = 0.8f;
    public const int FullPercent = 100;
    public const float ButtonProgressOpacity = 0.5f;

    /// <summary>
    /// Width of the revealed hold fill inside the visible button frame. The
    /// fill layer itself always spans the whole frame so it keeps the frame's
    /// rounded contour; only this reveal window changes while holding.
    /// </summary>
    public static float ButtonProgressRevealWidth(float frameWidth, float progress)
    {
        if (!float.IsFinite(frameWidth) || frameWidth <= 0)
        {
            return 0;
        }

        if (!float.IsFinite(progress) || progress <= 0)
        {
            return 0;
        }

        return progress >= 1 ? frameWidth : frameWidth * progress;
    }

    public static float HoldProgress(double elapsedSeconds, double durationSeconds)
    {
        if (!double.IsFinite(elapsedSeconds) || elapsedSeconds <= 0)
        {
            return 0;
        }

        if (!double.IsFinite(durationSeconds) || durationSeconds <= 0)
        {
            return 1;
        }

        return (float)Math.Clamp(elapsedSeconds / durationSeconds, 0, 1);
    }

    public enum CanonicalComponent
    {
        Button,
        IconButton,
        HoldButton,
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
    }

    public static IReadOnlyList<CanonicalComponent> AllCanonicalComponents { get; } =
        Enum.GetValues<CanonicalComponent>();

    public static string ControlTypeFor(CanonicalComponent component) =>
        component switch
        {
            CanonicalComponent.Button => nameof(UiButton),
            CanonicalComponent.IconButton => nameof(UiButton),
            CanonicalComponent.HoldButton => nameof(UiButton),
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
            _ => throw new ArgumentOutOfRangeException(nameof(component), component, null),
        };

    /// <summary>The <c>c_*</c> entry in <c>reference design/library.md</c> that a component implements.</summary>
    public static string ReferenceEntryFor(CanonicalComponent component) =>
        component switch
        {
            CanonicalComponent.Button => "c_btn",
            CanonicalComponent.IconButton => "c_ib",
            CanonicalComponent.HoldButton => "c_hold",
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
            _ => throw new ArgumentOutOfRangeException(nameof(component), component, null),
        };

    /// <summary>
    /// Reference entries with no component of their own; <c>docs/UI_DIRECTION.md</c>
    /// ("Reference component mapping") says what builds each one instead.
    /// </summary>
    public static IReadOnlyList<string> ReferenceEntriesWithoutComponent { get; } =
        ["c_panel_head", "c_inspector", "c_rows"];

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

    public static double ClampValue(double value, double minimum, double maximum)
    {
        minimum = FiniteOrZero(minimum);
        maximum = FiniteOrZero(maximum);
        if (minimum > maximum)
        {
            (minimum, maximum) = (maximum, minimum);
        }

        if (double.IsNaN(value))
        {
            return minimum;
        }

        if (double.IsNegativeInfinity(value))
        {
            return minimum;
        }

        if (double.IsPositiveInfinity(value))
        {
            return maximum;
        }

        return Math.Clamp(value, minimum, maximum);
    }

    public static double ClampSliderPosition(double position)
    {
        if (!double.IsFinite(position))
        {
            return position > 0 ? 1 : 0;
        }

        return Math.Clamp(position, 0, 1);
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
