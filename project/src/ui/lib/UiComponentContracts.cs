using System.Globalization;

namespace NodeRunner.Ui.Lib;

/// <summary>Pure, testable contracts for the reference component-library inventory.</summary>
public static class UiComponentContracts
{
    public const float IconButtonVisibleSize = 40;
    public const float ProgressRingDiameter = 44;
    public const float HoldCompletionSeconds = 0.8f;
    public const float ButtonProgressOpacity = 0.35f;
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
        CBtn,
        CIb,
        CHold,
        CSlider,
        CRange,
        CToggle,
        CCheck,
        CSeg,
        CPick,
        CMenu,
        CChip,
        CProg,
        CTextfield,
        CName,
        CValue,
        CReadonly,
        CPower,
        CMeter,
        CRow,
        CTabs,
        CPanelHead,
        CInfoRow,
        CCard,
        CPanel,
        CRing,
    }

    public enum HoldState
    {
        Rest,
        Holding,
        Cancelled,
        Completed,
        Disabled,
    }

    public enum ValidationState
    {
        Rest,
        Editing,
        Invalid,
    }

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
            CanonicalComponent.CBtn => nameof(UiActionButton),
            CanonicalComponent.CIb => nameof(UiIconButton),
            CanonicalComponent.CHold => nameof(UiHoldButton),
            CanonicalComponent.CSlider => nameof(UiSlider),
            CanonicalComponent.CRange => nameof(UiSlider),
            CanonicalComponent.CToggle => nameof(UiToggleRow),
            CanonicalComponent.CCheck => nameof(UiCheckRow),
            CanonicalComponent.CSeg => nameof(UiSegmentedSwitch),
            CanonicalComponent.CPick => nameof(UiPicker),
            CanonicalComponent.CMenu => nameof(UiOverflowMenu),
            CanonicalComponent.CChip => nameof(UiChip),
            CanonicalComponent.CProg => nameof(UiProgressBar),
            CanonicalComponent.CTextfield => nameof(UiTextField),
            CanonicalComponent.CName => nameof(UiNameField),
            CanonicalComponent.CValue => nameof(UiValueRow),
            CanonicalComponent.CReadonly => nameof(UiReadonlyValue),
            CanonicalComponent.CPower => nameof(UiPowerRow),
            CanonicalComponent.CMeter => nameof(UiMeterRow),
            CanonicalComponent.CRow => nameof(UiPartRow),
            CanonicalComponent.CTabs => nameof(UiIconTabs),
            CanonicalComponent.CPanelHead => nameof(UiPanelHeader),
            CanonicalComponent.CInfoRow => nameof(UiInfoRow),
            CanonicalComponent.CCard => nameof(UiPanel),
            CanonicalComponent.CPanel => nameof(UiPanel),
            CanonicalComponent.CRing => nameof(UiProgressRing),
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

    public static double[] NormalizeSliderThumbs(IEnumerable<double>? thumbs)
    {
        var normalized = thumbs?
            .Take(2)
            .Select(ClampSliderPosition)
            .Order()
            .ToArray() ?? [];
        return normalized.Length == 0 ? [0] : normalized;
    }

    public static int SelectSliderThumb(IEnumerable<double>? thumbs, double position)
    {
        var normalized = NormalizeSliderThumbs(thumbs);
        if (normalized.Length == 1)
        {
            return 0;
        }

        var target = ClampSliderPosition(position);
        if (normalized[0] == normalized[1])
        {
            return target < normalized[0] ? 0 : 1;
        }

        var lowDistance = Math.Abs(target - normalized[0]);
        var highDistance = Math.Abs(target - normalized[1]);
        return lowDistance <= highDistance ? 0 : 1;
    }

    public static string FormatPercent(double value) =>
        ClampPercent(value).ToString("0", CultureInfo.InvariantCulture) + "%";

    public static string FormatNumber(double value, string suffix = "") =>
        FiniteOrZero(value).ToString("0.#", CultureInfo.InvariantCulture) + suffix;

    private static double FiniteOrZero(double value) => double.IsFinite(value) ? value : 0;
}
