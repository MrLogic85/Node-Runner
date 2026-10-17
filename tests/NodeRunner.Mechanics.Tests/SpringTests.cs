using NodeRunner.Domain;

namespace NodeRunner.Mechanics.Tests;

public sealed class SpringTests
{
    private const double _built = 100;

    // Drawn 1 m. Point joints, Stroke 100%: drawn at its longest a travel of 50, so Coil length spans 50 + 50 + 50 and
    // the stops sit at ⅓ and ⅔; inside them the stops move round the drawn length as a Piston's round its Start,
    // past them only the rest length moves, evenly. Joints of radius 20: a gap of 60, so 30 + 30 + 30. Plain hubs at
    // Stroke 200%: a gap of 70, so 35 + (70 − 70/3) + 35. Stroke 20% still presses half the drawn length past a stop.
    // A new Spring is drawn at its longest and squeezes to half; joints touching leave no travel.
    [Fact]
    public void CoilLength_MovesTheRestLengthEvenly_FromHalfTheDrawnLengthShortToHalfOfItPast()
    {
        foreach (var (stroke, coilLength, radii, shortest, longest, rest) in new[]
        {
            (1.0, 0.0, 0.0, 100.0, 200.0, 50.0), (1, 1.0 / 6, 0, 100, 200, 75), (1, 1.0 / 3, 0, 100, 200, 100), (1, 0.5, 0, 75, 150, 100),
            (1, 2.0 / 3, 0, 50, 100, 100), (1, 5.0 / 6, 0, 50, 100, 125), (1, 1, 0, 50, 100, 150),
            (1, 0, 40, 100, 160, 70), (1, 0.5, 40, 85, 130, 100), (1, 1, 40, 70, 100, 130),
            (SpringDef.MaxStroke, 0, Travel.HubRadii, 100, 240, 65), (2, 0.5, Travel.HubRadii, 30 + (70 / 1.5), 30 + (210 / 1.5), 100),
            (2, 1, Travel.HubRadii, 30 + (70.0 / 3), 100, 135),
            (0.2, 0, 0, 100, 120, 50), (0.2, 1, 0, 100 - (100.0 / 6), 100, 150),
            (SpringDef.DefaultStroke, SpringDef.DefaultCoilLength, 0, 50, 100, 100), (1, 1, 120, 100, 100, 100),
        })
        {
            var spring = new SpringDef(1, 2, 3, stroke: stroke, coilLength: coilLength);
            var row = $"stroke {stroke}, coil {coilLength}, radii {radii}";

            Spring.ShortestLength(spring, _built, radii).ShouldBe(shortest, 1e-9, row);
            Spring.LongestLength(spring, _built, radii).ShouldBe(longest, 1e-9, row);
            Spring.RestLength(spring, _built, radii).ShouldBe(rest, 1e-9, row);
            Spring.TravelLength(spring, _built, radii).ShouldBe(longest - shortest, 1e-9, row);
        }
    }

    // A real telescope's stage and rod are each at most as long as the shortest gap between the hubs, so the gap
    // can at most triple. Its stops are a Piston's of the same Stroke drawn where its rest length
    // is, and at Start 0% or 100% past a stop; hanging free it shares one drawn at its longest.
    [Fact]
    public void ItsStops_AreAPistonsOfTheSameStroke_DrawnWhereItsRestLengthIs()
    {
        foreach (var (stroke, coilLength) in new[] { (0.1, 0.5), (2, 0), (2, 1) })
        {
            var spring = new SpringDef(1, 2, 3, stroke: stroke, coilLength: coilLength);
            (Spring.ShortestLength(spring, _built, Travel.HubRadii) - Travel.HubRadii).ShouldBeGreaterThanOrEqualTo((Spring.TravelLength(spring, _built, Travel.HubRadii) / 2) - 1e-9);
        }

        foreach (var coilLength in new[] { 0, 0.2, 0.35, 0.5, 0.65, 0.9 })
        {
            var spring = new SpringDef(1, 2, 3, stroke: 0.4, coilLength: coilLength);
            var shortest = Spring.ShortestLength(spring, _built, 40);
            var longest = Spring.LongestLength(spring, _built, 40);
            var piston = new PistonDef(1, 2, 3, stroke: 0.4, start: Math.Clamp((Spring.RestLength(spring, _built, 40) - shortest) / (longest - shortest), 0, 1));
            shortest.ShouldBe(Piston.ShortestLength(piston, _built, 40), 1e-9);
            longest.ShouldBe(Piston.LongestLength(piston, _built, 40), 1e-9);
        }

        var travel = 100 - (100 / 1.4);
        var hanging = new SpringDef(1, 2, 3, stroke: 0.4, coilLength: (50 + travel) / (100 + travel));
        Spring.ShortestLength(hanging, _built, 0).ShouldBe(Piston.ShortestLength(new PistonDef(1, 2, 3, stroke: 0.4, start: 1), _built, 0), 1e-9);
    }

    // Coil length moves its rest length and stops smoothly, with no jump where it meets a stop.
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

    // Stops at 50 and 100, stiffness 1000, step 0.1 s. Rest 150 past the longest: reaching it d away from standstill
    // takes 100·d·reducedMass, and it never pushes more than its own rest length. Rest 20 past the shortest: the
    // same, pulling. Inside its travel it is its rest length.
    [Fact]
    public void StepRestLength_PastAStop_PushesAtMostWhatReachesIt()
    {
        foreach (var (rest, length, speed, reducedMass, stepped) in new[]
        {
            (150.0, 100.0, 0.0, 1.0, 100.0), (150, 99, 0, 1, 99.1), (150, 10, 0, 1, 19), (150, 99, 20, 1, 99), (150, 99, -10, 1, 99.2),
            (150, 99, 0, 1000, 150), (150, 160, 0, 1, 150),
            (20, 50, 0, 1, 50), (20, 51, 0, 1, 50.9), (20, 90, 0, 1, 86), (20, 51, -20, 1, 51), (20, 10, 0, 1, 20),
            (50, 60, 30, 1, 50), (75, 60, 30, 1, 75), (100, 60, 30, 1, 100),
        })
        {
            Spring.StepRestLength(rest, 50, 100, 1000, length, speed, reducedMass, 0.1).ShouldBe(stepped, 1e-9, $"rest {rest}, length {length}, speed {speed}");
        }
    }
}
