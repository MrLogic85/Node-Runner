using NodeRunner.Domain;

namespace NodeRunner.Mechanics.Tests;

public sealed class SpringTests
{
    [Fact]
    public void DampingCoefficient_IsItsShareOfCriticalOnThePairMass()
    {
        // Pair mass 2·2/(2+2) = 1, so critical is 2·√(400·1) = 40.
        var spring = new SpringDef(5, 1, 2, stiffness: 400, damping: 0.25);

        Spring.DampingCoefficient(spring, 2, 2).ShouldBe(10, tolerance: 1e-12);
    }

    [Fact]
    public void DampingCoefficient_AtFullDamping_IsCritical()
    {
        var spring = new SpringDef(5, 1, 2, stiffness: 900, damping: 1);

        // Pair mass 1·3/(1+3) = 0.75.
        Spring.DampingCoefficient(spring, 1, 3).ShouldBe(2 * Math.Sqrt(900 * 0.75), tolerance: 1e-12);
    }

    [Fact]
    public void DampingCoefficient_WithNoDamping_IsZero() =>
        Spring.DampingCoefficient(new SpringDef(5, 1, 2, damping: 0), 1, 1).ShouldBe(0);

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, -1)]
    [InlineData(double.NaN, 1)]
    [InlineData(1, double.PositiveInfinity)]
    public void DampingCoefficient_WithAnInvalidMass_Throws(double massA, double massB) =>
        Should.Throw<ArgumentOutOfRangeException>(() => Spring.DampingCoefficient(new SpringDef(5, 1, 2), massA, massB));
}
