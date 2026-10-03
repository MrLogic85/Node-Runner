using Godot;
using NodeRunner.Theme;

namespace NodeRunner.Ui.Tests;

public sealed class JointDrawingTests
{
    private const float _ringWidth = 2;

    [Fact]
    public void BeamSpan_EndsOnEachRingsCentreLine()
    {
        var span = JointDrawing.BeamSpan(_ringWidth, new Vector2(0, 0), 20, new Vector2(100, 0), 10);

        span.ShouldNotBeNull();
        span.Value.Start.X.ShouldBe(20 - (_ringWidth / 2), 1e-4f);
        span.Value.End.X.ShouldBe(100 - 10 + (_ringWidth / 2), 1e-4f);
        span.Value.Start.Y.ShouldBe(0f);
        span.Value.End.Y.ShouldBe(0f);
    }

    [Fact]
    public void BeamSpan_WithAZeroRadiusEnd_EndsAtThatPoint()
    {
        var span = JointDrawing.BeamSpan(_ringWidth, new Vector2(0, 0), 20, new Vector2(0, 50), 0);

        span.ShouldNotBeNull();
        span.Value.End.ShouldBe(new Vector2(0, 50));
    }

    [Fact]
    public void BeamSpan_WhenTheRingsMeet_IsNull()
    {
        JointDrawing.BeamSpan(_ringWidth, new Vector2(0, 0), 20, new Vector2(30, 0), 20).ShouldBeNull();
    }
}
