using NodeRunner.Ui.Widgets;

namespace NodeRunner.Ui.Tests;

public sealed class BrainFocusRowLayoutTests
{
    private const float _fontSize = 12;
    private const float _clipTop = 16;
    private const float _lineSpacing = _fontSize * BrainFocusRowLayout.RowSpacingPerFontSize;
    private const float _top = BrainFocusRowLayout.HeadingBand + BrainFocusRowLayout.VerticalInset;
    private const float _haloRoom = BrainFocusRowLayout.HaloGap + BrainFocusRowLayout.HaloWidth;

    [Fact]
    public void Rows_that_fit_spread_over_the_card_without_scrolling()
    {
        var layout = BrainFocusRowLayout.For(viewHeight: 300, tallest: 12, _fontSize, _clipTop);

        layout.Scrolls.ShouldBeFalse();
        layout.RowY(0, 12, 12).ShouldBe(_top);
        var dotOverWholeHeight = (300 - BrainFocusRowLayout.VerticalInset - _top) / 11 * BrainFocusRowLayout.RadiusPerRow;
        layout.RowY(11, 12, 12).ShouldBe(300 - dotOverWholeHeight - _haloRoom, tolerance: 0.001);
    }

    [Theory]
    [InlineData(60, 2)]
    [InlineData(120, 3)]
    [InlineData(200, 6)]
    [InlineData(300, 12)]
    [InlineData(600, 22)]
    public void Rows_that_fit_keep_the_bottom_selection_halo_inside_the_card(float viewHeight, int tallest)
    {
        var layout = BrainFocusRowLayout.For(viewHeight, tallest, _fontSize, _clipTop);

        layout.Scrolls.ShouldBeFalse();
        (layout.RowY(tallest - 1, tallest, tallest) + layout.Radius + _haloRoom).ShouldBeLessThanOrEqualTo(viewHeight + 0.001f);
    }

    [Theory]
    [InlineData(0f, false)]
    [InlineData(-0.5f, true)]
    public void Rows_scroll_exactly_when_they_would_be_closer_than_a_label_line(float extra, bool scrolls)
    {
        const int tallest = 22;
        var justFits = _top + ((tallest - 1) * _lineSpacing) + (_lineSpacing * BrainFocusRowLayout.RadiusPerRow) + _haloRoom;

        var layout = BrainFocusRowLayout.For(justFits + extra, tallest, _fontSize, _clipTop);

        layout.Scrolls.ShouldBe(scrolls);
        layout.Spacing.ShouldBeGreaterThanOrEqualTo(_lineSpacing - 0.001f);
    }

    [Theory]
    [InlineData(60)]
    [InlineData(150)]
    [InlineData(280)]
    [InlineData(600)]
    public void Rows_are_never_closer_than_a_label_line(float viewHeight)
    {
        BrainFocusRowLayout.For(viewHeight, tallest: 22, _fontSize, _clipTop).Spacing.ShouldBeGreaterThanOrEqualTo(_lineSpacing - 0.001f);
    }

    [Fact]
    public void Scrolled_rows_keep_the_selection_halo_inside_the_card_at_both_ends()
    {
        var layout = BrainFocusRowLayout.For(viewHeight: 200, tallest: 22, _fontSize, _clipTop);
        var haloReach = layout.Radius + BrainFocusRowLayout.HaloGap + BrainFocusRowLayout.HaloWidth;
        var scrolledToEnd = layout.ContentHeight - layout.ViewHeight;

        layout.Scrolls.ShouldBeTrue();
        (layout.RowY(0, 22, 22) - haloReach).ShouldBeGreaterThanOrEqualTo(_clipTop - 0.001f);
        (layout.RowY(21, 22, 22) - scrolledToEnd + haloReach).ShouldBeLessThanOrEqualTo(layout.ViewHeight + 0.001f);
    }

    [Fact]
    public void A_shorter_column_spans_the_same_height_as_the_tallest()
    {
        var layout = BrainFocusRowLayout.For(viewHeight: 200, tallest: 22, _fontSize, _clipTop);

        layout.RowY(0, 12, 22).ShouldBe(layout.RowY(0, 22, 22));
        layout.RowY(11, 12, 22).ShouldBe(layout.RowY(21, 22, 22), tolerance: 0.001);
    }

    [Fact]
    public void A_single_row_sits_in_the_middle()
    {
        var layout = BrainFocusRowLayout.For(viewHeight: 100, tallest: 1, _fontSize, _clipTop);
        var top = BrainFocusRowLayout.HeadingBand + BrainFocusRowLayout.VerticalInset;

        layout.RowY(0, 1, 1).ShouldBe((top + 100 - BrainFocusRowLayout.VerticalInset) / 2, tolerance: 0.001);
        layout.Scrolls.ShouldBeFalse();
    }
}
