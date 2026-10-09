using NodeRunner.Domain;

namespace NodeRunner.Mechanics.Tests;

public sealed class CameraRaysTests
{
    private const double _range = SensorDef.DefaultRange;

    [Fact]
    public void DefaultCamera_OnLevelBeam_LooksForwardUpForwardAndForwardDownFromLeftToRight()
    {
        var aim = SensorDef.DefaultAim(new Vector2D(0, 0), new Vector2D(10, 0));
        var targets = CameraRays.LocalRayTargets(new SensorDef(1, 2, SensorKind.Camera), aim);

        targets.Length.ShouldBe(3);
        targets[0].X.ShouldBe(220 * Math.Sqrt(0.5), 1e-9);
        targets[0].Y.ShouldBe(-220 * Math.Sqrt(0.5), 1e-9);
        targets[1].X.ShouldBe(220, 1e-9);
        targets[1].Y.ShouldBe(0, 1e-9);
        targets[2].X.ShouldBe(220 * Math.Sqrt(0.5), 1e-9);
        targets[2].Y.ShouldBe(220 * Math.Sqrt(0.5), 1e-9);
    }

    [Theory]
    [InlineData(10, 7)]
    [InlineData(-5, -9)]
    [InlineData(-10, 0)]
    [InlineData(0, 10)]
    public void DefaultCamera_OnTurnedBeam_LooksForwardUpForwardAndForwardDownInTheWorld(double dx, double dy)
    {
        var nodeA = new Vector2D(3, 4);
        var nodeB = new Vector2D(3 + dx, 4 + dy);
        var aim = SensorDef.DefaultAim(nodeA, nodeB);
        Vector2D[] world = [new(Math.Sqrt(0.5), -Math.Sqrt(0.5)), new(1, 0), new(Math.Sqrt(0.5), Math.Sqrt(0.5))];

        var targets = CameraRays.LocalRayTargets(new SensorDef(1, 2, SensorKind.Camera), aim);
        for (var ray = 0; ray < targets.Length; ray++)
        {
            var target = ToWorld(targets[ray], CameraRays.BeamAngle(nodeA, nodeB));

            target.X.ShouldBe(world[ray].X * _range, 1e-9);
            target.Y.ShouldBe(world[ray].Y * _range, 1e-9);
        }
    }

    [Theory]
    [InlineData(1, new[] { 0.0 })]
    [InlineData(3, new[] { -45.0, 0, 45 })]
    [InlineData(5, new[] { -45.0, -22.5, 0, 22.5, 45 })]
    public void RayAngle_SpacesTheRaysEvenly_TheOuterOnesASpreadApart(int rays, double[] degrees)
    {
        for (var ray = 0; ray < rays; ray++)
        {
            CameraRays.RayAngle(ray, rays, Math.PI / 2).ShouldBe(degrees[ray] * Math.PI / 180, 1e-12);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void LocalRayTarget_FansTheRaysAroundTheAim_ToTheRange(int rays)
    {
        const double aim = 1.2;
        const double spread = 0.6;
        const double range = 150;

        for (var ray = 0; ray < rays; ray++)
        {
            var target = CameraRays.LocalRayTarget(ray, aim, rays, spread, range);
            Math.Atan2(target.Y, target.X).ShouldBe(aim + CameraRays.RayAngle(ray, rays, spread), 1e-9);
            Math.Sqrt((target.X * target.X) + (target.Y * target.Y)).ShouldBe(range, 1e-9);
        }

        var outer = Math.Atan2(CameraRays.LocalRayTarget(rays - 1, aim, rays, spread, range).Y, CameraRays.LocalRayTarget(rays - 1, aim, rays, spread, range).X)
            - Math.Atan2(CameraRays.LocalRayTarget(0, aim, rays, spread, range).Y, CameraRays.LocalRayTarget(0, aim, rays, spread, range).X);
        outer.ShouldBe(rays == 1 ? 0 : spread, 1e-9);
        var centre = CameraRays.LocalRayTarget(rays / 2, aim, rays, spread, range);
        Math.Atan2(centre.Y, centre.X).ShouldBe(aim, 1e-9);
    }

    [Fact]
    public void LocalRayTargets_UsesTheCamerasOwnSettings()
    {
        var camera = new SensorDef(1, 2, SensorKind.Camera, rays: 5, spread: Math.PI / 3, range: 300);

        var targets = CameraRays.LocalRayTargets(camera, 0.4);

        targets.Length.ShouldBe(5);
        for (var ray = 0; ray < 5; ray++)
        {
            targets[ray].ShouldBe(CameraRays.LocalRayTarget(ray, 0.4, 5, Math.PI / 3, 300));
        }
    }

    [Fact]
    public void LocalRayTargets_OfAnAccelerometer_Throws()
    {
        Should.Throw<ArgumentException>(() => CameraRays.LocalRayTargets(new SensorDef(1, 2, SensorKind.Accelerometer), 0));
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
        Should.Throw<ArgumentOutOfRangeException>(() => CameraRays.LocalRayTarget(1, aim, 3, Math.PI / 2, _range));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(double.PositiveInfinity)]
    public void LocalRayTarget_WithoutAPositiveRange_Throws(double range)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => CameraRays.LocalRayTarget(1, 0, 3, Math.PI / 2, range));
    }

    [Theory]
    [InlineData(3, 3)]
    [InlineData(-1, 3)]
    [InlineData(1, 1)]
    [InlineData(0, 2)]
    [InlineData(0, 7)]
    public void RayAngle_WithUnknownRayOrCount_Throws(int ray, int rays)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => CameraRays.RayAngle(ray, rays, Math.PI / 2));
    }

    [Theory]
    [InlineData(null, 220, 0)]
    [InlineData(0.0, 220, 1)]
    [InlineData(55.0, 220, 0.75)]
    [InlineData(110.0, 220, 0.5)]
    [InlineData(220.0, 220, 0)]
    [InlineData(500.0, 220, 0)]
    [InlineData(-5.0, 220, 1)]
    [InlineData(50.0, 100, 0.5)]
    [InlineData(100.0, 400, 0.75)]
    [InlineData(150.0, 100, 0)]
    public void Reading_IsNearnessWithinTheRange_ZeroWithNothingSeenAndOneAtContact(double? hitDistance, double range, double expected)
    {
        CameraRays.Reading(hitDistance, range).ShouldBe(expected, 1e-12);
    }

    [Theory]
    [InlineData(null, null, null, new[] { 0.0, 0, 0, 0 })]
    [InlineData(null, 0.0, null, new[] { 0.0, 1, 0, 1 })]
    [InlineData(220.0, null, null, new[] { 0.0, 0, 0, 1 })]
    [InlineData(55.0, 110.0, null, new[] { 0.75, 0.5, 0, 1 })]
    public void Read_WritesEachRaysNearness_ThenWhetherAnyRayHits(double? left, double? centre, double? right, double[] expected)
    {
        var values = new double[5];
        values[^1] = -7;

        CameraRays.Read([left, centre, right], _range, values.AsSpan(0, 4));

        values[..^1].ShouldBe(expected);
        values[^1].ShouldBe(-7);
    }

    [Fact]
    public void Read_WithOneOrFiveRays_WritesARayEach_ThenHit()
    {
        var one = new double[2];
        CameraRays.Read([50.0], 100, one);
        one.ShouldBe([0.5, 1]);

        var five = new double[6];
        CameraRays.Read([null, 25.0, null, null, 100.0], 100, five);
        five.ShouldBe([0, 0.75, 0, 0, 0, 1]);
    }

    [Fact]
    public void Read_WithTheWrongCounts_Throws()
    {
        Should.Throw<ArgumentException>(() => CameraRays.Read([null, null], _range, new double[3]));
        Should.Throw<ArgumentException>(() => CameraRays.Read([null, null, null], _range, new double[3]));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void Read_FillsTheCameraChannels_WithHitLast(int rays)
    {
        var channels = BrainPorts.CameraChannels(rays);
        var values = new double[channels.Count];

        CameraRays.Read(new double?[rays], _range, values);

        channels.Count.ShouldBe(rays + 1);
        channels[^1].ShouldBe(BrainPorts.CameraHitChannel);
    }

    private static Vector2D ToWorld(Vector2D local, double beamAngle)
    {
        var cos = Math.Cos(beamAngle);
        var sin = Math.Sin(beamAngle);
        return new Vector2D((local.X * cos) - (local.Y * sin), (local.X * sin) + (local.Y * cos));
    }
}
