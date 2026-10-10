using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Tests;

/// <summary>A callout meter bar's fill (#1064): centred meters fill from the centre tick, others from the left end.</summary>
public sealed class UiMeterBarTests
{
    [Theory]
    [InlineData(-1, 0, 0.5)]
    [InlineData(0, 0.5, 0.5)]
    [InlineData(1, 0.5, 1)]
    [InlineData(-0.5, 0.25, 0.5)]
    [InlineData(0.5, 0.5, 0.75)]
    public void FillSpan_ACentredMeter_FillsFromTheCentreTick(double value, float start, float end)
    {
        UiMeterBar.FillSpan(value, centred: true).ShouldBe((start, end));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0.5, 0.5)]
    [InlineData(1, 1)]
    public void FillSpan_AFillingMeter_FillsFromItsLeftEnd(double value, float end)
    {
        UiMeterBar.FillSpan(value, centred: false).ShouldBe((0f, end));
    }

    [Theory]
    [InlineData(3, true, 0.5, 1)]
    [InlineData(-3, true, 0, 0.5)]
    [InlineData(1.5, false, 0, 1)]
    [InlineData(-0.5, false, 0, 0)]
    public void FillSpan_AValuePastTheRange_IsDrawnAtItsEnd(double value, bool centred, float start, float end)
    {
        UiMeterBar.FillSpan(value, centred).ShouldBe((start, end));
    }

    [Fact]
    public void FillSpan_WithNoValue_DrawsNoFill()
    {
        UiMeterBar.FillSpan(double.NaN, centred: true).ShouldBeNull();
        UiMeterBar.FillSpan(double.NaN, centred: false).ShouldBeNull();
    }
}
