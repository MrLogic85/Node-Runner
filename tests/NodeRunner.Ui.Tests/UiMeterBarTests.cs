using Godot;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Tests;

/// <summary>A meter bar's fill: centred meters fill from the centre tick, others from the left end, or the bottom when vertical.</summary>
public sealed class UiMeterBarTests
{
    [Theory]
    [InlineData(-1, true, 0, 0.5)]
    [InlineData(1, true, 0.5, 1)]
    [InlineData(-0.5, true, 0.25, 0.5)]
    [InlineData(0.5, false, 0, 0.5)]
    [InlineData(3, true, 0.5, 1)]       // A value past the range is drawn at its end.
    [InlineData(-0.5, false, 0, 0)]
    public void FillSpan_FillsCentredMetersFromTheCentreTick_AndOthersFromTheLeft(double value, bool centred, float start, float end) =>
        UiMeterBar.FillSpan(value, centred).ShouldBe((start, end));

    [Fact]
    public void FillSpan_WithNoValue_DrawsNoFill()
    {
        UiMeterBar.FillSpan(double.NaN, centred: true).ShouldBeNull();
        UiMeterBar.FillSpan(double.NaN, centred: false).ShouldBeNull();
    }

    [Theory]
    [InlineData(0.5, false, 12, 12)]   // A 0…1 bar fills up from the bottom.
    [InlineData(1, true, 0, 12)]       // A positive value fills up from the centre tick.
    [InlineData(-0.5, true, 12, 6)]    // A negative value fills down from it.
    public void FillRect_OfAVerticalMeter_FillsUpFromTheBottom_OrBothWaysFromTheCentre(double value, bool centred, float top, float height) =>
        UiMeterBar.FillRect(new Rect2(0, 0, 8, 24), value, centred, vertical: true).ShouldBe(new Rect2(0, top, 8, height));

    [Fact]
    public void FillRect_OfAHorizontalMeter_FillsAlongItsTrack() =>
        UiMeterBar.FillRect(new Rect2(0, 1, 26, 4), 0.5, centred: true, vertical: false).ShouldBe(new Rect2(13, 1, 6.5f, 4));

    [Fact]
    public void FillRect_WithAnEmptySpan_IsNull() =>
        UiMeterBar.FillRect(new Rect2(0, 0, 8, 24), 0, centred: true, vertical: true).ShouldBeNull();
}
