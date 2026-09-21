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
        CStep,
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

    public enum StepperSymbol
    {
        Minus,
        Plus,
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
            CanonicalComponent.CStep => nameof(UiStepperButton),
            CanonicalComponent.CSlider => nameof(UiTokenSlider),
            CanonicalComponent.CRange => nameof(UiRangeSlider),
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

    public static (double Low, double High) ClampRange(double low, double high, double minimum, double maximum, double? mark = null)
    {
        if (minimum > maximum)
        {
            (minimum, maximum) = (maximum, minimum);
        }

        var clampedLow = ClampValue(low, minimum, maximum);
        var clampedHigh = ClampValue(high, minimum, maximum);
        if (clampedLow > clampedHigh)
        {
            (clampedLow, clampedHigh) = (clampedHigh, clampedLow);
        }

        if (mark is { } finiteMark && double.IsFinite(finiteMark))
        {
            var clampedMark = ClampValue(finiteMark, minimum, maximum);
            clampedLow = Math.Min(clampedLow, clampedMark);
            clampedHigh = Math.Max(clampedHigh, clampedMark);
        }

        return (clampedLow, clampedHigh);
    }

    public static string FormatPercent(double value) =>
        ClampPercent(value).ToString("0", CultureInfo.InvariantCulture) + "%";

    public static string FormatNumber(double value, string suffix = "") =>
        FiniteOrZero(value).ToString("0.#", CultureInfo.InvariantCulture) + suffix;

    private static double FiniteOrZero(double value) => double.IsFinite(value) ? value : 0;
}
