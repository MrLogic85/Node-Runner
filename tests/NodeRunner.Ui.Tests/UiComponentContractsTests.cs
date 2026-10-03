using System.Text.RegularExpressions;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Tests;

public sealed partial class UiComponentContractsTests
{
    [Fact]
    public void SegmentedChoices_KeepTextAndIconInOneResource()
    {
        typeof(UiSegmentedSwitch).GetProperty(nameof(UiSegmentedSwitch.Segments))!
            .PropertyType.ShouldBe(typeof(Godot.Collections.Array<UiSegment>));
        typeof(UiSegmentedSwitch).GetProperty("Options").ShouldBeNull();
        typeof(UiSegmentedSwitch).GetProperty("Icons").ShouldBeNull();
        typeof(UiSegmentedSwitch).GetProperty("IconIds").ShouldBeNull();
        typeof(UiSegment).BaseType.ShouldBe(typeof(Godot.Resource));
        typeof(UiSegment).GetProperty(nameof(UiSegment.IconId))!
            .PropertyType.ShouldBe(typeof(UiIconId));
    }

    [Fact]
    public void ButtonAvailability_UsesNativeDisabledWithoutAnInverseProperty()
    {
        typeof(UiButton).GetProperty("Enabled").ShouldBeNull();
        typeof(UiButton).GetProperty(nameof(UiButton.Disabled))!
            .DeclaringType.ShouldBe(typeof(Godot.BaseButton));
        typeof(UiSlider).GetProperty("Enabled").ShouldBeNull();
        typeof(UiSlider).GetProperty(nameof(UiSlider.Disabled)).ShouldNotBeNull();
        typeof(UiCard).GetProperty(nameof(UiCard.Disabled)).ShouldNotBeNull();
    }

    [Fact]
    public void MenuToggleItem_ComposesTheStandardToggleContract()
    {
        typeof(UiMenuToggleItem).BaseType.ShouldBe(typeof(UiMenuItem));
        typeof(UiMenuActionItem).BaseType.ShouldBe(typeof(UiMenuItem));
        typeof(UiMenuItemDivider).BaseType.ShouldBe(typeof(UiMenuItem));
        typeof(UiMenuItem).IsAbstract.ShouldBeTrue();
        typeof(UiMenuItem).GetProperty(nameof(UiMenuItem.Selected)).ShouldNotBeNull();
        typeof(UiMenuItem).GetProperty(nameof(UiMenuItem.Disabled)).ShouldNotBeNull();
        typeof(UiMenuItem).GetProperty(nameof(UiMenuItem.SizeVariant)).ShouldNotBeNull();
        typeof(UiMenuToggleItem).GetProperty(nameof(UiMenuToggleItem.LabelText)).ShouldNotBeNull();
        typeof(UiMenuToggleItem).GetProperty(nameof(UiMenuToggleItem.Subtext)).ShouldNotBeNull();
        typeof(UiMenuToggleItem).GetProperty(nameof(UiMenuToggleItem.On)).ShouldNotBeNull();
        typeof(UiMenuToggleItem).GetProperty(nameof(UiMenuToggleItem.Disabled)).ShouldNotBeNull();
        typeof(UiMenuToggleItem).GetProperty(nameof(UiMenuToggleItem.SizeVariant))!
            .PropertyType.ShouldBe(typeof(UiMenuItem.MenuItemSize));
        typeof(UiMenu).GetProperty(nameof(UiMenu.Compact))!
            .PropertyType.ShouldBe(typeof(bool));
    }

    [Fact]
    public void AllCanonicalComponents_AreMappedToReusableControls()
    {
        var components = UiComponentContracts.AllCanonicalComponents;

        components.ShouldBe(Enum.GetValues<UiComponentContracts.CanonicalComponent>());
        foreach (var component in components)
        {
            UiComponentContracts.ControlTypeFor(component).ShouldNotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void CanonicalComponents_UseComponentLibraryNamesNotReferenceFunctionNames()
    {
        Enum.GetNames<UiComponentContracts.CanonicalComponent>().ShouldBe(
            [
                "Button",
                "IconButton",
                "HoldButton",
                "Slider",
                "Range",
                "Toggle",
                "Checkbox",
                "Segmented",
                "Picker",
                "Menu",
                "Chip",
                "Callout",
                "ProgressBar",
                "TextField",
                "NameField",
                "Note",
                "ValueRow",
                "PowerRow",
                "MeterRow",
                "PartRow",
                "IconTabs",
                "SelectionHandle",
                "InfoRow",
                "Card",
                "ProgressRing",
                "Number",
                "StageCard",
                "CardActions",
            ]);
    }

    [Fact]
    public void ComponentLibraryComponents_MapToReferenceDesignEntryNames()
    {
        var mapped = UiComponentContracts.AllCanonicalComponents
            .Select(UiComponentContracts.ReferenceEntryFor)
            .ToArray();
        var unmapped = UiComponentContracts.ReferenceEntriesWithoutComponent;

        mapped.ShouldBeUnique();
        mapped.Intersect(unmapped).ShouldBeEmpty();
        mapped.Concat(unmapped).Order(StringComparer.Ordinal).ShouldBe(
            ReferenceEntries().Order(StringComparer.Ordinal),
            "Every c_* entry under reference design/ is mapped to a component or listed as having none, and nothing else.");
    }

    private static HashSet<string> ReferenceEntries()
    {
        var reference = Path.Combine(SceneNodes.FindRepositoryRoot(), "reference design");
        return Directory.EnumerateFiles(reference, "*.md", SearchOption.AllDirectories)
            .SelectMany(path => ReferenceEntry().Matches(File.ReadAllText(path)))
            .Select(match => match.Groups["name"].Value)
            .ToHashSet(StringComparer.Ordinal);
    }

    [GeneratedRegex(@"\b(?<name>c_[a-z_]+)\(")]
    private static partial Regex ReferenceEntry();

    [Fact]
    public void SliderAndRange_ShareImplementation()
    {
        UiComponentContracts.ControlTypeFor(UiComponentContracts.CanonicalComponent.Slider)
            .ShouldBe(nameof(UiSlider));
        UiComponentContracts.ControlTypeFor(UiComponentContracts.CanonicalComponent.Range)
            .ShouldBe(nameof(UiSlider));
        UiComponentContracts.SharesImplementation(
                UiComponentContracts.CanonicalComponent.Slider,
                UiComponentContracts.CanonicalComponent.Range)
            .ShouldBeTrue();
    }

    [Fact]
    public void PowerAndValueRows_ShareImplementation()
    {
        UiComponentContracts.ControlTypeFor(UiComponentContracts.CanonicalComponent.PowerRow)
            .ShouldBe(nameof(UiValueRow));
        UiComponentContracts.SharesImplementation(
            UiComponentContracts.CanonicalComponent.PowerRow,
            UiComponentContracts.CanonicalComponent.ValueRow).ShouldBeTrue();
    }

    [Fact]
    public void ProgressBar_IsRepresentedBySliderWithoutThumbs()
    {
        UiComponentContracts.ControlTypeFor(UiComponentContracts.CanonicalComponent.ProgressBar)
            .ShouldBe(nameof(UiSlider));
        UiComponentContracts.ControlTypeFor(UiComponentContracts.CanonicalComponent.MeterRow)
            .ShouldBe(nameof(UiSlider));
        var progress = UiSliderValue.Progress(0.62);

        progress.Kind.ShouldBe(UiSliderValueKind.Progress);
        progress.ThumbCount.ShouldBe(0);
        progress.Low.ShouldBe(0);
        progress.High.ShouldBe(0.62);
        progress.FillStart.ShouldBe(0);
        progress.FillEnd.ShouldBe(0.62);
    }

    [Fact]
    public void SliderValue_FactoriesPreventConflictingProgressAndThumbState()
    {
        var thumb = UiSliderValue.Thumb(0.4);
        thumb.Kind.ShouldBe(UiSliderValueKind.Thumb);
        thumb.ThumbCount.ShouldBe(1);
        thumb.Low.ShouldBe(0);
        thumb.High.ShouldBe(0.4);
        thumb.FillEnd.ShouldBe(0.4);
        thumb.ThumbAt(0).ShouldBe(0.4);

        var range = UiSliderValue.Thumbs(0.8, 0.2);
        range.Kind.ShouldBe(UiSliderValueKind.Range);
        range.ThumbCount.ShouldBe(2);
        range.Low.ShouldBe(0.2);
        range.High.ShouldBe(0.8);
        range.FillStart.ShouldBe(0.2);
        range.FillEnd.ShouldBe(0.8);
    }

    [Theory]
    [InlineData(0, 0, 0, 1)]
    [InlineData(1, 1, 0.9, 0)]
    [InlineData(0.5, 0.5, 0.49, 0)]
    [InlineData(0.5, 0.5, 0.5, 1)]
    [InlineData(0.2, 0.8, 0.4, 0)]
    [InlineData(0.2, 0.8, 0.6, 1)]
    public void SliderValue_SelectThumb_ReopensCollapsedRangesAndChoosesNearest(
        double low,
        double high,
        double position,
        int expected)
    {
        UiSliderValue.Thumbs(low, high).SelectThumb(position).ShouldBe(expected);
    }

    [Fact]
    public void SliderValue_FromComposesPrimitiveSceneValuesWithoutAmbiguousInputs()
    {
        UiSliderValue.From(UiSliderValueKind.Progress, 0.62, 0.8)
            .ShouldBe(UiSliderValue.Progress(0.8));
        UiSliderValue.From(UiSliderValueKind.Thumb, 0.62, 0.8)
            .ShouldBe(UiSliderValue.Thumb(0.8));
        UiSliderValue.From(UiSliderValueKind.Range, 0.62, 0.8)
            .ShouldBe(UiSliderValue.Thumbs(0.62, 0.8));
    }

    [Fact]
    public void StageCard_UsesDedicatedImplementation()
    {
        UiComponentContracts.ControlTypeFor(UiComponentContracts.CanonicalComponent.StageCard)
            .ShouldBe(nameof(UiStageCard));
    }

    [Fact]
    public void Number_UsesDedicatedImplementation()
    {
        UiComponentContracts.ControlTypeFor(UiComponentContracts.CanonicalComponent.Number)
            .ShouldBe(nameof(UiNumber));
    }


    [Fact]
    public void ReusableControlEnums_PinDocumentedDefaultsAndVariants()
    {
        Enum.GetNames<UiButtonKind>()
            .ShouldBe(["Primary", "Secondary", "Tertiary", "Flat"]);
        Enum.GetNames<UiIconSize>()
            .ShouldBe(["Small", "Standard", "Large", "ExtraLarge"]);
        Enum.GetNames<UiButtonContentLayout>()
            .ShouldBe(["Row", "Stacked", "RowCompact"]);
        Enum.GetNames<UiCard.CardVariant>()
            .ShouldBe(["Frame", "Selected", "Locked", "Warning", "Hint", "Raised"]);
        Enum.GetNames<UiCard.CardSize>()
            .ShouldBe(["Default", "Snug", "Tight", "Roomy", "Flush"]);
        Enum.GetNames<UiPartRow.PartRowState>()
            .ShouldBe(["Rest", "Selected", "Locked", "NoneLeft"]);
        Enum.GetNames<UiChip.ChipKind>()
            .ShouldBe(["Neutral", "Warning", "Danger", "Ok"]);
        Enum.GetNames<UiCallout.CalloutKind>()
            .ShouldBe(["Warning", "Danger", "Ok"]);
        Enum.GetNames<UiMenu.MenuWidthMode>()
            .ShouldBe(["Fixed", "WrapContent"]);
        Enum.GetNames<UiMenuActionItem.MenuItemKind>()
            .ShouldBe(["Default", "Danger"]);
        Enum.GetNames<UiMenuItem.MenuItemSize>()
            .ShouldBe(["Standard", "Compact"]);
    }

    [Fact]
    public void Defaults_MatchReferenceTouchAndCompletionContracts()
    {
        UiSize.Control.Touch.ShouldBe(48);
        UiSize.Widget.NumberDiameter.ShouldBe(16);
        UiSize.Stroke.Number.ShouldBe(1.5f);
        UiComponentContracts.HoldCompletionSeconds.ShouldBe(0.8f);
        UiComponentContracts.ButtonProgressOpacity.ShouldBe(0.5f);
        UiGlow.Extent.ShouldBe(10);
        UiGlow.Opacity.ShouldBe(0.12f);
        UiSliderStyle.Default.ShouldBe(new UiSliderStyle(
            UiSize.Widget.SliderThumbDiameter * 0.5f,
            UiSize.Widget.SliderTrackWidth,
            UiSize.Widget.SliderMarkerHeight * 0.5f,
            UiSize.Widget.SliderStepTickHeight * 0.5f,
            UiSize.Widget.SliderDisabledDashLength,
            UiSize.Stroke.Hair,
            UiSize.Widget.SliderSteppedHeight));
        UiSliderStyle.DisabledOpacity.ShouldBe(0.5f);
    }

    [Fact]
    public void MenuWidthResolution_PreservesFixedDefaultAndWrapContracts()
    {
        UiMenu.ResolveWidth(UiMenu.MenuWidthMode.Fixed, 0, UiLayout.MenuWidth)
            .ShouldBe(UiLayout.MenuWidth);
        UiMenu.ResolveWidth(UiMenu.MenuWidthMode.Fixed, 220, UiLayout.MenuWidth)
            .ShouldBe(220);
        UiMenu.ResolveWidth(UiMenu.MenuWidthMode.WrapContent, 220, UiLayout.MenuWidth)
            .ShouldBe(0);
    }

    [Fact]
    public void SliderGeometry_MatchesTheComponentContract()
    {
        UiSize.Widget.SliderThumbDiameter.ShouldBe(18);
        UiSize.Widget.SliderTrackWidth.ShouldBe(4);
        UiSize.Widget.SliderMarkerHeight.ShouldBe(16);
        UiSize.Widget.SliderStepTickHeight.ShouldBe(10);
        UiSize.Widget.SliderDisabledDashLength.ShouldBe(4);
        UiSize.Widget.SliderSteppedHeight.ShouldBe(60);
    }

    [Fact]
    public void SliderMinimumHeight_ComposesOnlyVisibleLabelRows()
    {
        var style = new UiSliderStyle(9, 4, 9, 5, 8, 1, 80);
        const float lineHeight = 13;
        var space3 = UiSize.Space.S3;
        var space2 = UiSize.Space.S2;
        const float stepLineHeight = 13;
        var trackOnly = UiSlider.CalculateMinimumHeight(style, lineHeight, space3, space2, stepLineHeight, false, false, false);
        var withValueLabels = UiSlider.CalculateMinimumHeight(style, lineHeight, space3, space2, stepLineHeight, true, false, false);
        var withStepLabels = UiSlider.CalculateMinimumHeight(style, lineHeight, space3, space2, stepLineHeight, true, true, false);
        var withMarkerBelow = UiSlider.CalculateMinimumHeight(style, lineHeight, space3, space2, stepLineHeight, true, false, true);

        trackOnly.ShouldBe(style.ThumbRadius * 2);
        withValueLabels.ShouldBe(lineHeight + UiSize.Space.S3 + style.ThumbRadius);
        withStepLabels.ShouldBe(lineHeight + UiSize.Space.S3 + UiSize.Space.S2 + stepLineHeight);
        withMarkerBelow.ShouldBe(withStepLabels);
        UiSlider.CalculateMinimumHeight(style, lineHeight, space3, space2, stepLineHeight, false, true, false)
            .ShouldBe(style.ThumbRadius + UiSize.Space.S2 + stepLineHeight);
        UiSlider.CalculateMinimumHeight(style, lineHeight, space3, space2, stepLineHeight, false, false, true)
            .ShouldBe(style.ThumbRadius + UiSize.Space.S2 + stepLineHeight);
    }

    [Theory]
    [InlineData(0, 0.8, 0)]
    [InlineData(0.4, 0.8, 0.5)]
    [InlineData(0.8, 0.8, 1)]
    [InlineData(1.2, 0.8, 1)]
    [InlineData(-0.1, 0.8, 0)]
    [InlineData(double.NaN, 0.8, 0)]
    [InlineData(double.PositiveInfinity, 0.8, 0)]
    [InlineData(0.4, 0, 1)]
    [InlineData(0.4, -0.8, 1)]
    [InlineData(0.4, double.NaN, 1)]
    [InlineData(0.4, double.PositiveInfinity, 1)]
    public void HoldProgress_TracksElapsedFraction(
        double elapsedSeconds,
        double durationSeconds,
        float expected)
    {
        UiComponentContracts.HoldProgress(elapsedSeconds, durationSeconds)
            .ShouldBe(expected);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0.72, 0.72)]
    [InlineData(2, 1)]
    [InlineData(double.NaN, 0)]
    [InlineData(double.PositiveInfinity, 1)]
    public void ClampProgress_ConstrainsToNormalizedRange(double value, double expected)
    {
        UiComponentContracts.ClampProgress(value).ShouldBe(expected);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0.724, 72)]
    [InlineData(0.725, 73)]
    [InlineData(0.994, 99)]
    [InlineData(0.995, 100)]
    [InlineData(2, 100)]
    public void ProgressPercent_RoundsNormalizedProgressToWholePercent(double progress, int expected)
    {
        UiComponentContracts.ProgressPercent(progress).ShouldBe(expected);
    }

    [Theory]
    [InlineData(double.NaN, 10, 20, 10)]
    [InlineData(double.NegativeInfinity, 10, 20, 10)]
    [InlineData(double.PositiveInfinity, 10, 20, 20)]
    [InlineData(12, 20, 10, 12)]
    [InlineData(30, 20, 10, 20)]
    public void ClampValue_NormalizesBoundsAndFiniteSpecialValues(
        double value,
        double minimum,
        double maximum,
        double expected)
    {
        UiComponentContracts.ClampValue(value, minimum, maximum).ShouldBe(expected);
    }

    [Theory]
    [InlineData(double.NaN, 0)]
    [InlineData(double.NegativeInfinity, 0)]
    [InlineData(double.PositiveInfinity, 1)]
    [InlineData(-0.2, 0)]
    [InlineData(0.4, 0.4)]
    [InlineData(1.2, 1)]
    public void ClampSliderPosition_UsesNormalizedFiniteRange(double position, double expected)
    {
        UiComponentContracts.ClampSliderPosition(position).ShouldBe(expected);
    }

    [Theory]
    [InlineData(0.3, 0, 0.3)]
    [InlineData(0.3, 0.25, 0.25)]
    [InlineData(0.4, 0.25, 0.5)]
    [InlineData(0.99, 0.25, 1)]
    [InlineData(1.2, 0.25, 1)]
    [InlineData(-0.1, 0.25, 0)]
    public void SnapSliderPosition_StopsOnWholeSteps(double position, double step, double expected)
    {
        UiComponentContracts.SnapSliderPosition(position, step).ShouldBe(expected, 1e-9);
    }

    [Theory]
    [InlineData(0, "0%")]
    [InlineData(0.724, "72%")]
    [InlineData(0.999, "99%")]
    [InlineData(1, "99%")]
    [InlineData(2, "99%")]
    public void FormatProgressPercent_FormatsOnlyIncompleteProgress(double progress, string expected)
    {
        UiComponentContracts.FormatProgressPercent(progress).ShouldBe(expected);
    }

    [Theory]
    [InlineData(0.994, false)]
    [InlineData(0.995, true)]
    [InlineData(1, true)]
    public void IsProgressComplete_UsesRoundedPercent(double progress, bool expected)
    {
        UiComponentContracts.IsProgressComplete(progress).ShouldBe(expected);
    }

    [Theory]
    [InlineData(double.NaN, "", "0")]
    [InlineData(1.24, " s", "1.2 s")]
    [InlineData(1.26, " m", "1.3 m")]
    public void FormatNumber_UsesInvariantSingleDecimalAndFiniteFallback(double value, string suffix, string expected)
    {
        UiComponentContracts.FormatNumber(value, suffix).ShouldBe(expected);
    }

    [Fact]
    public void SemanticEnums_IncludeDocumentedStates()
    {
        Enum.GetNames<UiTextField.TextInputState>()
            .ShouldBe(["Rest", "Editing", "Error"]);
        Enum.GetNames<UiTextField.TextInputSize>()
            .ShouldBe(["Standard", "Compact"]);
    }

    [Fact]
    public void AllButtonSpecimens_ShareTheOnlyButtonImplementation()
    {
        foreach (var component in new[]
        {
            UiComponentContracts.CanonicalComponent.Button,
            UiComponentContracts.CanonicalComponent.IconButton,
            UiComponentContracts.CanonicalComponent.HoldButton,
        })
        {
            UiComponentContracts.ControlTypeFor(component).ShouldBe(nameof(UiButton));
        }

        typeof(UiButton).IsSealed.ShouldBeTrue();
        typeof(UiButton).Assembly.GetTypes()
            .ShouldNotContain(type => type.IsSubclassOf(typeof(UiButton)));
    }

    [Fact]
    public void ButtonProgressRevealWidth_StaysInsideFrameAndTracksProgress()
    {
        const float frame = 200;
        UiComponentContracts.ButtonProgressRevealWidth(frame, -1).ShouldBe(0);
        UiComponentContracts.ButtonProgressRevealWidth(frame, 0).ShouldBe(0);
        UiComponentContracts.ButtonProgressRevealWidth(frame, 0.25f).ShouldBe(50);
        UiComponentContracts.ButtonProgressRevealWidth(frame, 1).ShouldBe(frame);
        UiComponentContracts.ButtonProgressRevealWidth(frame, 4).ShouldBe(frame);
        UiComponentContracts.ButtonProgressRevealWidth(0, 0.5f).ShouldBe(0);
        UiComponentContracts.ButtonProgressRevealWidth(float.NaN, 0.5f).ShouldBe(0);
        UiComponentContracts.ButtonProgressRevealWidth(frame, float.NaN).ShouldBe(0);
    }

    [Fact]
    public void ButtonProgressRevealWidth_IsMonotonicAndNeverExceedsTheVisibleFrame()
    {
        const float frame = 137.5f;
        var previous = 0f;
        for (var step = 0; step <= 20; step++)
        {
            var width = UiComponentContracts.ButtonProgressRevealWidth(frame, step / 20f);
            width.ShouldBeGreaterThanOrEqualTo(previous);
            width.ShouldBeLessThanOrEqualTo(frame);
            previous = width;
        }

        previous.ShouldBe(frame);
    }

    [Fact]
    public void NormalizeTabIndex_ClampsWithoutLosingPendingSelection()
    {
        UiComponentContracts.NormalizeTabIndex(2, 4).ShouldBe(2);
        UiComponentContracts.NormalizeTabIndex(9, 4).ShouldBe(3);
        UiComponentContracts.NormalizeTabIndex(-3, 4).ShouldBe(0);
        UiComponentContracts.NormalizeTabIndex(2, 0).ShouldBe(2);
        UiComponentContracts.NormalizeTabIndex(-1, 0).ShouldBe(0);
    }

    [Fact]
    public void NormalizeTabIndex_IsIdempotent()
    {
        foreach (var index in new[] { -5, 0, 1, 7 })
        {
            var once = UiComponentContracts.NormalizeTabIndex(index, 3);
            UiComponentContracts.NormalizeTabIndex(once, 3).ShouldBe(once);
        }
    }
}
