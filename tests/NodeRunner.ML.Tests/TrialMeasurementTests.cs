using NodeRunner.ML.Ga;

namespace NodeRunner.ML.Tests;

public sealed class TrialMeasurementTests
{
    // Ten ticks per second gives a five-tick half-second window, easy to count by hand.
    private const int _ticksPerSecond = 10;
    private const double _maxStep = TrialMeasurement.MaxPlausibleSpeed / _ticksPerSecond;

    // Distance and the score are the centre's furthest forward, never below 0; the front distance is where the
    // front ends up, not its furthest, never below 0.
    [Fact]
    public void Distance_IsTheCentresFurthestForward_AndTheFrontWhereItEndsUp()
    {
        foreach (var (centre, front, distance, frontDistance) in new[]
        {
            (new[] { 110.0, 130, 120, 90 }, new[] { 160.0, 180, 170, 140 }, 30.0, 0.0),
            ([90, 80], [140, 130], 0, 0),
            ([110, 105], [170, 160], 10, 10),
            ([130, 120], [200, 170], 30, 20),
        })
        {
            var measurement = new TrialMeasurement(_ticksPerSecond);
            measurement.Reset(100, 150);
            for (var tick = 0; tick < centre.Length; tick++)
            {
                measurement.Record(centre[tick], front[tick], 0);
            }

            (measurement.Result.Distance, measurement.Result.Fitness, measurement.Result.FrontDistance, measurement.Result.IsValid)
                .ShouldBe((distance, distance, frontDistance, true));
        }
    }

    // Forward progress averaged over half a second: 2 units a tick for five ticks is 20 units/s; a 5-unit jolt in one
    // tick only 10 over the window. It needs a full window and counts only forward motion.
    [Fact]
    public void TopSpeed_AveragesForwardProgressOverHalfASecond()
    {
        foreach (var (positions, topSpeed) in new[]
        {
            (new[] { 2.0, 4, 6, 8, 10, 10, 10 }, 20.0),
            ([0, 0, 0, 0, 0, 5, 5, 5, 5, 5, 5], 10),
            ([10, 20, 30, 40], 0),
            ([-2, -4, -6, -8, -10, -12], 0),
        })
        {
            Run(positions).Result.TopSpeed.ShouldBe(topSpeed, 1e-9, string.Join(", ", positions));
        }
    }

    // The largest ground clearance once it has landed (at most 0.5 up), so the starting drop does not count, a
    // crawler sinking in scores 0, and a new trial starts the drop again.
    [Fact]
    public void Elevation_IsTheLargestClearanceOnceLanded()
    {
        foreach (var (clearances, elevation) in new[]
        {
            (new[] { 0.0, 14, 3, -2 }, 14.0),
            ([5, 2, 0.25, 3], 3),
            ([5, 4], 0),
            ([-0.5, -0.25], 0),
        })
        {
            var measurement = Run([]);
            foreach (var clearance in clearances)
            {
                measurement.Record(0, 0, clearance);
            }

            measurement.Result.Elevation.ShouldBe(elevation, string.Join(", ", clearances));
        }

        var again = Run([0]);
        again.Reset(0, 0);
        again.Record(0, 0, 5);
        again.Result.Elevation.ShouldBe(0);
    }

    // A non-finite sample, or one that moves further in a tick than anything can, is a physics blow-up: the trial
    // scores −∞ until the next starts fresh. The first clearance has nothing to compare with, so a high start is fine.
    [Fact]
    public void ABlowUp_MakesTheTrialInvalid_UntilTheNextStartsFresh()
    {
        foreach (var (centre, front, clearance) in new[]
        {
            (double.NaN, 0.0, 0.0), (double.PositiveInfinity, 0, 0), (0, double.NaN, 0), (0, double.PositiveInfinity, 0),
            (0, 0, double.NaN), (0, 0, double.NegativeInfinity), ((2 * _maxStep) + 1, 0, 0), (0, (2 * _maxStep) + 1, 0),
        })
        {
            var measurement = Run([]);
            measurement.Record(_maxStep, _maxStep, 0);
            measurement.Result.IsValid.ShouldBeTrue();
            measurement.Record(centre, front, clearance);
            (measurement.Result.IsValid, measurement.Result.Fitness).ShouldBe((false, double.NegativeInfinity), $"{centre}, {front}, {clearance}");
            measurement.Record(_maxStep + 1, _maxStep + 1, 0);
            measurement.Result.IsValid.ShouldBeFalse();
        }

        var lowest = Run([]);
        lowest.Record(0, 0, 5 * _maxStep);
        lowest.Record(0, 0, 4 * _maxStep);
        lowest.Result.IsValid.ShouldBeTrue();
        lowest.Record(0, 0, (5 * _maxStep) + 1);
        lowest.Result.IsValid.ShouldBeFalse();

        var fresh = Run([10, 20, 30, 40, 50, 60, double.NaN]);
        fresh.Reset(500, 500);
        fresh.Record(501, 501, 0);
        fresh.Result.ShouldBe(new TrialResult(1, 0, 0, 1));
    }

    // A tear keeps every value as it was, so a loose part thrown forward scores nothing. At ten ticks a second
    // the five samples before the tear give a top speed. The next sample is checked against the tear's sample, as each
    // later tear's against its own; a blow-up after it still makes the trial invalid. A new trial starts whole.
    [Fact]
    public void ATear_FreezesEveryValue_ButStillChecksForABlowUp()
    {
        var measurement = new TrialMeasurement(_ticksPerSecond);
        measurement.Reset(0, 50);
        measurement.IsFrozen.ShouldBeFalse();
        foreach (var x in new[] { 10.0, 20, 30, 40 })
        {
            measurement.Record(x, x, 0);
        }

        measurement.Record(50, 90, 5);
        var before = measurement.Result;
        (before.TopSpeed, before.Elevation, before.FrontDistance).ShouldBe((100, 5, 40));
        measurement.Freeze(50, 90, 5);
        measurement.IsFrozen.ShouldBeTrue();
        measurement.Record(300, 400, 80);
        measurement.Record(600, 700, 160);
        measurement.Result.ShouldBe(before);
        measurement.Reset(0, 0);
        measurement.IsFrozen.ShouldBeFalse();
        measurement.Record(10, 10, 0);
        measurement.Result.ShouldBe(new TrialResult(10, 0, 0, 10));

        foreach (var (tears, after, valid) in new (double[][], double[], bool)[]
        {
            ([[-3000, 2000, 400]], [-2990, 2010, 390], true),
            ([[-3000, 2000, 400], [4000, 6000, 100]], [4010, 6010, 90], true),
            ([[-3000, 2000, 400]], [10, 10, 0], false),
            ([[10, 10, 0]], [5000, 20, 0], false),
            ([[10, 10, 0]], [double.NaN, 10, 0], false),
            ([[10, 10, 0]], [double.PositiveInfinity, 10, 0], false),
            ([[double.NaN, 0, 0]], [10, 10, 0], false),
            ([[double.NegativeInfinity, 0, 0]], [10, 10, 0], false),
        })
        {
            var torn = Run([10]);
            for (var index = 0; index < tears.Length; index++)
            {
                torn.Freeze(tears[index][0], tears[index][1], tears[index][2]);
                if (index < tears.Length - 1)
                {
                    torn.Record(tears[index][0] + 10, tears[index][1] + 10, tears[index][2] - 10);
                }
            }

            torn.Record(after[0], after[1], after[2]);
            torn.Result.ShouldBe(new TrialResult(10, 0, 0, 10, valid), string.Join(", ", after));
        }

        Should.Throw<ArgumentOutOfRangeException>(() => new TrialMeasurement(0));
    }

    private static TrialMeasurement Run(double[] positions)
    {
        var measurement = new TrialMeasurement(_ticksPerSecond);
        measurement.Reset(0, 0);
        foreach (var x in positions)
        {
            measurement.Record(x, x, 0);
        }

        return measurement;
    }
}
