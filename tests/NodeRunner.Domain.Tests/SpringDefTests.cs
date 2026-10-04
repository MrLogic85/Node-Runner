namespace NodeRunner.Domain.Tests;

public sealed class SpringDefTests
{
    [Fact]
    public void Constructor_GivesANewSpringTheDefaultSettings()
    {
        var spring = new SpringDef(5, 1, 2);

        spring.Stiffness.ShouldBe(SpringDef.DefaultStiffness);
        spring.Damping.ShouldBe(SpringDef.DefaultDamping);
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
    [InlineData(400, 1.01)]
    [InlineData(400, double.NaN)]
    public void Constructor_WithAnOutOfRangeSetting_Throws(double stiffness, double damping) =>
        Should.Throw<ArgumentOutOfRangeException>(() => new SpringDef(5, 1, 2, stiffness: stiffness, damping: damping));

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Constructor_AcceptsNoDampingAndCriticalDamping(double damping) =>
        new SpringDef(5, 1, 2, damping: damping).Damping.ShouldBe(damping);

    [Fact]
    public void WithNameAndWithSettings_KeepTheRest()
    {
        var spring = new SpringDef(5, 1, 2, "Tail").WithSettings(800, 0.5).WithName("Knee");

        spring.ShouldBe(new SpringDef(5, 1, 2, "Knee", 800, 0.5));
    }
}
