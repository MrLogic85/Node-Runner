using NodeRunner.Domain;
using NodeRunner.Theme;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Tests;

public sealed class SpringDrawingTests
{
    [Fact]
    public void CoilTurns_StackSolidOverAThirdOfTheSpanAtTheRestLength()
    {
        // A 2 px wire stacks solid 4 apart: 10 turns fill a third of 120.
        SpringDrawing.CoilTurns(restSpan: 120, wire: 2).ShouldBe(10);
    }

    [Fact]
    public void CoilTurns_WithALongerRestLength_AreMore()
    {
        SpringDrawing.CoilTurns(restSpan: 180, wire: 2).ShouldBe(15);
    }

    [Fact]
    public void CoilTurns_WithAThickerWire_AreFewer()
    {
        SpringDrawing.CoilTurns(restSpan: 120, wire: 3).ShouldBe(8);
    }

    [Fact]
    public void CoilTurns_AreAtLeastTheMinimum()
    {
        SpringDrawing.CoilTurns(restSpan: -10, wire: 2).ShouldBe(SpringDrawing.MinTurns);
    }

    [Fact]
    public void CoilTurns_PastALongSpan_GrowWithItsSquareRoot_WithNoMaximum()
    {
        SpringDrawing.CoilTurns(restSpan: SpringDrawing.LongSpan, wire: 2).ShouldBe(16);

        // 200 · √12 ≈ 692.8 of span, a third of it at 4 apart.
        SpringDrawing.CoilTurns(restSpan: 2400, wire: 2).ShouldBe(57);
        SpringDrawing.CoilTurns(restSpan: 9600, wire: 2).ShouldBe(115);
    }

    [Theory]
    [InlineData(0f, SpringDrawing.MinHalfTurnSegments)]
    [InlineData(2.4f, 4)]
    [InlineData(10f, 7)]
    [InlineData(1000f, SpringDrawing.MaxHalfTurnSegments)]
    public void HalfTurnSegments_GrowWithTheCoilOnScreen(float radiusPixels, int segments)
    {
        SpringDrawing.HalfTurnSegments(radiusPixels).ShouldBe(segments);
    }

    [Fact]
    public void Wire_GrowsInProportion_FromAHairlineToADrawnBeamsWidth()
    {
        SpringDrawing.Wire(SpringDef.SoftestStiffness).ShouldBe(UiSize.Stroke.Hair);
        SpringDrawing.Wire(SpringDef.StiffestStiffness).ShouldBe(UiSize.Widget.CreatureBeamWidth);
        (SpringDrawing.Wire(200) - SpringDrawing.Wire(100)).ShouldBe(SpringDrawing.Wire(2000) - SpringDrawing.Wire(1900), tolerance: 1e-5);
        SpringDrawing.Wire(10).ShouldBe(UiSize.Stroke.Hair);
    }

    [Fact]
    public void SeatSpan_LeavesALeadOutsideEachJoint()
    {
        // Seats sit 3 outside each joint's edge, so seat to seat is length − 2 × (10 + 3).
        SpringDrawing.SeatSpan(length: 100, radiusA: 10, radiusB: 10).ShouldBe(100 - 26);
    }
}
