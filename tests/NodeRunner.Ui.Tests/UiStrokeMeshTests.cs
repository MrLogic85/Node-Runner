using Godot;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Tests;

public sealed class UiStrokeMeshTests
{
    private const float _half = 2;
    private const float _feather = 1;

    [Fact]
    public void Build_AStraightStroke_HasASolidCoreAsWideAsTheStroke_AndAClearFeather()
    {
        var (points, solid, _) = UiStrokeMesh.Build([[new Vector2(0, 0), new Vector2(10, 0)]], _half, _feather);

        // The first four corners cross the start point: clear, solid, solid, clear.
        points[..4].Select(point => Math.Abs(point.Y)).ShouldBe([_half + _feather, _half, _half, _half + _feather]);
        (points[0].Y * points[3].Y).ShouldBeLessThan(0);
        solid[..4].ShouldBe([false, true, true, false]);
    }

    [Fact]
    public void Build_RoundsBothEnds_ReachingHalfTheWidthPastThem()
    {
        var (points, solid, _) = UiStrokeMesh.Build([[new Vector2(0, 0), new Vector2(10, 0)]], _half, _feather);

        var solidX = points.Where((_, index) => solid[index]).Select(point => point.X).ToArray();
        solidX.Min().ShouldBe(-_half, 1e-4f);
        solidX.Max().ShouldBe(10 + _half, 1e-4f);
        points.Select(point => point.X).Max().ShouldBe(10 + _half + _feather, 1e-4f);
    }

    [Fact]
    public void Build_ManyStrokes_GoInOneList_EachOnItsOwnCorners()
    {
        var one = UiStrokeMesh.Build([[new Vector2(0, 0), new Vector2(10, 0), new Vector2(20, 5)]], _half, _feather);
        var three = UiStrokeMesh.Build(
            Enumerable.Range(0, 3).Select(row => new[] { new Vector2(0, row * 10), new Vector2(10, row * 10), new Vector2(20, (row * 10) + 5) }),
            _half,
            _feather);

        three.Points.Length.ShouldBe(one.Points.Length * 3);

        // Each stroke's triangles are the first one's, moved on to its own corners.
        var moved = Enumerable.Range(0, 3).SelectMany(row => one.Indices.Select(index => index + (row * one.Points.Length)));
        three.Indices.ShouldBe(moved);
    }

    [Fact]
    public void Build_AtABend_KeepsTheWidthAcrossBothLegs()
    {
        var (points, _, _) = UiStrokeMesh.Build([[new Vector2(0, 0), new Vector2(10, 0), new Vector2(10, 10)]], _half, _feather);

        // At the right-angle corner the solid edges meet on the legs' offsets: (12, -2) outside, (8, 2) inside.
        points[5].X.ShouldBe(10 + _half, 1e-4f);
        points[5].Y.ShouldBe(-_half, 1e-4f);
        points[6].X.ShouldBe(10 - _half, 1e-4f);
        points[6].Y.ShouldBe(_half, 1e-4f);
    }

    [Fact]
    public void Build_AStrokeOfOneDistinctPoint_DrawsNothing()
    {
        var (points, _, indices) = UiStrokeMesh.Build([[new Vector2(3, 3), new Vector2(3, 3)], []], _half, _feather);

        points.ShouldBeEmpty();
        indices.ShouldBeEmpty();
    }
}
