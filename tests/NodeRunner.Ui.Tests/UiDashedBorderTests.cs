using Godot;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Tests;

public sealed class UiDashedBorderTests
{
    [Fact]
    public void RoundedRectPoints_KeepTheSameCountHoweverLargeTheRect()
    {
        var small = UiDashedBorder.RoundedRectPoints(new Rect2(0, 0, 10, 10), 2);
        var large = UiDashedBorder.RoundedRectPoints(new Rect2(0, 0, 2000, 900), 2);

        large.Length.ShouldBe(small.Length);
    }

    [Fact]
    public void RoundedRectPoints_AreClosedAndTouchEveryEdgeWithoutLeavingTheRect()
    {
        var rect = new Rect2(10, 20, 300, 120);

        var points = UiDashedBorder.RoundedRectPoints(rect, 4);

        points[^1].ShouldBe(points[0]);
        points.ShouldAllBe(point => rect.Grow(1e-3f).HasPoint(point));
        points.Min(point => point.X).ShouldBe(rect.Position.X, 1e-3f);
        points.Max(point => point.X).ShouldBe(rect.End.X, 1e-3f);
        points.Min(point => point.Y).ShouldBe(rect.Position.Y, 1e-3f);
        points.Max(point => point.Y).ShouldBe(rect.End.Y, 1e-3f);
    }

    [Fact]
    public void RoundedRectPoints_WithoutARadius_AreTheFourCorners()
    {
        var rect = new Rect2(0, 0, 8, 6);

        var points = UiDashedBorder.RoundedRectPoints(rect, 0);

        points.ShouldBe([Vector2.Zero, new Vector2(8, 0), new Vector2(8, 6), new Vector2(0, 6), Vector2.Zero]);
    }
}
