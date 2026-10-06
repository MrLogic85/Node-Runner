using NodeRunner.Theme;

namespace NodeRunner.Ui.Tests;

public sealed class PistonDrawingTests
{
    [Theory]
    [InlineData(0f, 12f)]
    [InlineData(0.005f, 12f)]
    [InlineData(-0.005f, 12f)]
    [InlineData(5f, 12f)]
    [InlineData(-5f, -12f)]
    public void TickPastB_InsideTheJoint_MovesOutToItsEdge(float past, float drawn)
    {
        PistonDrawing.TickPastB(past, clear: 12).ShouldBe(drawn);
    }

    [Theory]
    [InlineData(20f)]
    [InlineData(-30f)]
    public void TickPastB_ClearOfTheJoint_StaysPut(float past)
    {
        PistonDrawing.TickPastB(past, clear: 12).ShouldBe(past);
    }
}
