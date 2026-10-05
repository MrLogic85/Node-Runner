namespace NodeRunner.Domain.Tests;

public sealed class PistonDefTests
{
    [Fact]
    public void Constructor_GivesANewPistonTheDefaultSettings()
    {
        var piston = new PistonDef(5, 1, 2);

        piston.Strength.ShouldBe(PistonDef.DefaultStrength);
        piston.Stroke.ShouldBe(PistonDef.DefaultStroke);
        piston.MaxSpeed.ShouldBe(PistonDef.DefaultMaxSpeed);
        piston.RiseTime.ShouldBe(PistonDef.DefaultRiseTime);
        piston.Name.ShouldBeNull();
    }

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
    [InlineData(0, 0.3, 200, 0.2)]
    [InlineData(double.NaN, 0.3, 200, 0.2)]
    [InlineData(15000, 0, 200, 0.2)]
    [InlineData(15000, 1, 200, 0.2)]
    [InlineData(15000, 0.3, 0, 0.2)]
    [InlineData(15000, 0.3, double.PositiveInfinity, 0.2)]
    [InlineData(15000, 0.3, 200, 0)]
    [InlineData(15000, 0.3, 200, double.NaN)]
    public void Constructor_WithAnOutOfRangeSetting_Throws(double strength, double stroke, double maxSpeed, double riseTime) =>
        Should.Throw<ArgumentOutOfRangeException>(() => new PistonDef(5, 1, 2, strength: strength, stroke: stroke, maxSpeed: maxSpeed, riseTime: riseTime));

    [Fact]
    public void WithNameAndWithSettings_KeepTheRest()
    {
        var piston = new PistonDef(5, 1, 2, "Ram").WithSettings(20000, 0.5, 100, 0.5).WithName("Kick");

        piston.ShouldBe(new PistonDef(5, 1, 2, "Kick", 20000, 0.5, 100, 0.5));
    }
}
