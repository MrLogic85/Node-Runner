using NodeRunner.Domain;

namespace NodeRunner.Mechanics.Tests;

public sealed class WheelTests
{
    // 3 kg per metre of radius, at 100 world units per metre.
    [Theory]
    [InlineData(40, 1.2)]
    [InlineData(70, 2.1)]
    [InlineData(100, 3)]
    public void Mass_Is3KgPerMetreOfRadius(double radius, double kg)
    {
        Wheel.Mass(new WheelDef(6, 1, radius: radius)).ShouldBe(kg, tolerance: 1e-9);
    }

    // A 0.40 m wheel weighs 1.2 kg and a 1 m one 3 kg (3 kg per metre of radius), all in its tyre:
    // a thin ring's m·r², with r in world units (100 per metre).
    [Theory]
    [InlineData(40, 1.2 * 40 * 40)]
    [InlineData(100, 3.0 * 100 * 100)]
    public void Inertia_IsAThinRingAtTheRadius(double radius, double inertia)
    {
        Wheel.Inertia(new WheelDef(1, 2, "Wheel", radius)).ShouldBe(inertia, tolerance: 1e-9);
    }
}
