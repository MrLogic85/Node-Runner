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
    public void RequiresAMap() =>
        Should.Throw<ArgumentException>(() => new TrainingRunDef(0, 0, 0, " "));
}
