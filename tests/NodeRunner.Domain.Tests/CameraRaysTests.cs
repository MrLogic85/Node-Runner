namespace NodeRunner.Domain.Tests;

public sealed class CameraRaysTests
{
    [Fact]
    public void DefaultAim_OnLevelBeam_LooksForwardUpForwardAndForwardDownFromLeftToRight()
    {
        var aim = CameraRays.DefaultAim(new Vector2D(0, 0), new Vector2D(10, 0));
        var left = CameraRays.LocalRayTarget(0, aim);
        var centre = CameraRays.LocalRayTarget(1, aim);
        var right = CameraRays.LocalRayTarget(2, aim);

        left.X.ShouldBe(CameraRays.RayLength * Math.Sqrt(0.5), 1e-9);
        left.Y.ShouldBe(-CameraRays.RayLength * Math.Sqrt(0.5), 1e-9);
        centre.X.ShouldBe(CameraRays.RayLength, 1e-9);
        centre.Y.ShouldBe(0, 1e-9);
        right.X.ShouldBe(CameraRays.RayLength * Math.Sqrt(0.5), 1e-9);
        right.Y.ShouldBe(CameraRays.RayLength * Math.Sqrt(0.5), 1e-9);
    }

    [Theory]
    [InlineData(10, 7)]
    [InlineData(-5, -9)]
    [InlineData(-10, 0)]
    [InlineData(0, 10)]
    public void DefaultAim_OnTurnedBeam_LooksForwardUpForwardAndForwardDownInTheWorld(double dx, double dy)
    {
        var nodeA = new Vector2D(3, 4);
        var nodeB = new Vector2D(3 + dx, 4 + dy);
        var aim = CameraRays.DefaultAim(nodeA, nodeB);
        Vector2D[] world = [new(Math.Sqrt(0.5), -Math.Sqrt(0.5)), new(1, 0), new(Math.Sqrt(0.5), Math.Sqrt(0.5))];

        for (var ray = 0; ray < CameraRays.RayCount; ray++)
        {
            var target = ToWorld(CameraRays.LocalRayTarget(ray, aim), CameraRays.BeamAngle(nodeA, nodeB));

            target.X.ShouldBe(world[ray].X * CameraRays.RayLength, 1e-9);
            target.Y.ShouldBe(world[ray].Y * CameraRays.RayLength, 1e-9);
        }
    }

    [Fact]
    public void LocalRayTarget_FansTheRaysASpreadApartAroundTheAim()
    {
        const double aim = 1.2;

        for (var ray = 0; ray < CameraRays.RayCount; ray++)
        {
            var target = CameraRays.LocalRayTarget(ray, aim);
            Math.Atan2(target.Y, target.X).ShouldBe(aim + ((ray - 1) * CameraRays.Spread), 1e-9);
            Math.Sqrt((target.X * target.X) + (target.Y * target.Y)).ShouldBe(CameraRays.RayLength, 1e-9);
        }
    }

    [Fact]
    public void BeamAngle_OfZeroLengthBeam_IsZero()
    {
        CameraRays.BeamAngle(new Vector2D(2, 2), new Vector2D(2, 2)).ShouldBe(0);
    }

    [Theory]
    [InlineData(0.1, 0, 0.1)]
    [InlineData(0.2, 0, 0.2)]
    [InlineData(Math.PI / 2, Math.PI / 2, 0)]
    [InlineData(3.1, -0.1, 3.2 - (2 * Math.PI))]
    public void AimAlong_KeepsTheWorldAngleUnsnappedAndIsRelativeToTheBeam(double worldAngle, double beamAngle, double expected)
    {
        var nodeA = new Vector2D(0, 0);
        var nodeB = new Vector2D(Math.Cos(beamAngle), Math.Sin(beamAngle));

        CameraRays.AimAlong(worldAngle, nodeA, nodeB).ShouldBe(expected, 1e-9);
    }

    [Fact]
    public void AimAlong_WithNonFiniteAngle_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => CameraRays.AimAlong(double.NaN, new Vector2D(0, 0), new Vector2D(1, 0)));
    }

    [Fact]
    public void Wrap_KeepsAnglesWithinHalfATurn()
    {
        CameraRays.Wrap(3 * Math.PI / 2).ShouldBe(-Math.PI / 2, 1e-9);
        CameraRays.Wrap(-3 * Math.PI / 2).ShouldBe(Math.PI / 2, 1e-9);
        CameraRays.Wrap(0.5).ShouldBe(0.5, 1e-12);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void LocalRayTarget_WithNonFiniteAim_Throws(double aim)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => CameraRays.LocalRayTarget(1, aim));
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

    private static Vector2D ToWorld(Vector2D local, double beamAngle)
    {
        var cos = Math.Cos(beamAngle);
        var sin = Math.Sin(beamAngle);
        return new Vector2D((local.X * cos) - (local.Y * sin), (local.X * sin) + (local.Y * cos));
    }
}
