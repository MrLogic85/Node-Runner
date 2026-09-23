using System.Globalization;

namespace NodeRunner.Ui.Lib;

/// <summary>Pure, testable contracts for the UI component-library inventory.</summary>
public static class UiComponentContracts
{
    public const float IconButtonVisibleSize = 40;
    public const float ProgressRingDiameter = 44;
    public const float HoldCompletionSeconds = 0.8f;
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
        OverflowMenu,
        Chip,
        ProgressBar,
        TextField,
        NameField,
        ValueRow,
        ReadonlyValue,
        PowerRow,
        MeterRow,
        PartRow,
        IconTabs,
        SelectionHandle,
        PanelHeader,
        InfoRow,
        Card,
        Panel,
        ProgressRing,
    }

    public enum HoldState
    {
        Rest,
        Holding,
        Cancelled,
        Completed,
        Disabled,
    }

    /// <summary>Completed destructive holds remain one-shot until their owner explicitly resets them.</summary>
    public static bool CanBeginHold(HoldState state) =>
        state is HoldState.Rest or HoldState.Cancelled;

    public enum SemanticState
    {
        Neutral,
        Selected,
        Locked,
        Warning,
        Danger,
        Hint,
        Ok,
        Bad,
        Disabled,
    }

    public static IReadOnlyList<CanonicalComponent> AllCanonicalComponents { get; } =
        Enum.GetValues<CanonicalComponent>();

    public static string ControlTypeFor(CanonicalComponent component) =>
        component switch
        {
            CanonicalComponent.Button => nameof(UiActionButton),
            CanonicalComponent.IconButton => nameof(UiIconButton),
            CanonicalComponent.HoldButton => nameof(UiHoldButton),
            CanonicalComponent.Slider => nameof(UiSlider),
            CanonicalComponent.Range => nameof(UiSlider),
            CanonicalComponent.Toggle => nameof(UiToggleRow),
            CanonicalComponent.Checkbox => nameof(UiCheckRow),
            CanonicalComponent.Segmented => nameof(UiSegmentedSwitch),
            CanonicalComponent.Picker => nameof(UiPicker),
            CanonicalComponent.OverflowMenu => nameof(UiOverflowMenu),
            CanonicalComponent.Chip => nameof(UiChip),
            CanonicalComponent.ProgressBar => nameof(UiSlider),
            CanonicalComponent.TextField => nameof(UiTextField),
            CanonicalComponent.NameField => nameof(UiNameField),
            CanonicalComponent.ValueRow => nameof(UiValueRow),
            CanonicalComponent.ReadonlyValue => nameof(UiReadonlyValue),
            CanonicalComponent.PowerRow => nameof(UiPowerRow),
            CanonicalComponent.MeterRow => nameof(UiMeterRow),
            CanonicalComponent.PartRow => nameof(UiPartRow),
            CanonicalComponent.IconTabs => nameof(UiIconTabs),
            CanonicalComponent.SelectionHandle => nameof(UiSelectionHandle),
            CanonicalComponent.PanelHeader => nameof(UiPanelHeader),
            CanonicalComponent.InfoRow => nameof(UiInfoRow),
            CanonicalComponent.Card => nameof(UiCard),
            CanonicalComponent.Panel => nameof(UiCard),
            CanonicalComponent.ProgressRing => nameof(UiProgressRing),
            _ => throw new ArgumentOutOfRangeException(nameof(component), component, null),
        };

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

    public static double ClampPercent(double value)
    {
        if (double.IsNaN(value) || double.IsNegativeInfinity(value))
        {
            return 0;
        }

        if (double.IsPositiveInfinity(value))
        {
            return 100;
        }

        return Math.Clamp(value, 0, 100);
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
        Math.Clamp((int)Math.Round(ClampProgress(progress) * 100, MidpointRounding.AwayFromZero), 0, 100);

    public static bool IsProgressComplete(double progress) =>
        ProgressPercent(progress) >= 100;

    public static string FormatProgressPercent(double progress)
    {
        var roundedPercent = ProgressPercent(progress);
        if (roundedPercent >= 100)
        {
            return "99%";
        }

        return roundedPercent.ToString("0", CultureInfo.InvariantCulture) + "%";
    }

    public static string FormatPercent(double value) =>
        ClampPercent(value).ToString("0", CultureInfo.InvariantCulture) + "%";

    public static string FormatNumber(double value, string suffix = "") =>
        FiniteOrZero(value).ToString("0.#", CultureInfo.InvariantCulture) + suffix;

    private static double FiniteOrZero(double value) => double.IsFinite(value) ? value : 0;
}
