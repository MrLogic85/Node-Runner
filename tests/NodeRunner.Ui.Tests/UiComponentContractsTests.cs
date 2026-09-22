using NodeRunner.Ui.Lib;
using NodeRunner.Ui.Screens;

namespace NodeRunner.Ui.Tests;

public sealed class UiComponentContractsTests
{
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
    public void ComponentGalleryInventory_CoversEveryCanonicalComponentExactlyOnce()
    {
        var inventory = ComponentGalleryScreen.CanonicalInventory;

        inventory.Select(spec => spec.Component)
            .ShouldBe(UiComponentContracts.AllCanonicalComponents);
        foreach (var component in UiComponentContracts.AllCanonicalComponents)
        {
            inventory.Count(spec => spec.Component == component).ShouldBe(1);
        }

        inventory.Select(spec => spec.EntryName).Distinct().Count()
            .ShouldBe(UiComponentContracts.AllCanonicalComponents.Count);
    }

    [Fact]
    public void ComponentGalleryInventory_UsesReferenceEntryNamesAndVisibleSectionLabels()
    {
        foreach (var spec in ComponentGalleryScreen.CanonicalInventory)
        {
            spec.EntryName.ShouldBe(ReferenceEntryName(spec.Component));
            spec.Section.ShouldNotContain("effects-lite");
            spec.Section.ShouldNotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void ComponentGalleryInventory_LabelsPanelAndCardAsSharedComposition()
    {
        var shared = ComponentGalleryScreen.CanonicalInventory
            .Where(spec => spec.SharedComposition)
            .ToArray();

        shared.Select(spec => spec.Component).ShouldBe(
            [
                UiComponentContracts.CanonicalComponent.CCard,
                UiComponentContracts.CanonicalComponent.CPanel,
            ]);
        shared.Select(spec => spec.Section).Distinct().Single()
            .ShouldContain("shared composition");
        UiComponentContracts.SharesImplementation(
                UiComponentContracts.CanonicalComponent.CCard,
                UiComponentContracts.CanonicalComponent.CPanel)
            .ShouldBeTrue();
    }

    [Fact]
    public void PanelAndCard_ShareImplementation()
    {
        UiComponentContracts.SharesImplementation(
                UiComponentContracts.CanonicalComponent.CCard,
                UiComponentContracts.CanonicalComponent.CPanel)
            .ShouldBeTrue();
    }

    [Fact]
    public void SliderAndRange_ShareImplementation()
    {
        UiComponentContracts.ControlTypeFor(UiComponentContracts.CanonicalComponent.CSlider)
            .ShouldBe(nameof(UiSlider));
        UiComponentContracts.ControlTypeFor(UiComponentContracts.CanonicalComponent.CRange)
            .ShouldBe(nameof(UiSlider));
        UiComponentContracts.SharesImplementation(
                UiComponentContracts.CanonicalComponent.CSlider,
                UiComponentContracts.CanonicalComponent.CRange)
            .ShouldBeTrue();
    }

    [Fact]
    public void ReusableControlEnums_PinDocumentedDefaultsAndVariants()
    {
        Enum.GetNames<UiActionButton.ActionKind>()
            .ShouldBe(["Primary", "Secondary", "Danger", "Flat"]);
        Enum.GetNames<UiIconSize>()
            .ShouldBe(["Small", "Standard", "Large", "ExtraLarge"]);
        Enum.GetNames<UiIconButtonSize>()
            .ShouldBe(["Small", "Default", "Large"]);
        Enum.GetNames<UiButtonContentLayout>()
            .ShouldBe(["Row", "Icon", "Stack"]);
        Enum.GetNames<UiPanel.PanelState>()
            .ShouldBe(["Normal", "Focused", "Selected", "Locked", "Warning", "Danger", "Hint"]);
        Enum.GetNames<UiChip.ChipKind>()
            .ShouldBe(["Neutral", "Accent", "Locked", "Danger", "Warning", "Bad", "Ok"]);
    }

    [Fact]
    public void Defaults_MatchReferenceTouchAndCompletionContracts()
    {
        UiComponentContracts.IconButtonVisibleSize.ShouldBe(40);
        UiTokens.Neon.TouchTarget.ShouldBe(48);
        UiComponentContracts.ProgressRingDiameter.ShouldBe(44);
        UiComponentContracts.HoldCompletionSeconds.ShouldBe(0.8f);
        UiComponentContracts.ButtonProgressOpacity.ShouldBe(0.5f);
        UiGlow.ControlExtent.ShouldBe(12);
        UiGlow.ControlOpacity.ShouldBe(0.4f);
        UiGlow.ButtonExtent.ShouldBe(12);
        UiGlow.ButtonOpacity.ShouldBe(0.12f);
        UiGlow.InsetExtent.ShouldBe(12);
        var sliderStyle = UiSliderStyle.From(UiTokens.Neon);
        sliderStyle.ThumbRadius.ShouldBe(9);
        sliderStyle.TrackWidth.ShouldBe(4);
        UiSliderStyle.DisabledOpacity.ShouldBe(0.5f);
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
            SliderCompactSteppedHeight = 108,
            StrokeHair = 2,
        };

        UiSliderStyle.From(tokens).ShouldBe(new UiSliderStyle(
            ThumbRadius: 18,
            TrackWidth: 8,
            MarkerHalfHeight: 16,
            StepTickHalfHeight: 10,
            DisabledDashLength: 8,
            DisabledThumbInset: 2,
            SteppedHeight: 120,
            CompactSteppedHeight: 108));
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

    [Fact]
    public void NormalizeSliderThumbs_DefaultsClampsSortsAndLimitsToRangeMode()
    {
        UiComponentContracts.NormalizeSliderThumbs(null).ShouldBe([0]);
        UiComponentContracts.NormalizeSliderThumbs([]).ShouldBe([0]);
        UiComponentContracts.NormalizeSliderThumbs([0.8, -1, 0.4]).ShouldBe([0, 0.8]);
    }

    [Theory]
    [InlineData(0, 0, 0, 1)]
    [InlineData(1, 1, 0.9, 0)]
    [InlineData(0.5, 0.5, 0.49, 0)]
    [InlineData(0.5, 0.5, 0.5, 1)]
    [InlineData(0.2, 0.8, 0.4, 0)]
    [InlineData(0.2, 0.8, 0.6, 1)]
    public void SelectSliderThumb_ReopensCollapsedRangesAndChoosesNearest(
        double low,
        double high,
        double position,
        int expected)
    {
        UiComponentContracts.SelectSliderThumb([low, high], position).ShouldBe(expected);
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
        Enum.GetNames<UiComponentContracts.HoldState>()
            .ShouldBe(["Rest", "Holding", "Cancelled", "Completed", "Disabled"]);
        Enum.GetNames<UiComponentContracts.ValidationState>()
            .ShouldBe(["Rest", "Editing", "Invalid"]);
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
    public void HoldLifecycle_AllowsCancellationRetryButKeepsCompletionOneShotUntilReset()
    {
        UiComponentContracts.CanBeginHold(UiComponentContracts.HoldState.Rest).ShouldBeTrue();
        UiComponentContracts.CanBeginHold(UiComponentContracts.HoldState.Cancelled).ShouldBeTrue();
        UiComponentContracts.CanBeginHold(UiComponentContracts.HoldState.Holding).ShouldBeFalse();
        UiComponentContracts.CanBeginHold(UiComponentContracts.HoldState.Completed).ShouldBeFalse();
        UiComponentContracts.CanBeginHold(UiComponentContracts.HoldState.Disabled).ShouldBeFalse();
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
            UiComponentContracts.CanonicalComponent.CBtn => "c_btn",
            UiComponentContracts.CanonicalComponent.CIb => "c_ib",
            UiComponentContracts.CanonicalComponent.CHold => "c_hold",
            UiComponentContracts.CanonicalComponent.CSlider => "c_slider",
            UiComponentContracts.CanonicalComponent.CRange => "c_range",
            UiComponentContracts.CanonicalComponent.CToggle => "c_toggle",
            UiComponentContracts.CanonicalComponent.CCheck => "c_check",
            UiComponentContracts.CanonicalComponent.CSeg => "c_seg",
            UiComponentContracts.CanonicalComponent.CPick => "c_pick",
            UiComponentContracts.CanonicalComponent.CMenu => "c_menu",
            UiComponentContracts.CanonicalComponent.CChip => "c_chip",
            UiComponentContracts.CanonicalComponent.CProg => "c_prog",
            UiComponentContracts.CanonicalComponent.CTextfield => "c_textfield",
            UiComponentContracts.CanonicalComponent.CName => "c_name",
            UiComponentContracts.CanonicalComponent.CValue => "c_value",
            UiComponentContracts.CanonicalComponent.CReadonly => "c_readonly",
            UiComponentContracts.CanonicalComponent.CPower => "c_power",
            UiComponentContracts.CanonicalComponent.CMeter => "c_meter",
            UiComponentContracts.CanonicalComponent.CRow => "c_row",
            UiComponentContracts.CanonicalComponent.CTabs => "c_tabs",
            UiComponentContracts.CanonicalComponent.CPanelHead => "c_panel_head",
            UiComponentContracts.CanonicalComponent.CInfoRow => "c_info_row",
            UiComponentContracts.CanonicalComponent.CCard => "c_card",
            UiComponentContracts.CanonicalComponent.CPanel => "c_panel",
            UiComponentContracts.CanonicalComponent.CRing => "c_ring",
            _ => throw new ArgumentOutOfRangeException(nameof(component), component, null),
        };
}
