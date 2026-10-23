using Godot;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Tests;

/// <summary>A dial's needle and fill: centred dials stand upright at 0 and fill from the top, others run from the left end.</summary>
public sealed class UiDialTests
{
    private const float _quarter = Mathf.Pi / 2;

    [Theory]
    [InlineData(0, true, 0)]             // At rest a centred dial points up.
    [InlineData(1, true, 1)]             // +1 points right.
    [InlineData(-1, true, -1)]           // −1 points left.
    [InlineData(0.5, true, 0.5)]
    [InlineData(3, true, 1)]             // A value past the range is drawn at its end.
    [InlineData(0, false, -1)]           // A Piston at 0 points left.
    [InlineData(1, false, 1)]            // At 1 it points right.
    [InlineData(0.5, false, 0)]
    [InlineData(-2, false, -1)]
    public void NeedleTurn_TurnsAQuarterEachWay_FromUprightOrFromTheLeftEnd(double value, bool centred, float quarters) =>
        UiDial.NeedleTurn(value, centred)!.Value.ShouldBe(quarters * _quarter, 1e-6f);

    [Fact]
    public void NeedleTurn_AndFillArc_WithNoValue_DrawNothing()
    {
        UiDial.NeedleTurn(double.NaN, centred: true).ShouldBeNull();
        UiDial.NeedleTurn(double.NaN, centred: false).ShouldBeNull();
        UiDial.FillArc(double.NaN, centred: true).ShouldBeNull();
        UiDial.FillArc(double.NaN, centred: false).ShouldBeNull();
    }

    // In quarter turns of Godot's angles: −2 is the arc's left end, −1 its top and 0 its right end.
    [Theory]
    [InlineData(1, true, -1, 0)]         // A centred fill runs from the top to the needle.
    [InlineData(-1, true, -2, -1)]
    [InlineData(0.5, false, -2, -1)]     // A filling fill runs from the left end.
    [InlineData(1, false, -2, 0)]
    public void FillArc_RunsFromTheTop_OrFromTheLeftEnd_ToTheNeedle(double value, bool centred, float start, float end)
    {
        var (from, to) = UiDial.FillArc(value, centred)!.Value;

        from.ShouldBe(start * _quarter, 1e-6f);
        to.ShouldBe(end * _quarter, 1e-6f);
    }
}
