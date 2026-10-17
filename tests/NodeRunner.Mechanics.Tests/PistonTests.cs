using NodeRunner.Domain;

namespace NodeRunner.Mechanics.Tests;

public sealed class PistonTests
{
    private const double _built = 100;

    // Joints with no size, so the gap between their edges is the drawn length.
    private const double _pointJoints = 0;

    private const double _step = 1.0 / 60;

    private static readonly PistonDef _piston = new(1, 2, 3);

    // Drawn 1 m: point joints, then joints of radius 20 (a gap of 60), plain hubs
    // (a gap of 70 that Stroke 200% triples), a new Piston, and joints touching, which cannot move.
    [Fact]
    public void ItsTravel_GrowsTheGapBetweenItsJoints_ByItsStroke_WithTheDrawnLengthAtItsStart()
    {
        foreach (var (stroke, start, radii, shortest, longest) in new[]
        {
            (1.0, 0.0, 0.0, 100.0, 200.0), (1, 0.5, 0, 200.0 / 3, 400.0 / 3), (1, 1, 0, 50, 100),
            (1, 0, 40, 100, 160), (1, 1, 40, 70, 100),
            (PistonDef.MaxStroke, 0, Travel.HubRadii, 100, 240), (2, 0.5, Travel.HubRadii, 65, 135), (2, 1, Travel.HubRadii, 30 + (70.0 / 3), 100),
            (PistonDef.DefaultStroke, PistonDef.DefaultStart, 0, 80, 120), (0.5, 0.5, 120, 100, 100),
        })
        {
            var piston = new PistonDef(1, 2, 3, stroke: stroke, start: start);
            var row = $"stroke {stroke}, start {start}, radii {radii}";

            Piston.ShortestLength(piston, _built, radii).ShouldBe(shortest, 1e-9, row);
            Piston.LongestLength(piston, _built, radii).ShouldBe(longest, 1e-9, row);
            if (shortest != longest)
            {
                Piston.LengthInput(piston, _built, radii, shortest).ShouldBe(0, 1e-12, row);
                Piston.LengthInput(piston, _built, radii, longest).ShouldBe(1, 1e-12, row);
                Piston.LengthInput(piston, _built, radii, _built).ShouldBe(start, 1e-12, row);
                Piston.TargetLength(piston, _built, radii, Piston.DrawnPosition(piston)).ShouldBe(_built, 1e-9, row);
            }
        }
    }

    // A real telescope's stage and rod are each at most as long as the shortest gap between the hubs,
    // so the gap can at most triple.
    [Fact]
    public void AtItsShortest_TheGapBetweenItsJointsHubs_IsAtLeastHalfItsTravel()
    {
        foreach (var (stroke, start) in new[] { (0.1, 0.0), (2, 0), (2, 1), (0.5, 0.5) })
        {
            var piston = new PistonDef(1, 2, 3, stroke: stroke, start: start);
            var shortest = Piston.ShortestLength(piston, _built, Travel.HubRadii);
            (shortest - Travel.HubRadii).ShouldBeGreaterThanOrEqualTo(((Piston.LongestLength(piston, _built, Travel.HubRadii) - shortest) / 2) - 1e-9);
        }
    }

    [Fact]
    public void ItsInputsAndTarget_ReadAndAskOnItsTravel()
    {
        Piston.SpeedInput(0, 200).ShouldBe(0);
        Piston.SpeedInput(200, 200).ShouldBe(Math.Tanh(1), 1e-12);
        Piston.SpeedInput(-20000, 200).ShouldBe(-1, 1e-9);

        var piston = new PistonDef(1, 2, 3, stroke: 1, start: 0);
        foreach (var (position, length) in new[] { (-1.0, 100.0), (0, 100), (0.5, 150), (1, 200), (3, 200) })
        {
            Piston.TargetLength(piston, _built, _pointJoints, position).ShouldBe(length, 1e-9);
        }

        var creature = new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(100, 0)), new NodeDef(3, new Vector2D(0, 100))],
            [],
            [],
            [new PistonDef(4, 1, 2, start: 0.25), new PistonDef(5, 1, 3, start: 1)],
            nextPartId: 6);
        Piston.DrawnPositions(creature).ShouldBe(new Dictionary<int, double> { [4] = 0.25, [5] = 1 });
    }

    // From rest a whole stroke away: tanh(3) of the rate, through a gate that is nearly open. Held still, it reaches
    // its whole chosen share within about its rise time and no more; it dies away once it moves as fast as it wants.
    [Fact]
    public void ItsForce_BuildsTowardsItsTarget_OverItsRiseTime_UpToTheChosenShare()
    {
        Next(length: _built, speed: 0, position: 0.5, force: 0).ShouldBe(0);
        Next(length: _built, speed: -1000, position: 1, force: 0, strength: 0).ShouldBe(0);
        var push = Next(length: _built, speed: 0, position: 1, force: 0);
        push.ShouldBe(_piston.Strength * _step / _piston.RiseTime * Math.Tanh(3) / (1 + Math.Exp(-10)), 1e-9);
        Next(length: _built, speed: 0, position: 0, force: 0).ShouldBe(-push, 1e-9);

        var forces = Hold(strength: 1, riseTime: _piston.RiseTime);
        forces[(int)(0.9 * _piston.RiseTime / _step)].ShouldBeLessThan(_piston.Strength);
        forces[(int)(1.1 * _piston.RiseTime / _step)].ShouldBe(_piston.Strength);
        forces.ShouldAllBe(force => force <= _piston.Strength);
        Hold(strength: 0.2, riseTime: _piston.RiseTime).Max().ShouldBe(OutputSignals.StrengthFromOutput(0.2, _piston.Strength));
        Hold(strength: 1, riseTime: 0.5)[3].ShouldBe(Hold(strength: 1, riseTime: 0.1)[3] / 5, 1e-6);

        var pushing = _piston.Strength / 2;
        var atSpeed = Next(length: _built, speed: _piston.MaxSpeed, position: 1, force: pushing);
        atSpeed.ShouldBeLessThan(pushing);
        Next(length: _built, speed: 2 * _piston.MaxSpeed, position: 1, force: pushing).ShouldBeLessThan(atSpeed / 1000);
        Next(length: _built, speed: 0, position: 1, force: pushing).ShouldBeGreaterThan(pushing);

        // Past the end of its stroke it pulls back with at most the chosen share: the end stops are not powered.
        Next(length: 150, speed: 0, position: 1, force: -_piston.Strength, strength: 0.2).ShouldBe(-OutputSignals.StrengthFromOutput(0.2, _piston.Strength));
        Next(length: 150, speed: 0, position: 1, force: 0, strength: 0.2).ShouldBeLessThan(0);
        Next(length: 50, speed: 0, position: 0, force: 0, strength: 0.2).ShouldBeGreaterThan(0);
    }

    // Light and heavy pairs at the slider ends where its force can brake in time. A light pair at
    // the shortest Rise time and a low Max speed still swings around its target; the user's
    // settings, the network and fitness handle that, not the force.
    [Theory]
    [InlineData(2, 1, 0.2, 200)]
    [InlineData(0.3, 1, 0.2, 200)]
    [InlineData(0.3, 0.1, 0.2, 200)]
    [InlineData(0.3, 1, 1, 100)]
    [InlineData(0.3, 1, 0.1, 400)]
    [InlineData(2, 1, 0.1, 50)]
    [InlineData(2, 1, 1, 400)]
    public void OnAFreePair_ItBrakesBeforeItsTarget_SettlesThere_AndItsForceDiesAway(double pairMass, double strength, double riseTime, double maxSpeed)
    {
        var piston = _piston.WithSettings(_piston.Strength, _piston.Stroke, _piston.Start, maxSpeed, riseTime);

        var forces = Run(pairMass, strength, load: 0, steps: 600, out var length, out var topSpeed, piston: piston);

        StillPushesAtTheEnd(forces, OutputSignals.StrengthFromOutput(strength, piston.Strength)).ShouldBeFalse();
        topSpeed.ShouldBeLessThanOrEqualTo(maxSpeed * 1.35);
        length.ShouldBe(Piston.LongestLength(piston, _built, _pointJoints), tolerance: 0.5);
    }

    [Fact]
    public void UnderASteadyLoad_ItSagsUntilItsForceCarriesTheLoad()
    {
        // A quarter of its Strength pulls the nodes together, like weight on a leg. It holds a
        // little short of its target: its force dies away as it reaches the speed it wants, so it
        // needs to lack some speed, and so some distance, to keep carrying the load.
        var forces = Run(pairMass: 2, strength: 1, load: -_piston.Strength / 4, steps: 600, out var length, out _);

        var target = Piston.LongestLength(_piston, _built, _pointJoints);
        length.ShouldBeInRange(target - 6, target - 1);
        forces.TakeLast(60).Average().ShouldBe(_piston.Strength / 4, tolerance: _piston.Strength * 0.01);
    }

    [Fact]
    public void AfterAnEndStopHeldItAgainstMoreThanItsStrength_ItSettlesBackCalmly()
    {
        // The force alone, against an idealised stop: the real end stops are a Godot joint
        // (Creature.CreateEndStops), measured in the sim, not here.
        var forces = Run(pairMass: 2, strength: 1, load: 2 * _piston.Strength, steps: 300, out var length, out _, position: 0.5, loadSteps: 120, endStops: true);

        StillPushesAtTheEnd(forces, _piston.Strength).ShouldBeFalse();
        length.ShouldBe(_built, tolerance: 0.5);
    }

    // Whether it still pushes with more than 1% of its target force over its last second: a
    // settled piston with nothing loading it pushes with next to nothing, a shaking one does not.
    private static bool StillPushesAtTheEnd(IReadOnlyList<double> forces, double targetForce) =>
        forces.TakeLast(60).Any(force => Math.Abs(force) > targetForce * 0.01);

    // Two free nodes and the Piston between them, stepped like Godot: speed first, then length.
    // The load pushes them apart (negative: together) for the first loadSteps; endStops models the
    // cylinder's hard ends. Returns its force each step.
    private static List<double> Run(
        double pairMass,
        double strength,
        double load,
        int steps,
        out double length,
        out double topSpeed,
        double position = 1,
        int loadSteps = int.MaxValue,
        bool endStops = false,
        PistonDef? piston = null)
    {
        piston ??= _piston;
        var shortest = Piston.ShortestLength(piston, _built, _pointJoints);
        var longest = Piston.LongestLength(piston, _built, _pointJoints);
        length = _built;
        var speed = 0.0;
        var force = 0.0;
        var forces = new List<double>();
        topSpeed = 0.0;
        for (var i = 0; i < steps; i++)
        {
            force = Piston.NextForce(piston, _built, _pointJoints, length, speed, position, strength, force, _step);
            speed += (force + (i < loadSteps ? load : 0)) / pairMass * _step;
            length += speed * _step;
            if (endStops && (length < shortest || length > longest))
            {
                length = Math.Clamp(length, shortest, longest);
                speed = 0;
            }
            forces.Add(force);
            topSpeed = Math.Max(topSpeed, Math.Abs(speed));
        }

        return forces;
    }

    // Its force step by step while something holds it still at its built length, wanting it fully out.
    private static List<double> Hold(double strength, double riseTime)
    {
        var piston = _piston.WithSettings(_piston.Strength, _piston.Stroke, _piston.Start, _piston.MaxSpeed, riseTime);
        var force = 0.0;
        var forces = new List<double>();
        for (var i = 0; i < (int)(2 * riseTime / _step); i++)
        {
            force = Piston.NextForce(piston, _built, _pointJoints, _built, 0, 1, strength, force, _step);
            forces.Add(force);
        }

        return forces;
    }

    private static double Next(double length, double speed, double position, double force, double strength = 1) =>
        Piston.NextForce(_piston, _built, _pointJoints, length, speed, position, strength, force, _step);
}
