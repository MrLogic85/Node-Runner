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

    // A tear (#962) keeps every value as it was, so a loose part thrown forward scores nothing. At ten ticks
    // a second the speed window is half a second, so the five samples before the tear give a top speed.
    [Fact]
    public void Freeze_KeepsEveryValueAsItWas()
    {
        var measurement = new TrialMeasurement(_ticksPerSecond);
        measurement.Reset(0, 50);
        Record(measurement, 10, 20, 30, 40);
        measurement.Record(50, 90, 5);
        var before = measurement.Result;
        before.TopSpeed.ShouldBeGreaterThan(0);
        before.Elevation.ShouldBeGreaterThan(0);
        before.FrontDistance.ShouldBeGreaterThan(0);

        measurement.Freeze(50, 90, 5);
        measurement.Record(300, 400, 80);
        measurement.Record(600, 700, 160);

        measurement.Result.ShouldBe(before);
    }

    // At ten ticks a second a step over 1000 units is a blow-up; after a tear the next sample is checked
    // against the tear's sample, not the last.
    [Fact]
    public void Freeze_LetsTheTrackedValuesJumpToTheTearSample()
    {
        var measurement = Start(0);
        measurement.Record(10, 10, 0);

        measurement.Freeze(-3000, 2000, 400);
        measurement.Record(-2990, 2010, 390);

        measurement.Result.IsValid.ShouldBeTrue();
        measurement.Result.Distance.ShouldBe(10);
    }

    // The tear's own tick: a sample close to the last one but implausibly far from the tear's is a blow-up.
    [Fact]
    public void Freeze_MakesABlowUpFromTheTearSampleInvalid()
    {
        var measurement = Start(0);
        measurement.Record(10, 10, 0);

        measurement.Freeze(-3000, 2000, 400);
        measurement.Record(10, 10, 0);

        measurement.Result.ShouldBe(new TrialResult(10, 0, 0, 10, IsValid: false));
    }

    [Fact]
    public void Freeze_StillMakesALaterBlowUpInvalid()
    {
        var measurement = Start(0);
        measurement.Record(10, 10, 0);

        measurement.Freeze(10, 10, 0);
        measurement.Record(20, 20, 0);
        measurement.Record(5000, 20, 0);

        measurement.Result.ShouldBe(new TrialResult(10, 0, 0, 10, IsValid: false));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Freeze_StillMakesANonFiniteSampleInvalid(double x)
    {
        var measurement = Start(0);
        measurement.Record(10, 10, 0);

        measurement.Freeze(10, 10, 0);
        measurement.Record(x, 10, 0);

        measurement.Result.ShouldBe(new TrialResult(10, 0, 0, 10, IsValid: false));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.NegativeInfinity)]
    public void Freeze_MakesTheTrialInvalidForANonFiniteTearSample(double x)
    {
        var measurement = Start(0);

        measurement.Freeze(x, 0, 0);

        measurement.Result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Reset_UnfreezesTheNextTrial()
    {
        var measurement = Start(0);
        measurement.Freeze(0, 0, 0);

        measurement.Reset(0, 0);
        measurement.Record(10, 10, 0);

        measurement.Result.Distance.ShouldBe(10);
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
