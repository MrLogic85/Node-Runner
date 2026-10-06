namespace NodeRunner.Domain.Tests;

public sealed class SpringDefTests
{
    [Fact]
    public void Constructor_GivesANewSpringTheDefaultSettings()
    {
        var spring = new SpringDef(5, 1, 2);

        spring.Stiffness.ShouldBe(SpringDef.DefaultStiffness);
        spring.Damping.ShouldBe(SpringDef.DefaultDamping);
        spring.Stroke.ShouldBe(1);
        spring.Preload.ShouldBe(1);
        spring.Name.ShouldBeNull();
    }

    [Theory]
    [InlineData(0, 1, 2)]
    [InlineData(5, 0, 2)]
    [InlineData(5, 1, -1)]
    public void Constructor_WithANonPositiveId_Throws(int id, int nodeA, int nodeB) =>
        Should.Throw<ArgumentOutOfRangeException>(() => new SpringDef(id, nodeA, nodeB));

    [Fact]
    public void Constructor_BetweenANodeAndItself_Throws() =>
        Should.Throw<ArgumentException>(() => new SpringDef(5, 1, 1));

    [Theory]
    [InlineData(0, 0.3)]
    [InlineData(-10, 0.3)]
    [InlineData(double.NaN, 0.3)]
    [InlineData(double.PositiveInfinity, 0.3)]
    [InlineData(400, -0.01)]
    [InlineData(400, double.NaN)]
    [InlineData(400, double.PositiveInfinity)]
    public void Constructor_WithAnOutOfRangeSetting_Throws(double stiffness, double damping) =>
        Should.Throw<ArgumentOutOfRangeException>(() => new SpringDef(5, 1, 2, stiffness: stiffness, damping: damping));

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-0.1, 1)]
    [InlineData(1.01, 1)]
    [InlineData(double.NaN, 1)]
    [InlineData(1, -0.51)]
    [InlineData(1, 2.01)]
    [InlineData(1, double.NaN)]
    public void Constructor_WithAnOutOfRangeTravel_Throws(double stroke, double preload) =>
        Should.Throw<ArgumentOutOfRangeException>(() => new SpringDef(5, 1, 2, stroke: stroke, preload: preload));

    [Theory]
    [InlineData(-0.5)]
    [InlineData(0.5)]
    [InlineData(2)]
    public void Constructor_AcceptsAPreloadOutsideItsTravel(double preload) =>
        new SpringDef(5, 1, 2, stroke: 0.1, preload: preload).Preload.ShouldBe(preload);

    [Theory]
    [InlineData(0)]
    [InlineData(250)]
    public void Constructor_AcceptsNoDampingAndAnyPositiveCoefficient(double damping) =>
        new SpringDef(5, 1, 2, damping: damping).Damping.ShouldBe(damping);

    [Fact]
    public void WithNameAndWithSettings_KeepTheRest()
    {
        var spring = new SpringDef(5, 1, 2, "Tail").WithSettings(800, 0.5, 0.4, 1.5).WithName("Knee");

        spring.ShouldBe(new SpringDef(5, 1, 2, "Knee", 800, 0.5, 0.4, 1.5));
    }
}
