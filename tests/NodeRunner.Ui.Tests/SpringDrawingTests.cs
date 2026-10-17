using NodeRunner.Domain;
using NodeRunner.Theme;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Tests;

public sealed class SpringDrawingTests
{
    // A shortest span with room for every turn stacked solid.
    private const float _roomy = 10_000;

    [Fact]
    public void CoilTurns_StackSolidOverAThirdOfTheSpanAtTheRestLength()
    {
        // A 2 px wire stacks solid 4 apart: 10 turns fill a third of 120.
        SpringDrawing.CoilTurns(restSpan: 120, shortestSpan: _roomy, wire: 2).ShouldBe(10);
        SpringDrawing.CoilTurns(restSpan: 180, shortestSpan: _roomy, wire: 2).ShouldBe(15);
        SpringDrawing.CoilTurns(restSpan: 120, shortestSpan: _roomy, wire: 3).ShouldBe(8);
        SpringDrawing.CoilTurns(restSpan: -10, shortestSpan: _roomy, wire: 2).ShouldBe(SpringDrawing.MinTurns);
    }

    // Stroke 200% squeezes it to a third of its gap, past what a third of its rest span stacks solid in (#1211).
    [Fact]
    public void CoilTurns_AreNeverMoreThanFitWireToWireAtItsShortest()
    {
        // 10 turns would fill a third of 120, but only 6 of a 2 wide wire fit in 12.
        SpringDrawing.CoilTurns(restSpan: 120, shortestSpan: 12, wire: 2).ShouldBe(6);
        SpringDrawing.CoilTurns(restSpan: 120, shortestSpan: 4, wire: 2).ShouldBe(SpringDrawing.MinTurns);
    }

    [Fact]
    public void CoilTurns_GrowWithCoilLength_AtStroke200()
    {
        // A 1 m Spring at Stroke 200%, seat to seat: Coil length 0 rests at 0.65 m and stops at 1 m,
        // 50% rests at 1 m and stops at 0.77 m, 100% rests at 1.35 m and stops at 0.53 m.
        var coil0 = SpringDrawing.CoilTurns(SpringDrawing.SeatSpan(65, 15, 15), SpringDrawing.SeatSpan(100, 15, 15), wire: 2);
        var coil50 = SpringDrawing.CoilTurns(SpringDrawing.SeatSpan(100, 15, 15), SpringDrawing.SeatSpan(76.67f, 15, 15), wire: 2);
        var coil100 = SpringDrawing.CoilTurns(SpringDrawing.SeatSpan(135, 15, 15), SpringDrawing.SeatSpan(53.33f, 15, 15), wire: 2);

        coil0.ShouldBeLessThan(coil50);
        coil50.ShouldBeLessThan(coil100);
    }

    [Fact]
    public void CoilTurns_PastALongSpan_GrowWithItsSquareRoot_WithNoMaximum()
    {
        SpringDrawing.CoilTurns(restSpan: SpringDrawing.LongSpan, shortestSpan: _roomy, wire: 2).ShouldBe(16);

        // 200 · √12 ≈ 692.8 of span, a third of it at 4 apart.
        SpringDrawing.CoilTurns(restSpan: 2400, shortestSpan: _roomy, wire: 2).ShouldBe(57);
    }

    [Theory]
    [InlineData(0f, SpringDrawing.MinHalfTurnSegments)]
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

    // Seats sit 3 outside what covers each end (#1086, #1211): a plain joint's hub, 15, or something wider, as a Servo's housing.
    [Theory]
    [InlineData(15, 15, 100 - 36)]
    [InlineData(31, 15, 100 - 52)]
    public void SeatSpan_LeavesALeadOutsideWhatCoversEachEnd(float coveredA, float coveredB, float span)
    {
        SpringDrawing.SeatSpan(length: 100, coveredA, coveredB).ShouldBe(span);
    }
}
