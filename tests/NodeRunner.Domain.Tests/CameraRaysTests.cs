namespace NodeRunner.Domain.Tests;

public sealed class CameraRaysTests
{
    [Fact]
    public void LocalRayTarget_OnLevelBeam_LooksForwardForwardDownAndDownFromLeftToRight()
    {
        var left = CameraRays.LocalRayTarget(0, builtRotation: 0);
        var centre = CameraRays.LocalRayTarget(1, builtRotation: 0);
        var right = CameraRays.LocalRayTarget(2, builtRotation: 0);

        left.X.ShouldBe(CameraRays.RayLength, 1e-9);
        left.Y.ShouldBe(0, 1e-9);
        centre.X.ShouldBe(CameraRays.RayLength * Math.Sqrt(0.5), 1e-9);
        centre.Y.ShouldBe(CameraRays.RayLength * Math.Sqrt(0.5), 1e-9);
        right.X.ShouldBe(0, 1e-9);
        right.Y.ShouldBe(CameraRays.RayLength, 1e-9);
    }

    [Theory]
    [InlineData(0.7)]
    [InlineData(-2.1)]
    [InlineData(Math.PI)]
    public void LocalRayTarget_TurnedByBuiltRotation_PointsAlongTheBuiltWorldDirection(double builtRotation)
    {
        Vector2D[] world = [new(1, 0), new(Math.Sqrt(0.5), Math.Sqrt(0.5)), new(0, 1)];

        for (var ray = 0; ray < CameraRays.RayCount; ray++)
        {
            var local = CameraRays.LocalRayTarget(ray, builtRotation);
            var cos = Math.Cos(builtRotation);
            var sin = Math.Sin(builtRotation);
            var worldX = (local.X * cos) - (local.Y * sin);
            var worldY = (local.X * sin) + (local.Y * cos);

            worldX.ShouldBe(world[ray].X * CameraRays.RayLength, 1e-9);
            worldY.ShouldBe(world[ray].Y * CameraRays.RayLength, 1e-9);
        }
    }

    [Fact]
    public void LocalRayTarget_WithUnknownRay_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => CameraRays.LocalRayTarget(3, 0));
    }

    [Theory]
    [InlineData(null, 0)]
    [InlineData(0.0, 1)]
    [InlineData(55.0, 0.75)]
    [InlineData(110.0, 0.5)]
    [InlineData(220.0, 0)]
    [InlineData(500.0, 0)]
    [InlineData(-5.0, 1)]
    public void Reading_IsNearness_ZeroWithNothingSeenAndOneAtContact(double? hitDistance, double expected)
    {
        CameraRays.Reading(hitDistance).ShouldBe(expected, 1e-12);
    }

    [Fact]
    public void RayNames_RunLeftToRightAroundTheCentre()
    {
        CameraRays.RayNames.ShouldBe(["left 1", "centre", "right 1"]);
        CameraRays.RayNames.Count.ShouldBe(CameraRays.RayCount);
    }
}
