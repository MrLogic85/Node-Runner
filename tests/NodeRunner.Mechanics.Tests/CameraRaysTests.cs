using NodeRunner.Domain;

namespace NodeRunner.Mechanics.Tests;

public sealed class CameraRaysTests
{
    private const double _range = SensorDef.DefaultRange;

    // A default Camera looks forward-up, forward and forward-down in the world, however its beam is turned.
    [Fact]
    public void ADefaultCamera_LooksForwardUpForwardAndForwardDown_InTheWorld()
    {
        var half = Math.Sqrt(0.5);
        Vector2D[] world = [new(half, -half), new(1, 0), new(half, half)];
        foreach (var (dx, dy) in new[] { (10.0, 0.0), (10, 7), (-5, -9), (-10, 0), (0, 10) })
        {
            var nodeA = new Vector2D(3, 4);
            var nodeB = new Vector2D(3 + dx, 4 + dy);
            var targets = CameraRays.LocalRayTargets(new SensorDef(1, 2, SensorKind.Camera), SensorDef.DefaultAim(nodeA, nodeB));

            targets.Length.ShouldBe(3);
            for (var ray = 0; ray < 3; ray++)
            {
                var target = ToWorld(targets[ray], CameraRays.BeamAngle(nodeA, nodeB));
                target.X.ShouldBe(world[ray].X * _range, 1e-9, $"({dx}, {dy}) ray {ray}");
                target.Y.ShouldBe(world[ray].Y * _range, 1e-9, $"({dx}, {dy}) ray {ray}");
            }
        }

        CameraRays.BeamAngle(new Vector2D(2, 2), new Vector2D(2, 2)).ShouldBe(0);
    }

    // Its rays fan evenly around the aim out to the range, the outer ones a spread apart, from its own settings.
    [Fact]
    public void ItsRays_FanEvenlyAroundTheAim_ToTheRange()
    {
        foreach (var (rays, degrees) in new[] { (1, new[] { 0.0 }), (3, [-45.0, 0, 45]), (5, [-45.0, -22.5, 0, 22.5, 45]) })
        {
            var camera = new SensorDef(1, 2, SensorKind.Camera, rays: rays, spread: 0.6, range: 150);
            var targets = CameraRays.LocalRayTargets(camera, 1.2);
            targets.Length.ShouldBe(rays);
            for (var ray = 0; ray < rays; ray++)
            {
                CameraRays.RayAngle(ray, rays, Math.PI / 2).ShouldBe(degrees[ray] * Math.PI / 180, 1e-12);
                targets[ray].ShouldBe(CameraRays.LocalRayTarget(ray, 1.2, rays, 0.6, 150));
                Math.Atan2(targets[ray].Y, targets[ray].X).ShouldBe(1.2 + CameraRays.RayAngle(ray, rays, 0.6), 1e-9);
                Math.Sqrt((targets[ray].X * targets[ray].X) + (targets[ray].Y * targets[ray].Y)).ShouldBe(150, 1e-9);
            }
        }
    }

    [Fact]
    public void AimAlong_KeepsTheWorldAngleUnsnapped_RelativeToTheBeam_WrappedWithinHalfATurn()
    {
        foreach (var (worldAngle, beamAngle, aim) in new[] { (0.1, 0.0, 0.1), (Math.PI / 2, Math.PI / 2, 0), (3.1, -0.1, 3.2 - (2 * Math.PI)) })
        {
            CameraRays.AimAlong(worldAngle, new Vector2D(0, 0), new Vector2D(Math.Cos(beamAngle), Math.Sin(beamAngle))).ShouldBe(aim, 1e-9);
        }

        CameraRays.Wrap(3 * Math.PI / 2).ShouldBe(-Math.PI / 2, 1e-9);
        CameraRays.Wrap(-3 * Math.PI / 2).ShouldBe(Math.PI / 2, 1e-9);
        CameraRays.Wrap(0.5).ShouldBe(0.5, 1e-12);
    }

    // Each ray reads its nearness within the range, 0 seeing nothing and 1 at contact.
    [Fact]
    public void ItReadsEachRaysNearness_ThenWhetherAnyRayHits()
    {
        foreach (var (hit, range, nearness) in new (double?, double, double)[]
        {
            (null, 220, 0), (0, 220, 1), (55, 220, 0.75), (110, 220, 0.5), (220, 220, 0), (500, 220, 0), (-5, 220, 1),
            (50, 100, 0.5), (100, 400, 0.75), (150, 100, 0),
        })
        {
            CameraRays.Reading(hit, range).ShouldBe(nearness, 1e-12, $"hit {hit}, range {range}");
        }

        foreach (var (hits, range, read) in new (double?[], double, double[])[]
        {
            ([null, null, null], _range, [0, 0, 0, 0]),
            ([null, 0, null], _range, [0, 1, 0, 1]),
            ([220, null, null], _range, [0, 0, 0, 1]),
            ([55, 110, null], _range, [0.75, 0.5, 0, 1]),
            ([50], 100, [0.5, 1]),
            ([null, 25, null, null, 100], 100, [0, 0.75, 0, 0, 0, 1]),
        })
        {
            var values = new double[read.Length + 1];
            values[^1] = -7;
            CameraRays.Read(hits, range, values.AsSpan(0, read.Length));
            values.ShouldBe([.. read, -7]);
            NetworkPorts.CameraChannels(hits.Length).Count.ShouldBe(read.Length);
            NetworkPorts.CameraChannels(hits.Length)[^1].ShouldBe(NetworkPorts.CameraHitChannel);
        }
    }

    [Fact]
    public void ItRefuses_BadAimsRangesRaysAndCounts()
    {
        Should.Throw<ArgumentException>(() => CameraRays.LocalRayTargets(new SensorDef(1, 2, SensorKind.Accelerometer), 0));
        Should.Throw<ArgumentOutOfRangeException>(() => CameraRays.AimAlong(double.NaN, new Vector2D(0, 0), new Vector2D(1, 0)));
        foreach (var aim in new[] { double.NaN, double.PositiveInfinity })
        {
            Should.Throw<ArgumentOutOfRangeException>(() => CameraRays.LocalRayTarget(1, aim, 3, Math.PI / 2, _range));
        }

        foreach (var range in new[] { 0, -1, double.PositiveInfinity })
        {
            Should.Throw<ArgumentOutOfRangeException>(() => CameraRays.LocalRayTarget(1, 0, 3, Math.PI / 2, range));
        }

        foreach (var (ray, rays) in new[] { (3, 3), (-1, 3), (1, 1), (0, 2), (0, 7) })
        {
            Should.Throw<ArgumentOutOfRangeException>(() => CameraRays.RayAngle(ray, rays, Math.PI / 2));
        }

        Should.Throw<ArgumentException>(() => CameraRays.Read([null, null], _range, new double[3]));
        Should.Throw<ArgumentException>(() => CameraRays.Read([null, null, null], _range, new double[3]));
    }

    private static Vector2D ToWorld(Vector2D local, double beamAngle)
    {
        var cos = Math.Cos(beamAngle);
        var sin = Math.Sin(beamAngle);
        return new Vector2D((local.X * cos) - (local.Y * sin), (local.X * sin) + (local.Y * cos));
    }
}
