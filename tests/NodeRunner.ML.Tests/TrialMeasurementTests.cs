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
    public void FrontDistance_IsWhereTheFrontEndsUpNotItsFurthest()
    {
        var measurement = new TrialMeasurement(_ticksPerSecond);
        measurement.Reset(100, 150);

        measurement.Record(110, 170, 0);
        measurement.Record(105, 160, 0);

        measurement.Result.FrontDistance.ShouldBe(10);
    }

    [Fact]
    public void FrontDistance_IsZeroWhenTheFrontEndsBehindItsStart()
    {
        var measurement = new TrialMeasurement(_ticksPerSecond);
        measurement.Reset(100, 150);

        measurement.Record(100, 140, 0);

        measurement.Result.FrontDistance.ShouldBe(0);
    }

    [Fact]
    public void Fitness_StaysTheCentresFurthestWhateverTheFrontDoes()
    {
        var measurement = new TrialMeasurement(_ticksPerSecond);
        measurement.Reset(100, 150);

        measurement.Record(130, 200, 0);
        measurement.Record(120, 170, 0);

        measurement.Result.Fitness.ShouldBe(30);
        measurement.Result.FrontDistance.ShouldBe(20);
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

        measurement.Record(0, 0, 0);
        measurement.Record(0, 0, 14);
        measurement.Record(0, 0, 3);
        measurement.Record(0, 0, -2);

        measurement.Result.Elevation.ShouldBe(14);
    }

    [Fact]
    public void Elevation_IgnoresTheStartingDropUntilTheCreatureLands()
    {
        var measurement = Start(0);

        measurement.Record(0, 0, 5);
        measurement.Record(0, 0, 2);
        measurement.Record(0, 0, 0.25);
        measurement.Record(0, 0, 3);

        measurement.Result.Elevation.ShouldBe(3);
    }

    [Fact]
    public void Elevation_IsZeroWhileTheCreatureHasNotLanded()
    {
        var measurement = Start(0);

        measurement.Record(0, 0, 5);
        measurement.Record(0, 0, 4);

        measurement.Result.Elevation.ShouldBe(0);
    }

    [Fact]
    public void Reset_StartsTheDropAgain()
    {
        var measurement = Start(0);
        measurement.Record(0, 0, 0);

        measurement.Reset(0, 0);
        measurement.Record(0, 0, 5);

        measurement.Result.Elevation.ShouldBe(0);
    }

    [Fact]
    public void Elevation_IsZeroForACrawlerThatSinksSlightlyIntoTheGround()
    {
        var measurement = Start(0);

        measurement.Record(1, 1, -0.5);
        measurement.Record(2, 2, -0.25);

        measurement.Result.Elevation.ShouldBe(0);
    }

    [Fact]
    public void Result_IsValidForAnOrdinaryTrial()
    {
        var measurement = Start(0);

        Record(measurement, 1, 2, 3);

        measurement.Result.IsValid.ShouldBeTrue();
        measurement.Result.Fitness.ShouldBe(3);
    }

    [Theory]
    [InlineData(double.NaN, 0, 0)]
    [InlineData(double.PositiveInfinity, 0, 0)]
    [InlineData(0, double.NaN, 0)]
    [InlineData(0, double.PositiveInfinity, 0)]
    [InlineData(0, 0, double.NaN)]
    [InlineData(0, 0, double.NegativeInfinity)]
    public void Result_IsInvalidAfterANonFiniteSample(double centerX, double frontX, double groundClearance)
    {
        var measurement = Start(0);
        measurement.Record(1, 1, 0);

        measurement.Record(centerX, frontX, groundClearance);

        measurement.Result.IsValid.ShouldBeFalse();
        measurement.Result.Fitness.ShouldBe(double.NegativeInfinity);
    }

    [Fact]
    public void Result_IsInvalidWhenTheCentreJumpsFurtherThanAnythingCanMoveInOneTick()
    {
        var maxStep = TrialMeasurement.MaxPlausibleSpeed / _ticksPerSecond;
        var measurement = Start(0);

        measurement.Record(maxStep, 0, 0);
        measurement.Result.IsValid.ShouldBeTrue();

        measurement.Record((2 * maxStep) + 1, 0, 0);
        measurement.Result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Result_IsInvalidWhenTheFrontJumpsFurtherThanAnythingCanMoveInOneTick()
    {
        var maxStep = TrialMeasurement.MaxPlausibleSpeed / _ticksPerSecond;
        var measurement = Start(0);

        measurement.Record(0, maxStep, 0);
        measurement.Result.IsValid.ShouldBeTrue();

        measurement.Record(0, (2 * maxStep) + 1, 0);
        measurement.Result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Result_IsInvalidWhenTheLowestPointJumpsFurtherThanAnythingCanMoveInOneTick()
    {
        var maxStep = TrialMeasurement.MaxPlausibleSpeed / _ticksPerSecond;
        var measurement = Start(0);

        // The first clearance has nothing to compare with, so a high start is fine.
        measurement.Record(0, 0, 5 * maxStep);
        measurement.Record(0, 0, 4 * maxStep);
        measurement.Result.IsValid.ShouldBeTrue();

        measurement.Record(0, 0, (5 * maxStep) + 1);
        measurement.Result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Result_StaysInvalidWhenTheTrialCarriesOn()
    {
        var measurement = Start(0);

        measurement.Record(double.NaN, double.NaN, 0);
        Record(measurement, 1, 2, 3);

        measurement.Result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Reset_MakesTheNextTrialValidAgain()
    {
        var measurement = Start(0);
        measurement.Record(double.NaN, double.NaN, 0);

        measurement.Reset(0, 0);
        measurement.Record(1, 1, 0);

        measurement.Result.ShouldBe(new TrialResult(1, 0, 0, 1));
    }

    [Fact]
    public void Reset_StartsAFreshTrial()
    {
        var measurement = Start(0);
        Record(measurement, 10, 20, 30, 40, 50, 60);
        measurement.Record(60, 60, 9);

        measurement.Reset(500, 500);
        measurement.Record(501, 501, 0);

        measurement.Result.ShouldBe(new TrialResult(1, 0, 0, 1));
    }

    [Fact]
    public void Constructor_RejectsANonPositiveTickRate() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new TrialMeasurement(0));

    private static TrialMeasurement Start(double startX)
    {
        var measurement = new TrialMeasurement(_ticksPerSecond);
        measurement.Reset(startX, startX);
        return measurement;
    }

    private static void Record(TrialMeasurement measurement, params double[] positions)
    {
        foreach (var x in positions)
        {
            measurement.Record(x, x, 0);
        }
    }
}
