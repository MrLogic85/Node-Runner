using NodeRunner.Domain;

namespace NodeRunner.Mechanics.Tests;

public sealed class WheelTests
{
    // 3 kg per metre of radius at 100 world units per metre, all in its tyre: a thin ring's m·r².
    [Fact]
    public void ItWeighs3KgPerMetreOfRadius_AllInItsTyre()
    {
        foreach (var (radius, kg) in new[] { (40.0, 1.2), (70, 2.1), (100, 3) })
        {
            var wheel = new WheelDef(6, 1, radius: radius);
            Wheel.Mass(wheel).ShouldBe(kg, 1e-9);
            Wheel.Inertia(wheel).ShouldBe(kg * radius * radius, 1e-9);
        }
    }
}
