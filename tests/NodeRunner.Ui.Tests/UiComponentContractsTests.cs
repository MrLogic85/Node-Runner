using System.Text.RegularExpressions;
using Godot;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Tests;

public sealed partial class UiComponentContractsTests
{
    [Fact]
    public void ComponentLibraryComponents_MapToReferenceDesignEntryNames()
    {
        var mapped = UiComponentContracts.AllCanonicalComponents
            .Select(UiComponentContracts.ReferenceEntryFor)
            .ToArray();
        var unmapped = UiComponentContracts.ReferenceEntriesWithoutComponent;

        UiComponentContracts.AllCanonicalComponents.ShouldBe(Enum.GetValues<UiComponentContracts.CanonicalComponent>());
        UiComponentContracts.AllCanonicalComponents.ShouldAllBe(component => !string.IsNullOrWhiteSpace(UiComponentContracts.ControlTypeFor(component)));
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
    public void Slider_range_and_value_rows_share_implementation()
    {
        UiComponentContracts.SharesImplementation(UiComponentContracts.CanonicalComponent.Slider, UiComponentContracts.CanonicalComponent.Range).ShouldBeTrue();
        UiComponentContracts.SharesImplementation(UiComponentContracts.CanonicalComponent.PowerRow, UiComponentContracts.CanonicalComponent.ValueRow).ShouldBeTrue();
        UiComponentContracts.SharesImplementation(UiComponentContracts.CanonicalComponent.Slider, UiComponentContracts.CanonicalComponent.Number).ShouldBeFalse();
    }

    [Fact]
    public void SliderValue_orders_its_ends_and_moves_only_thumbs()
    {
        var progress = UiSliderValue.Progress(0.62);
        progress.ShouldBe(new UiSliderValue(UiSliderEnd.Rounded(0), UiSliderEnd.Rounded(0.62)));
        progress.ThumbCount.ShouldBe(0);
        progress.SelectThumb(0.5).ShouldBe(-1);

        UiSliderValue.Thumb(0.4).ShouldBe(new UiSliderValue(UiSliderEnd.Rounded(0), UiSliderEnd.Thumb(0.4)));
        var range = UiSliderValue.Thumbs(0.8, 0.2);
        range.ShouldBe(new UiSliderValue(UiSliderEnd.Thumb(0.2), UiSliderEnd.Thumb(0.8)));
        (range.ThumbCount, range.ThumbAt(0), range.ThumbAt(1)).ShouldBe((2, 0.2, 0.8));

        // Each end keeps its kind while the positions are put in order.
        var mixed = new UiSliderValue(UiSliderEnd.Thumb(0.7), UiSliderEnd.Marker(0.3));
        mixed.ShouldBe(new UiSliderValue(UiSliderEnd.Thumb(0.3), UiSliderEnd.Marker(0.7)));
        Should.Throw<ArgumentOutOfRangeException>(() => mixed.ThumbAt(1));

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
    public void SliderValue_SelectThumb_ReopensCollapsedRangesAndChoosesNearest(double low, double high, double position, int expected) =>
        UiSliderValue.Thumbs(low, high).SelectThumb(position).ShouldBe(expected);

    [Fact]
    public void MenuWidthResolution_PreservesFixedDefaultAndWrapContracts()
    {
        UiMenu.ResolveWidth(UiMenu.MenuWidthMode.Fixed, 0, UiLayout.MenuWidth).ShouldBe(UiLayout.MenuWidth);
        UiMenu.ResolveWidth(UiMenu.MenuWidthMode.Fixed, 220, UiLayout.MenuWidth).ShouldBe(220);
        UiMenu.ResolveWidth(UiMenu.MenuWidthMode.WrapContent, 220, UiLayout.MenuWidth).ShouldBe(0);
    }

    [Theory]
    [InlineData(300, 400, 0, 0)]
    [InlineData(450, 458, 58, 50)]
    [InlineData(380, 458, 58, 0)]
    public void PickerListRoom_IsHowFarTheOpenListRunsPastTheContentWithoutIt(float listBottom, float contentHeight, float reserved, float room) =>
        UiPicker.RoomForList(listBottom, contentHeight, reserved).ShouldBe(room);

    [Theory]
    [InlineData(500, 454, false, 500)] // A picker stays below, even past the screen.
    [InlineData(500, 454, true, 150)]  // Another menu flips up when it fits above.
    [InlineData(300, 454, true, 300)]  // It stays below when it fits there.
    [InlineData(500, 100, true, 500)]  // And below when it fits neither way.
    public void FollowingMenu_FlipsUpOnlyWhenItMay(float below, float anchorTop, bool opensUpward, float top) =>
        UiMenu.FollowTop(below, anchorTop, offset: 4, height: 300, screenBottom: 720, opensUpward).ShouldBe(top);

    [Theory]
    [InlineData(8, false)]
    [InlineData(8.5f, true)]
    public void MenuPress_IsAScrollOnlyPastTheTapSlop(float moved, bool scrolls) =>
        UiMenu.IsScrollDrag(Vector2.Zero, new Vector2(0, moved)).ShouldBe(scrolls);

    [Fact]
    public void MenuDragScroll_CarriesTheFractionInBothDirections()
    {
        var (step, remainder) = UiMenu.ScrollStep(0, 0.6f);
        step.ShouldBe(0);
        (step, remainder) = UiMenu.ScrollStep(remainder, 0.6f);
        step.ShouldBe(1);
        remainder.ShouldBe(0.2f, 0.0001f);
        (step, remainder) = UiMenu.ScrollStep(0, -2.5f);
        step.ShouldBe(-2);
        remainder.ShouldBe(-0.5f, 0.0001f);
    }

    [Theory]
    [InlineData(false, false, false, 18)]
    [InlineData(true, false, false, 34)]
    [InlineData(true, true, false, 46)]
    [InlineData(true, false, true, 46)]
    [InlineData(false, true, false, 30)]
    public void SliderMinimumHeight_ComposesOnlyVisibleLabelRows(bool valueLabels, bool stepLabels, bool markerBelow, float expected) =>
        UiSlider.CalculateMinimumHeight(new UiSliderStyle(9, 4, 9, 5, 8, 1, 80), 13, 12, 8, 13, valueLabels, stepLabels, markerBelow)
            .ShouldBe(expected);

    [Theory]
    [InlineData(double.NaN, 0)]
    [InlineData(double.PositiveInfinity, 1)]
    [InlineData(-0.2, 0)]
    [InlineData(0.4, 0.4)]
    [InlineData(1.2, 1)]
    public void Positions_and_progress_clamp_to_the_normalized_finite_range(double position, double expected)
    {
        UiComponentContracts.ClampSliderPosition(position).ShouldBe(expected);
        UiComponentContracts.ClampProgress(position).ShouldBe(expected);
    }

    [Theory]
    [InlineData(0.3, 0, 0.3)]
    [InlineData(0.4, 0.25, 0.5)]
    [InlineData(1.2, 0.25, 1)]
    [InlineData(-0.1, 0.25, 0)]
    public void SnapSliderPosition_StopsOnWholeSteps(double position, double step, double expected) =>
        UiComponentContracts.SnapSliderPosition(position, step).ShouldBe(expected, 1e-9);

    /// <summary>Progress rounds half away from zero to a whole percent, and shows 99% until it is complete.</summary>
    [Theory]
    [InlineData(0.724, 72, "72%", false)]
    [InlineData(0.725, 73, "73%", false)]
    [InlineData(0.994, 99, "99%", false)]
    [InlineData(0.995, 100, "99%", true)]
    [InlineData(2, 100, "99%", true)]
    public void Progress_rounds_to_whole_percent(double progress, int percent, string text, bool complete)
    {
        UiComponentContracts.ProgressPercent(progress).ShouldBe(percent);
        UiComponentContracts.FormatProgressPercent(progress).ShouldBe(text);
        UiComponentContracts.IsProgressComplete(progress).ShouldBe(complete);
    }

    [Theory]
    [InlineData(double.NaN, "", "0")]
    [InlineData(1.26, " m", "1.3 m")]
    public void FormatNumber_UsesInvariantSingleDecimalAndFiniteFallback(double value, string suffix, string expected) =>
        UiComponentContracts.FormatNumber(value, suffix).ShouldBe(expected);

    [Fact]
    public void NormalizeTabIndex_ClampsWithoutLosingPendingSelection()
    {
        UiComponentContracts.NormalizeTabIndex(9, 4).ShouldBe(3);
        UiComponentContracts.NormalizeTabIndex(-3, 4).ShouldBe(0);
        UiComponentContracts.NormalizeTabIndex(2, 0).ShouldBe(2);
        UiComponentContracts.NormalizeTabIndex(-1, 0).ShouldBe(0);
    }

    [Theory]
    [InlineData(0, 55, 0)]
    [InlineData(40, 12, 40)]
    [InlineData(40, 55, 55)]
    public void EditorMaxLength_NeverCutsTheTextAlreadyThere(int limit, int currentLength, int expected) =>
        UiComponentContracts.EditorMaxLength(limit, currentLength).ShouldBe(expected);

    [Theory]
    [InlineData(true, "Name it", "You already have a Walker.", "Name it", true)]
    [InlineData(true, " ", "You already have a Walker.", "You already have a Walker.", false)]
    [InlineData(false, "Name it", "You already have a Walker.", "You already have a Walker.", false)]
    [InlineData(false, "Name it", null, "", false)]
    public void FieldLine_ShowsTheErrorOverTheFact(bool inError, string errorText, string? fact, string text, bool isError) =>
        UiComponentContracts.FieldLine(inError, errorText, fact).ShouldBe((text, isError));
}
