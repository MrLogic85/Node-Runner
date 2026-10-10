using System.Text.RegularExpressions;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Tests;

public sealed partial class UiComponentContractsTests
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

        progress.ShouldBe(new UiSliderValue(UiSliderEnd.Rounded(0), UiSliderEnd.Rounded(0.62)));
        progress.ThumbCount.ShouldBe(0);
        progress.SelectThumb(0.5).ShouldBe(-1);
    }

    [Fact]
    public void SliderValue_PresetsAreSpansWithTheirEnds()
    {
        var thumb = UiSliderValue.Thumb(0.4);
        thumb.ShouldBe(new UiSliderValue(UiSliderEnd.Rounded(0), UiSliderEnd.Thumb(0.4)));
        thumb.ThumbCount.ShouldBe(1);
        thumb.ThumbAt(0).ShouldBe(0.4);

        var range = UiSliderValue.Thumbs(0.8, 0.2);
        range.ShouldBe(new UiSliderValue(UiSliderEnd.Thumb(0.2), UiSliderEnd.Thumb(0.8)));
        range.ThumbCount.ShouldBe(2);
        (range.ThumbAt(0), range.ThumbAt(1)).ShouldBe((0.2, 0.8));
    }

    [Fact]
    public void SliderValue_KeepsEachEndsKind_AndOrdersThePositions()
    {
        var value = new UiSliderValue(UiSliderEnd.Thumb(0.7), UiSliderEnd.Marker(0.3));

        value.ShouldBe(new UiSliderValue(UiSliderEnd.Thumb(0.3), UiSliderEnd.Marker(0.7)));
        value.ThumbAt(0).ShouldBe(0.3);
        Should.Throw<ArgumentOutOfRangeException>(() => value.ThumbAt(1));
    }

    [Fact]
    public void SliderValue_MarkerEnds_HaveNoThumbToDrag()
    {
        var markers = new UiSliderValue(UiSliderEnd.Marker(0.3), UiSliderEnd.Marker(0.7));

        markers.ThumbCount.ShouldBe(0);
        markers.SelectThumb(0.5).ShouldBe(-1);
    }

    [Fact]
    public void SliderValue_WithThumb_MovesOnlyThatEnd_AndStopsAtTheOther()
    {
        var value = new UiSliderValue(UiSliderEnd.Marker(0.2), UiSliderEnd.Thumb(0.6));

        value.WithThumb(0, 0.9).ShouldBe(new UiSliderValue(UiSliderEnd.Marker(0.2), UiSliderEnd.Thumb(0.9)));
        value.WithThumb(0, 0.1).ShouldBe(new UiSliderValue(UiSliderEnd.Marker(0.2), UiSliderEnd.Thumb(0.2)));
        UiSliderValue.Thumbs(0.2, 0.6).WithThumb(0, 0.8).ShouldBe(UiSliderValue.Thumbs(0.6, 0.6));
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
    public void Defaults_MatchReferenceNumberGlowAndSliderContracts()
    {
        UiSize.Widget.NumberDiameter.ShouldBe(16);
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
    public void ButtonAndIconButton_MapToUiButton()
    {
        foreach (var component in new[]
        {
            UiComponentContracts.CanonicalComponent.Button,
            UiComponentContracts.CanonicalComponent.IconButton,
        })
        {
            UiComponentContracts.ControlTypeFor(component).ShouldBe(nameof(UiButton));
        }
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

    [Theory]
    [InlineData(0, 55, 0)]
    [InlineData(40, 12, 40)]
    [InlineData(40, 40, 40)]
    [InlineData(40, 55, 55)]
    [InlineData(40, 54, 54)]
    public void EditorMaxLength_NeverCutsTheTextAlreadyThere(int limit, int currentLength, int expected)
    {
        UiComponentContracts.EditorMaxLength(limit, currentLength).ShouldBe(expected);
    }

    [Theory]
    [InlineData(true, "Name it", "You already have a Walker.", "Name it", true)]
    [InlineData(true, " ", "You already have a Walker.", "You already have a Walker.", false)]
    [InlineData(false, "Name it", "You already have a Walker.", "You already have a Walker.", false)]
    [InlineData(false, "Name it", null, "", false)]
    [InlineData(false, "Name it", " ", "", false)]
    public void FieldLine_ShowsTheErrorOverTheFact(bool inError, string errorText, string? fact, string text, bool isError)
    {
        UiComponentContracts.FieldLine(inError, errorText, fact).ShouldBe((text, isError));
    }
}
