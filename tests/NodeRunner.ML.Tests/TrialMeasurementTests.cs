using NodeRunner.ML.Ga;

namespace NodeRunner.ML.Tests;

public sealed class TrialMeasurementTests
{
    // Ten ticks per second gives a five-tick half-second window, easy to count by hand.
    private const int _ticksPerSecond = 10;

    [Fact]
    public void Distance_IsTheFurthestForwardPositionFromTheStart()
    {
        var measurement = Start(100);

        Record(measurement, 110, 130, 120, 90);

        measurement.Result.Distance.ShouldBe(30);
    }

    [Fact]
    public void Distance_IsZeroWhenTheCreatureOnlyGoesBackward()
    {
        var measurement = Start(100);

        Record(measurement, 90, 80);

        measurement.Result.Distance.ShouldBe(0);
    }

    [Fact]
    public void TopSpeed_AveragesForwardProgressOverHalfASecond()
    {
        var measurement = Start(0);

        // 2 units a tick for five ticks = 10 units in 0.5 s = 20 units/s; then it stops.
        Record(measurement, 2, 4, 6, 8, 10, 10, 10);

        measurement.Result.TopSpeed.ShouldBe(20, tolerance: 1e-9);
    }

    [Fact]
    public void TopSpeed_SpreadsASingleTickJoltOverTheWindow()
    {
        var measurement = Start(0);

        // A 5-unit jolt in one tick is 50 units/s for that tick, but only 10 units/s over half a second.
        Record(measurement, 0, 0, 0, 0, 0, 5, 5, 5, 5, 5, 5);

        measurement.Result.TopSpeed.ShouldBe(10, tolerance: 1e-9);
    }

    [Fact]
    public void TopSpeed_NeedsAFullWindow()
    {
        var measurement = Start(0);

        Record(measurement, 10, 20, 30, 40);

        measurement.Result.TopSpeed.ShouldBe(0);
    }

    [Fact]
    public void TopSpeed_CountsOnlyForwardMotion()
    {
        var measurement = Start(0);

        Record(measurement, -2, -4, -6, -8, -10, -12);

        measurement.Result.TopSpeed.ShouldBe(0);
    }

    [Fact]
    public void Elevation_IsTheLargestGroundClearance()
    {
        var measurement = Start(0);

        measurement.Record(0, 0);
        measurement.Record(0, 14);
        measurement.Record(0, 3);
        measurement.Record(0, -2);

        measurement.Result.Elevation.ShouldBe(14);
    }

    [Fact]
    public void Elevation_IgnoresTheStartingDropUntilTheCreatureLands()
    {
        var measurement = Start(0);

        measurement.Record(0, 5);
        measurement.Record(0, 2);
        measurement.Record(0, 0.25);
        measurement.Record(0, 3);

        measurement.Result.Elevation.ShouldBe(3);
    }

    [Fact]
    public void Elevation_IsZeroWhileTheCreatureHasNotLanded()
    {
        var measurement = Start(0);

        measurement.Record(0, 5);
        measurement.Record(0, 4);

        measurement.Result.Elevation.ShouldBe(0);
    }

    [Fact]
    public void Reset_StartsTheDropAgain()
    {
        var measurement = Start(0);
        measurement.Record(0, 0);

        measurement.Reset(0);
        measurement.Record(0, 5);

        measurement.Result.Elevation.ShouldBe(0);
    }

    [Fact]
    public void Elevation_IsZeroForACrawlerThatSinksSlightlyIntoTheGround()
    {
        var measurement = Start(0);

        measurement.Record(1, -0.5);
        measurement.Record(2, -0.25);

        measurement.Result.Elevation.ShouldBe(0);
    }

    [Fact]
    public void Record_SkipsNonFiniteSamples()
    {
        var measurement = Start(0);

        measurement.Record(double.NaN, double.PositiveInfinity);
        measurement.Record(4, 0);
        measurement.Record(5, 2);

        measurement.Result.ShouldBe(new TrialResult(5, 0, 2));
    }

    [Fact]
    public void Reset_StartsAFreshTrial()
    {
        var measurement = Start(0);
        Record(measurement, 10, 20, 30, 40, 50, 60);
        measurement.Record(60, 9);

        measurement.Reset(500);
        measurement.Record(501, 0);

        measurement.Result.ShouldBe(new TrialResult(1, 0, 0));
    }

    [Fact]
    public void Constructor_RejectsANonPositiveTickRate() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new TrialMeasurement(0));

    private static TrialMeasurement Start(double startX)
    {
        var measurement = new TrialMeasurement(_ticksPerSecond);
        measurement.Reset(startX);
        return measurement;
    }

    private static void Record(TrialMeasurement measurement, params double[] positions)
    {
        foreach (var x in positions)
        {
            measurement.Record(x, 0);
        }
    }
}
