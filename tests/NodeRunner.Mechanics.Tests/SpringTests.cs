using NodeRunner.Domain;

namespace NodeRunner.Mechanics.Tests;

public sealed class SpringTests
{
    private const double _built = 100;

    // The table in #835: drawn 1 m, Stroke 100%. Outside 0…1 the travel stays put and only the rest length moves.
    [Theory]
    [InlineData(-0.5, 100, 200, 50)]
    [InlineData(0, 100, 200, 100)]
    [InlineData(0.5, 200.0 / 3, 400.0 / 3, 100)]
    [InlineData(1, 50, 100, 100)]
    [InlineData(2, 50, 100, 150)]
    public void Travel_PutsTheDrawnLengthAtItsPreload_AndRestLength_GoesPastAStop(double preload, double shortest, double longest, double rest)
    {
        var spring = new SpringDef(1, 2, 3, stroke: 1, preload: preload);

        Spring.ShortestLength(spring, _built).ShouldBe(shortest, tolerance: 1e-9);
        Spring.LongestLength(spring, _built).ShouldBe(longest, tolerance: 1e-9);
        Spring.RestLength(spring, _built).ShouldBe(rest, tolerance: 1e-9);
    }

    [Fact]
    public void ANewSpring_IsDrawnAtItsLongest_AndSqueezesToHalf()
    {
        var spring = new SpringDef(1, 2, 3);

        Spring.LongestLength(spring, _built).ShouldBe(_built, tolerance: 1e-9);
        Spring.ShortestLength(spring, _built).ShouldBe(_built / 2, tolerance: 1e-9);
    }

    [Fact]
    public void AtTheSameStrokeAndPosition_ASpringAndAPistonShareTheirTravel()
    {
        var spring = new SpringDef(1, 2, 3, stroke: 0.4, preload: 0.25);
        var piston = new PistonDef(1, 2, 3, stroke: 0.4, start: 0.25);

        Spring.ShortestLength(spring, _built).ShouldBe(Piston.ShortestLength(piston, _built));
        Spring.LongestLength(spring, _built).ShouldBe(Piston.LongestLength(piston, _built));
    }

    // Rest 150 past the longest stop at 100, stiffness 1000, step 0.1 s: reaching a stop d away
    // from standstill takes 100·d·reducedMass, and it never pushes more than its own rest length.
    [Theory]
    [InlineData(100, 0, 1, 100)]
    [InlineData(99, 0, 1, 99.1)]
    [InlineData(10, 0, 1, 19)]
    [InlineData(99, 20, 1, 99)]
    [InlineData(99, -10, 1, 99.2)]
    [InlineData(99, 0, 1000, 150)]
    [InlineData(160, 0, 1, 150)]
    public void StepRestLength_PastTheLongestStop_PushesAtMostWhatReachesIt(double length, double speed, double reducedMass, double expected)
    {
        Spring.StepRestLength(150, 50, 100, 1000, length, speed, reducedMass, 0.1).ShouldBe(expected, tolerance: 1e-9);
    }

    // Rest 20 past the shortest stop at 50: the same, pulling.
    [Theory]
    [InlineData(50, 0, 50)]
    [InlineData(51, 0, 50.9)]
    [InlineData(90, 0, 86)]
    [InlineData(51, -20, 51)]
    [InlineData(10, 0, 20)]
    public void StepRestLength_PastTheShortestStop_PullsAtMostWhatReachesIt(double length, double speed, double expected)
    {
        Spring.StepRestLength(20, 50, 100, 1000, length, speed, 1, 0.1).ShouldBe(expected, tolerance: 1e-9);
    }

    [Theory]
    [InlineData(50)]
    [InlineData(75)]
    [InlineData(100)]
    public void StepRestLength_InsideItsTravel_IsItsRestLength(double rest)
    {
        Spring.StepRestLength(rest, 50, 100, 1000, 60, 30, 1, 0.1).ShouldBe(rest);
    }
}
