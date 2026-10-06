using NodeRunner.Domain;

namespace NodeRunner.Mechanics.Tests;

public sealed class SpringTests
{
    private const double _built = 100;

    // Joints with no size, so the gap between their edges is the drawn length.
    private const double _pointJoints = 0;

    // Drawn 1 m, Stroke 100%: drawn at its longest a travel of 50, so Coil length spans 50 + 50 + 50 and
    // the stops sit at ⅓ and ⅔. Inside them the stops move round the drawn length as a Piston's round its
    // Start position, its travel growing from 50 to 100 (#974); past them only the rest length moves, evenly.
    [Theory]
    [InlineData(0, 100, 200, 50)]
    [InlineData(1.0 / 6, 100, 200, 75)]
    [InlineData(1.0 / 3, 100, 200, 100)]
    [InlineData(0.5, 75, 150, 100)]
    [InlineData(2.0 / 3, 50, 100, 100)]
    [InlineData(5.0 / 6, 50, 100, 125)]
    [InlineData(1, 50, 100, 150)]
    public void CoilLength_MovesTheRestLengthEvenly_FromHalfTheDrawnLengthShortToHalfOfItPast(double coilLength, double shortest, double longest, double rest)
    {
        var spring = new SpringDef(1, 2, 3, stroke: 1, coilLength: coilLength);

        Spring.ShortestLength(spring, _built, _pointJoints).ShouldBe(shortest, tolerance: 1e-9);
        Spring.LongestLength(spring, _built, _pointJoints).ShouldBe(longest, tolerance: 1e-9);
        Spring.RestLength(spring, _built, _pointJoints).ShouldBe(rest, tolerance: 1e-9);
    }

    // Drawn 1 m between joints of radius 20, Stroke 100%: a gap of 60, so drawn at its longest a travel of 30
    // and Coil length spans 30 + 30 + 30.
    [Theory]
    [InlineData(0, 100, 160, 70)]
    [InlineData(0.5, 85, 130, 100)]
    [InlineData(1, 70, 100, 130)]
    public void ItsTravelAndCoilLength_AreOnTheGapBetweenItsJointsEdges(double coilLength, double shortest, double longest, double rest)
    {
        var spring = new SpringDef(1, 2, 3, stroke: 1, coilLength: coilLength);

        Spring.ShortestLength(spring, _built, 40).ShouldBe(shortest, tolerance: 1e-9);
        Spring.LongestLength(spring, _built, 40).ShouldBe(longest, tolerance: 1e-9);
        Spring.RestLength(spring, _built, 40).ShouldBe(rest, tolerance: 1e-9);
    }

    [Fact]
    public void AServosBiggerJoint_ShortensItsTravel()
    {
        var spring = new SpringDef(1, 2, 3);

        Spring.TravelLength(spring, _built, NodeDef.PlainJointRadius + ServoDef.JointRadius)
            .ShouldBeLessThan(Spring.TravelLength(spring, _built, 2 * NodeDef.PlainJointRadius));
    }

    [Fact]
    public void WithItsJointsTouching_ItHasNoTravel_AndRestsAsDrawn()
    {
        var spring = new SpringDef(1, 2, 3, coilLength: 1);

        Spring.TravelLength(spring, _built, 120).ShouldBe(0);
        Spring.ShortestLength(spring, _built, 120).ShouldBe(_built);
        Spring.RestLength(spring, _built, 120).ShouldBe(_built);
    }

    // So a body from joint A's edge as long as its travel never reaches past joint B's edge.
    [Theory]
    [InlineData(0.1, 0.5)]
    [InlineData(1, 0)]
    [InlineData(1, 1)]
    public void AtItsShortest_TheGapBetweenItsJointsEdges_IsAtLeastItsTravel(double stroke, double coilLength)
    {
        var spring = new SpringDef(1, 2, 3, stroke: stroke, coilLength: coilLength);
        var radii = NodeDef.PlainJointRadius + ServoDef.JointRadius;

        (Spring.ShortestLength(spring, _built, radii) - radii).ShouldBeGreaterThanOrEqualTo(Spring.TravelLength(spring, _built, radii));
    }

    // Stroke 20%: a travel of 20 on its shortest stop and 100/6 on its longest, so the rest length still
    // reaches half the drawn length past a stop.
    [Theory]
    [InlineData(0, 20, 120, 50)]
    [InlineData(1, 100.0 / 6, 100, 150)]
    public void AShortStroke_PressesAsFarPastItsStops(double coilLength, double travel, double longest, double rest)
    {
        var spring = new SpringDef(1, 2, 3, stroke: 0.2, coilLength: coilLength);

        Spring.TravelLength(spring, _built, _pointJoints).ShouldBe(travel, tolerance: 1e-9);
        Spring.LongestLength(spring, _built, _pointJoints).ShouldBe(longest, tolerance: 1e-9);
        Spring.RestLength(spring, _built, _pointJoints).ShouldBe(rest, tolerance: 1e-9);
    }

    [Fact]
    public void ANewSpring_IsDrawnAtItsLongest_AndSqueezesToHalf()
    {
        var spring = new SpringDef(1, 2, 3);

        Spring.LongestLength(spring, _built, _pointJoints).ShouldBe(_built, tolerance: 1e-9);
        Spring.ShortestLength(spring, _built, _pointJoints).ShouldBe(_built / 2, tolerance: 1e-9);
        Spring.RestLength(spring, _built, _pointJoints).ShouldBe(_built, tolerance: 1e-9);
    }

    // Hanging free its rest length is on its longest stop: half the drawn length plus its travel, of the drawn length plus its travel.
    [Fact]
    public void AtTheSameStroke_ASpringHangingFree_SharesAPistonsTravelDrawnAtItsLongest()
    {
        var travel = 100 - (100 / 1.4);
        var spring = new SpringDef(1, 2, 3, stroke: 0.4, coilLength: (50 + travel) / (100 + travel));
        var piston = new PistonDef(1, 2, 3, stroke: 0.4, start: 1);

        Spring.ShortestLength(spring, _built, _pointJoints).ShouldBe(Piston.ShortestLength(piston, _built, _pointJoints), tolerance: 1e-9);
        Spring.LongestLength(spring, _built, _pointJoints).ShouldBe(Piston.LongestLength(piston, _built, _pointJoints), tolerance: 1e-9);
    }

    // Between joints of radius 20 at Stroke 40%, its stops match a Piston's drawn where its rest length is,
    // and a Piston's at Start 0% or 100% past a stop (#974).
    [Theory]
    [InlineData(0)]
    [InlineData(0.2)]
    [InlineData(0.35)]
    [InlineData(0.5)]
    [InlineData(0.65)]
    [InlineData(0.9)]
    public void ItsStops_AreAPistonsOfTheSameStroke_DrawnWhereItsRestLengthIs(double coilLength)
    {
        const double radii = 40;
        var spring = new SpringDef(1, 2, 3, stroke: 0.4, coilLength: coilLength);
        var shortest = Spring.ShortestLength(spring, _built, radii);
        var longest = Spring.LongestLength(spring, _built, radii);
        var start = Math.Clamp((Spring.RestLength(spring, _built, radii) - shortest) / (longest - shortest), 0, 1);
        var piston = new PistonDef(1, 2, 3, stroke: 0.4, start: start);

        shortest.ShouldBe(Piston.ShortestLength(piston, _built, radii), tolerance: 1e-9);
        longest.ShouldBe(Piston.LongestLength(piston, _built, radii), tolerance: 1e-9);
    }

    // Coil length moves its rest length and stops smoothly, with no jump where it meets a stop (#974).
    [Fact]
    public void CoilLength_MovesItsStopsWithoutAJump()
    {
        static SpringDef At(double coilLength) => new(1, 2, 3, stroke: 1, coilLength: Math.Min(coilLength, 1));
        double Longest(double coilLength) => Spring.LongestLength(At(coilLength), _built, 40);
        double Rest(double coilLength) => Spring.RestLength(At(coilLength), _built, 40);

        foreach (var coilLength in Enumerable.Range(0, 1000).Select(step => step / 1000.0))
        {
            Math.Abs(Longest(coilLength + 0.001) - Longest(coilLength)).ShouldBeLessThan(0.2);
            Math.Abs(Rest(coilLength + 0.001) - Rest(coilLength)).ShouldBeLessThan(0.2);
        }
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
