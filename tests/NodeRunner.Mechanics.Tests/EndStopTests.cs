namespace NodeRunner.Mechanics.Tests;

public sealed class EndStopTests
{
    // Three times every Piston's force and the weight, under 980 world units/s². One 150 N Piston (15 000 at 100 world
    // units per metre) on 2.4 kg: 3 × (15 000 + 2 352) = 52 056, more than the Piston plus a load of twice its
    // strength. A Spring-only creature's stops hold three times its weight.
    [Fact]
    public void MaxForce_IsThreeTimesThePistonsAndTheWeight()
    {
        EndStop.MaxForce([15000], 2.4, 980).ShouldBe(52056, 1e-9);
        EndStop.MaxForce([15000, 15000, 15000], 18, 980).ShouldBe(3 * (45000 + 17640), 1e-9);
        EndStop.MaxForce([], 5, 980).ShouldBe(14700, 1e-9);
    }
}
