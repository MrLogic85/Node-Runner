namespace NodeRunner.Domain.Tests;

public sealed class PistonDefTests
{
    [Theory]
    [InlineData(0, 1, 2)]
    [InlineData(5, 0, 2)]
    [InlineData(5, 1, -1)]
    public void Constructor_WithANonPositiveId_Throws(int id, int nodeA, int nodeB) =>
        Should.Throw<ArgumentOutOfRangeException>(() => new PistonDef(id, nodeA, nodeB));

    [Fact]
    public void Constructor_BetweenANodeAndItself_Throws() =>
        Should.Throw<ArgumentException>(() => new PistonDef(5, 1, 1));

    [Theory]
    [InlineData(0, 0.3, 0.5, 200, 0.2)]
    [InlineData(double.NaN, 0.3, 0.5, 200, 0.2)]
    [InlineData(15000, 0, 0.5, 200, 0.2)]
    [InlineData(15000, 1.01, 0.5, 200, 0.2)]
    [InlineData(15000, double.NaN, 0.5, 200, 0.2)]
    [InlineData(15000, 0.3, -0.01, 200, 0.2)]
    [InlineData(15000, 0.3, 1.01, 200, 0.2)]
    [InlineData(15000, 0.3, double.NaN, 200, 0.2)]
    [InlineData(15000, 0.3, 0.5, 0, 0.2)]
    [InlineData(15000, 0.3, 0.5, double.PositiveInfinity, 0.2)]
    [InlineData(15000, 0.3, 0.5, 200, 0)]
    [InlineData(15000, 0.3, 0.5, 200, double.NaN)]
    public void Constructor_WithAnOutOfRangeSetting_Throws(double strength, double stroke, double start, double maxSpeed, double riseTime) =>
        Should.Throw<ArgumentOutOfRangeException>(() => new PistonDef(5, 1, 2, strength: strength, stroke: stroke, maxSpeed: maxSpeed, riseTime: riseTime, start: start));

    [Theory]
    [InlineData(1, 0)]
    [InlineData(1, 1)]
    [InlineData(0.1, 0.5)]
    public void Constructor_AcceptsTheWholeStrokeAndEitherEndAsItsStart(double stroke, double start)
    {
        var piston = new PistonDef(5, 1, 2, stroke: stroke, start: start);

        piston.Stroke.ShouldBe(stroke);
        piston.Start.ShouldBe(start);
    }

    [Fact]
    public void WithNameAndWithSettings_KeepTheRest()
    {
        var piston = new PistonDef(5, 1, 2, "Ram").WithSettings(20000, 0.7, 0.2, 100, 0.5).WithName("Kick");

        piston.ShouldBe(new PistonDef(5, 1, 2, "Kick", 20000, 0.7, 0.2, 100, 0.5));
    }
}
