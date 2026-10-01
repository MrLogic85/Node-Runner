namespace NodeRunner.Domain.Tests;

public sealed class LineOfSightTests
{
    [Fact]
    public void LocalRayTarget_OnLevelBeam_MatchesTheOldCoreRays()
    {
        var down = LineOfSight.LocalRayTarget(0, builtRotation: 0);
        var forward = LineOfSight.LocalRayTarget(1, builtRotation: 0);
        var forwardDown = LineOfSight.LocalRayTarget(2, builtRotation: 0);

        down.X.ShouldBe(0, 1e-9);
        down.Y.ShouldBe(LineOfSight.RayLength, 1e-9);
        forward.X.ShouldBe(LineOfSight.RayLength, 1e-9);
        forward.Y.ShouldBe(0, 1e-9);
        forwardDown.X.ShouldBe(LineOfSight.RayLength * Math.Sqrt(0.5), 1e-9);
        forwardDown.Y.ShouldBe(LineOfSight.RayLength * Math.Sqrt(0.5), 1e-9);
    }

    [Theory]
    [InlineData(0.7)]
    [InlineData(-2.1)]
    [InlineData(Math.PI)]
    public void LocalRayTarget_TurnedByBuiltRotation_PointsAlongTheBuiltWorldDirection(double builtRotation)
    {
        Vector2D[] world = [new(0, 1), new(1, 0), new(Math.Sqrt(0.5), Math.Sqrt(0.5))];

        for (var ray = 0; ray < LineOfSight.RayCount; ray++)
        {
            var local = LineOfSight.LocalRayTarget(ray, builtRotation);
            var cos = Math.Cos(builtRotation);
            var sin = Math.Sin(builtRotation);
            var worldX = (local.X * cos) - (local.Y * sin);
            var worldY = (local.X * sin) + (local.Y * cos);

            worldX.ShouldBe(world[ray].X * LineOfSight.RayLength, 1e-9);
            worldY.ShouldBe(world[ray].Y * LineOfSight.RayLength, 1e-9);
        }
    }

    [Fact]
    public void LocalRayTarget_WithUnknownRay_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => LineOfSight.LocalRayTarget(3, 0));
    }

    [Theory]
    [InlineData(null, 1)]
    [InlineData(0.0, 0)]
    [InlineData(110.0, 0.5)]
    [InlineData(500.0, 1)]
    public void Reading_IsHitDistanceOverRayLength(double? hitDistance, double expected)
    {
        LineOfSight.Reading(hitDistance).ShouldBe(expected, 1e-12);
    }

    [Fact]
    public void RayNames_HaveOnePerRay()
    {
        LineOfSight.RayNames.Count.ShouldBe(LineOfSight.RayCount);
    }
}
