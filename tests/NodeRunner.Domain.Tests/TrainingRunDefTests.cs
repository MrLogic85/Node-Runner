namespace NodeRunner.Domain.Tests;

public sealed class TrainingRunDefTests
{
    [Fact]
    public void KeepsTheMeasuredValuesAndMap()
    {
        var run = new TrainingRunDef(120.5, 64, 8.25, MapIds.Flat);

        run.Distance.ShouldBe(120.5);
        run.TopSpeed.ShouldBe(64);
        run.Elevation.ShouldBe(8.25);
        run.MapId.ShouldBe("map-flat");
    }

    [Theory]
    [InlineData(-1, 0, 0)]
    [InlineData(0, -1, 0)]
    [InlineData(0, 0, -1)]
    [InlineData(double.NaN, 0, 0)]
    [InlineData(0, double.PositiveInfinity, 0)]
    public void RejectsNegativeOrNonFiniteValues(double distance, double topSpeed, double elevation) =>
        Should.Throw<ArgumentOutOfRangeException>(() => new TrainingRunDef(distance, topSpeed, elevation, MapIds.Flat));

    [Fact]
    public void ShowsTheFrontDistance()
    {
        var run = new TrainingRunDef(120.5, 64, 8.25, MapIds.Flat, frontDistance: 140);

        run.Distance.ShouldBe(120.5);
        run.ShownDistance.ShouldBe(140);
    }

    [Fact]
    public void WithoutAFrontDistance_ShowsTheCentresDistance() =>
        new TrainingRunDef(120.5, 64, 8.25, MapIds.Flat).ShownDistance.ShouldBe(120.5);

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    public void RejectsANegativeOrNonFiniteFrontDistance(double frontDistance) =>
        Should.Throw<ArgumentOutOfRangeException>(() => new TrainingRunDef(0, 0, 0, MapIds.Flat, frontDistance));

    [Fact]
    public void RequiresAMap() =>
        Should.Throw<ArgumentException>(() => new TrainingRunDef(0, 0, 0, " "));
}
