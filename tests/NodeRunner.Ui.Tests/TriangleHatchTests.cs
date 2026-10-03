using Godot;
using NodeRunner.Theme;

namespace NodeRunner.Ui.Tests;

public sealed class TriangleHatchTests
{
    private const float _spacing = 7f;
    private static readonly Vector2 _a = new(0, 0);
    private static readonly Vector2 _b = new(100, 0);
    private static readonly Vector2 _c = new(0, 100);

    [Fact]
    public void Lines_RunDownToTheLeftAtFortyFiveDegrees()
    {
        var lines = TriangleHatch.Lines(_a, _b, _c, _spacing);

        lines.ShouldNotBeEmpty();
        foreach (var (start, end) in lines)
        {
            var direction = (end - start).Normalized();
            Mathf.Abs(direction.X + direction.Y).ShouldBeLessThan(1e-4f);
        }
    }

    [Fact]
    public void Lines_AreSpacingApartAndStayInsideTheTriangle()
    {
        var lines = TriangleHatch.Lines(_a, _b, _c, _spacing);
        var across = new Vector2(1, 1).Normalized();

        lines.Count.ShouldBe(10);
        for (var index = 0; index < lines.Count; index++)
        {
            var (start, end) = lines[index];
            across.Dot(start).ShouldBe((index + 1) * _spacing, 1e-3f);
            Inside(start).ShouldBeTrue();
            Inside(end).ShouldBeTrue();
        }
    }

    [Fact]
    public void Lines_InNeighbouringTrianglesLineUp()
    {
        var left = TriangleHatch.Lines(_a, _b, _c, _spacing);
        var right = TriangleHatch.Lines(_b, new Vector2(100, 100), _c, _spacing);
        var across = new Vector2(1, 1).Normalized();

        foreach (var (start, _) in right)
        {
            var level = across.Dot(start) / _spacing;
            Mathf.Abs(level - Mathf.Round(level)).ShouldBeLessThan(1e-3f);
        }

        left.ShouldNotBeEmpty();
    }

    [Fact]
    public void Lines_StayOutOfTheJointsAtTheCorners()
    {
        const float radius = 15;
        var all = TriangleHatch.Lines(_a, _b, _c, _spacing);
        var clipped = TriangleHatch.Lines(_a, _b, _c, _spacing, radius);

        clipped.ShouldNotBeEmpty();
        foreach (var (start, end) in clipped)
        {
            foreach (var corner in new[] { _a, _b, _c })
            {
                for (var t = 0f; t <= 1; t += 0.05f)
                {
                    corner.DistanceTo(start.Lerp(end, t)).ShouldBeGreaterThanOrEqualTo(radius - 1e-3f);
                }
            }
        }

        // Away from the corners the lines are whole.
        clipped.Sum(line => line.Start.DistanceTo(line.End)).ShouldBeGreaterThan(all.Sum(line => line.Start.DistanceTo(line.End)) * 0.8f);
    }

    [Fact]
    public void Lines_WithNoSpacing_AreEmpty() =>
        TriangleHatch.Lines(_a, _b, _c, 0).ShouldBeEmpty();

    private static bool Inside(Vector2 point) =>
        point.X >= -1e-3f && point.Y >= -1e-3f && point.X + point.Y <= 100 + 1e-3f;
}
