using NodeRunner.Theme;

namespace NodeRunner.Ui.Tests;

public sealed class SpringDrawingTests
{
    [Fact]
    public void CoilTurns_WithRoom_KeepsAllTurns()
    {
        SpringDrawing.CoilTurns(200).ShouldBe(SpringDrawing.Turns);
        SpringDrawing.CoilTurns(21).ShouldBe(SpringDrawing.Turns);
    }

    [Fact]
    public void CoilTurns_WhenSqueezed_DropsTurnsButKeepsOne()
    {
        SpringDrawing.CoilTurns(12).ShouldBe(4);
        SpringDrawing.CoilTurns(1).ShouldBe(1);
    }

    [Fact]
    public void BodyLength_FollowsTheBuiltSpanNotTheCurrentOne()
    {
        SpringDrawing.BodyLength(100, 60).ShouldBe(SpringDrawing.BodyLength(100, 140));
    }

    [Fact]
    public void BodyLength_IsClampedBetweenItsLimits()
    {
        SpringDrawing.BodyLength(10, 500).ShouldBe(16);
        SpringDrawing.BodyLength(1000, 500).ShouldBe(48);
    }

    [Fact]
    public void BodyLength_LeavesRoomForTheCoilWhenSqueezed()
    {
        SpringDrawing.BodyLength(100, 20).ShouldBe(8);
    }
}
