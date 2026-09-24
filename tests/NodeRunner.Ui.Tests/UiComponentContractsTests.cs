using NodeRunner.Ui.Lib;
using NodeRunner.Ui.Screens;

namespace NodeRunner.Ui.Tests;

public sealed class UiComponentContractsTests
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
                "OverflowMenu",
                "Chip",
                "ProgressBar",
                "TextField",
                "NameField",
                "Note",
                "ValueRow",
                "ReadonlyValue",
                "PowerRow",
                "MeterRow",
                "PartRow",
                "IconTabs",
                "SelectionHandle",
                "PanelHeader",
                "InfoRow",
                "Card",
                "Panel",
                "ProgressRing",
                "Number",
                "StageCard",
            ]);
    }

    [Fact]
    public void ComponentGalleryInventory_CoversVisibleCanonicalComponentsExactlyOnce()
    {
        var inventory = ComponentGalleryScreen.CanonicalInventory;

        inventory.Select(spec => spec.Component)
            .ShouldAllBe(component => UiComponentContracts.AllCanonicalComponents.Contains(component));
        inventory.Select(spec => spec.Component).Distinct().Count()
            .ShouldBe(inventory.Count);
        foreach (var component in inventory.Select(spec => spec.Component))
        {
            inventory.Count(spec => spec.Component == component).ShouldBe(1);
        }
    }

    [Fact]
    public void ComponentGalleryInventory_UsesVisibleSectionLabelsWithoutReferenceFunctionNames()
    {
        foreach (var spec in ComponentGalleryScreen.CanonicalInventory)
        {
            spec.Section.ShouldNotContain("effects-lite");
            spec.Section.ShouldNotContain("c_");
            spec.Section.ShouldNotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void ComponentLibraryComponents_MapToReferenceDesignEntryNames()
    {
        var referenceNames = UiComponentContracts.AllCanonicalComponents
            .Select(ReferenceEntryName)
            .ToArray();

        referenceNames.Distinct().Count().ShouldBe(UiComponentContracts.AllCanonicalComponents.Count);
        referenceNames.ShouldAllBe(name => name.StartsWith("c_", StringComparison.Ordinal));
    }

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
            .ShouldBe(nameof(UiMeterRow));
        var progress = UiSliderValue.Progress(0.62);

        progress.Kind.ShouldBe(UiSliderValueKind.Progress);
        progress.ThumbCount.ShouldBe(0);
        progress.FillStart.ShouldBe(0);
        progress.FillEnd.ShouldBe(0.62);
    }

    [Fact]
    public void SliderValue_FactoriesPreventConflictingProgressAndThumbState()
    {
        var thumb = UiSliderValue.Thumb(0.4);
        thumb.Kind.ShouldBe(UiSliderValueKind.Thumb);
        thumb.ThumbCount.ShouldBe(1);
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
            .ShouldBe(UiSliderValue.Progress(0.62));
        UiSliderValue.From(UiSliderValueKind.Thumb, 0.62, 0.8)
            .ShouldBe(UiSliderValue.Thumb(0.62));
        UiSliderValue.From(UiSliderValueKind.Range, 0.62, 0.8)
            .ShouldBe(UiSliderValue.Thumbs(0.62, 0.8));
    }

    [Fact]
    public void PanelAndStageCard_UseDedicatedImplementations()
    {
        UiComponentContracts.ControlTypeFor(UiComponentContracts.CanonicalComponent.Panel)
            .ShouldBe(nameof(UiInspectorPanel));
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
            .ShouldBe(["Neutral", "Accent", "Locked", "Danger", "Warning", "Bad", "Ok"]);
        Enum.GetNames<UiOverflowMenu.MenuWidthMode>()
            .ShouldBe(["Fixed", "WrapContent"]);
    }

    [Fact]
    public void Defaults_MatchReferenceTouchAndCompletionContracts()
    {
        UiComponentContracts.PartRowVisibleHeight.ShouldBe(UiTokens.Neon.ControlHeight);
        UiComponentContracts.PartRowTouchHeight.ShouldBe(UiTokens.Neon.TouchTarget);
        UiTokens.Neon.TouchTarget.ShouldBe(48);
        UiTokens.Neon.NumberDiameter.ShouldBe(16);
        UiTokens.Neon.NumberStrokeWidth.ShouldBe(1.5f);
        UiComponentContracts.ProgressRingDiameter.ShouldBe(44);
        UiComponentContracts.HoldCompletionSeconds.ShouldBe(0.8f);
        UiComponentContracts.ButtonProgressOpacity.ShouldBe(0.5f);
        UiGlow.Extent.ShouldBe(10);
        UiGlow.Opacity.ShouldBe(0.12f);
        var sliderStyle = UiSliderStyle.From(UiTokens.Neon);
        sliderStyle.ThumbRadius.ShouldBe(9);
        sliderStyle.TrackWidth.ShouldBe(4);
        UiSliderStyle.DisabledOpacity.ShouldBe(0.5f);
    }

    [Fact]
    public void OverflowMenuWidthResolution_PreservesFixedDefaultAndWrapContracts()
    {
        var tokens = UiTokens.Neon;

        UiOverflowMenu.ResolveContainerWidth(UiOverflowMenu.MenuWidthMode.Fixed, 0, tokens)
            .ShouldBe(tokens.MenuWidth);
        UiOverflowMenu.ResolveRowWidth(UiOverflowMenu.MenuWidthMode.Fixed, 0, tokens)
            .ShouldBe(tokens.MenuWidth - (tokens.StrokeHair * 2));
        UiOverflowMenu.ResolveContainerWidth(UiOverflowMenu.MenuWidthMode.Fixed, 220, tokens)
            .ShouldBe(220);
        UiOverflowMenu.ResolveRowWidth(UiOverflowMenu.MenuWidthMode.Fixed, 220, tokens)
            .ShouldBe(218);
        UiOverflowMenu.ResolveContainerWidth(UiOverflowMenu.MenuWidthMode.WrapContent, 220, tokens)
            .ShouldBe(0);
        UiOverflowMenu.ResolveRowWidth(UiOverflowMenu.MenuWidthMode.WrapContent, 220, tokens)
            .ShouldBe(0);
    }

    [Fact]
    public void SliderStyle_ResolvesScalableGeometryFromTokens()
    {
        var tokens = new UiTokens
        {
            SliderThumbDiameter = 36,
            SliderTrackWidth = 8,
            SliderMarkerHeight = 32,
            SliderStepTickHeight = 20,
            SliderDisabledDashLength = 8,
            SliderSteppedHeight = 120,
            StrokeHair = 2,
        };

        UiSliderStyle.From(tokens).ShouldBe(new UiSliderStyle(
            ThumbRadius: 18,
            TrackWidth: 8,
            MarkerHalfHeight: 16,
            StepTickHalfHeight: 10,
            DisabledDashLength: 8,
            DisabledThumbInset: 2,
            SteppedHeight: 120));
    }

    [Fact]
    public void SliderMinimumHeight_ComposesOnlyVisibleLabelRows()
    {
        var tokens = UiTokens.Neon;
        var style = UiSliderStyle.From(tokens);
        var trackOnly = UiSlider.CalculateMinimumHeight(style, tokens, hasValueLabelRow: false, hasStepLabelRow: false, hasMarkerBelowRow: false);
        var withValueLabels = UiSlider.CalculateMinimumHeight(style, tokens, hasValueLabelRow: true, hasStepLabelRow: false, hasMarkerBelowRow: false);
        var withStepLabels = UiSlider.CalculateMinimumHeight(style, tokens, hasValueLabelRow: true, hasStepLabelRow: true, hasMarkerBelowRow: false);
        var withMarkerBelow = UiSlider.CalculateMinimumHeight(style, tokens, hasValueLabelRow: true, hasStepLabelRow: false, hasMarkerBelowRow: true);

        trackOnly.ShouldBe(style.ThumbRadius * 2);
        withValueLabels.ShouldBe(tokens.OverlineText.LineHeight + tokens.Space3 + style.ThumbRadius);
        withStepLabels.ShouldBe(tokens.OverlineText.LineHeight + tokens.Space3 + tokens.Space2 + tokens.ReadoutSmallText.LineHeight);
        withMarkerBelow.ShouldBe(withStepLabels);
        UiSlider.CalculateMinimumHeight(style, tokens, false, true, false)
            .ShouldBe(style.ThumbRadius + tokens.Space2 + tokens.ReadoutSmallText.LineHeight);
        UiSlider.CalculateMinimumHeight(style, tokens, false, false, true)
            .ShouldBe(style.ThumbRadius + tokens.Space2 + tokens.ReadoutSmallText.LineHeight);
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
    [InlineData(42, 42)]
    [InlineData(101, 100)]
    public void ClampPercent_ConstrainsToCanonicalPercentRange(double value, double expected)
    {
        UiComponentContracts.ClampPercent(value).ShouldBe(expected);
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
    [InlineData(-2, "0%")]
    [InlineData(37.4, "37%")]
    [InlineData(101, "100%")]
    public void FormatPercent_ClampsAndFormatsWithoutDecimals(double value, string expected)
    {
        UiComponentContracts.FormatPercent(value).ShouldBe(expected);
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
        Enum.GetNames<UiComponentContracts.SemanticState>()
            .ShouldContain("Locked");
        Enum.GetNames<UiComponentContracts.SemanticState>()
            .ShouldContain("Warning");
        Enum.GetNames<UiComponentContracts.SemanticState>()
            .ShouldContain("Ok");
        Enum.GetNames<UiComponentContracts.SemanticState>()
            .ShouldContain("Bad");
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

    private static string ReferenceEntryName(UiComponentContracts.CanonicalComponent component) =>
        component switch
        {
            UiComponentContracts.CanonicalComponent.Button => "c_btn",
            UiComponentContracts.CanonicalComponent.IconButton => "c_ib",
            UiComponentContracts.CanonicalComponent.HoldButton => "c_hold",
            UiComponentContracts.CanonicalComponent.Slider => "c_slider",
            UiComponentContracts.CanonicalComponent.Range => "c_range",
            UiComponentContracts.CanonicalComponent.Toggle => "c_toggle",
            UiComponentContracts.CanonicalComponent.Checkbox => "c_check",
            UiComponentContracts.CanonicalComponent.Segmented => "c_seg",
            UiComponentContracts.CanonicalComponent.Picker => "c_pick",
            UiComponentContracts.CanonicalComponent.OverflowMenu => "c_menu",
            UiComponentContracts.CanonicalComponent.Chip => "c_chip",
            UiComponentContracts.CanonicalComponent.ProgressBar => "c_prog",
            UiComponentContracts.CanonicalComponent.TextField => "c_textfield",
            UiComponentContracts.CanonicalComponent.NameField => "c_name",
            UiComponentContracts.CanonicalComponent.Note => "c_note",
            UiComponentContracts.CanonicalComponent.ValueRow => "c_value",
            UiComponentContracts.CanonicalComponent.ReadonlyValue => "c_readonly",
            UiComponentContracts.CanonicalComponent.PowerRow => "c_power",
            UiComponentContracts.CanonicalComponent.MeterRow => "c_meter",
            UiComponentContracts.CanonicalComponent.PartRow => "c_row",
            UiComponentContracts.CanonicalComponent.IconTabs => "c_tabs",
            UiComponentContracts.CanonicalComponent.SelectionHandle => "c_handle",
            UiComponentContracts.CanonicalComponent.PanelHeader => "c_panel_head",
            UiComponentContracts.CanonicalComponent.InfoRow => "c_info_row",
            UiComponentContracts.CanonicalComponent.Card => "c_card",
            UiComponentContracts.CanonicalComponent.Panel => "c_inspector",
            UiComponentContracts.CanonicalComponent.ProgressRing => "c_ring",
            UiComponentContracts.CanonicalComponent.Number => "c_num",
            UiComponentContracts.CanonicalComponent.StageCard => "c_stage",
            _ => throw new ArgumentOutOfRangeException(nameof(component), component, null),
        };
}
