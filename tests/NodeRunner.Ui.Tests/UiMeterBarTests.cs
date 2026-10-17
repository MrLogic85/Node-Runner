using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Tests;

/// <summary>A callout meter bar's fill (#1064): centred meters fill from the centre tick, others from the left end.</summary>
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
}
