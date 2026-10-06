using NodeRunner.Domain;

namespace NodeRunner.Mechanics.Tests;

public sealed class PistonTests
{
    private const double _built = 100;

    // Joints with no size, so the gap between their edges is the drawn length.
    private const double _pointJoints = 0;

    private const double _step = 1.0 / 60;

    private static readonly PistonDef _piston = new(1, 2, 3);

    // The table in #870: drawn 1 m, Stroke 100%.
    [Theory]
    [InlineData(0, 100, 200)]
    [InlineData(0.5, 200.0 / 3, 400.0 / 3)]
    [InlineData(1, 50, 100)]
    public void ShortestAndLongest_GrowFromTheShortestByTheStroke_WithTheDrawnLengthAtItsStart(double start, double shortest, double longest)
    {
        var piston = new PistonDef(1, 2, 3, stroke: 1, start: start);

        Piston.ShortestLength(piston, _built, _pointJoints).ShouldBe(shortest, tolerance: 1e-9);
        Piston.LongestLength(piston, _built, _pointJoints).ShouldBe(longest, tolerance: 1e-9);
    }

    // Drawn 1 m between joints of radius 20, so a gap of 60 between their edges that the stroke grows.
    [Theory]
    [InlineData(0, 100, 160)]
    [InlineData(1, 70, 100)]
    public void ShortestAndLongest_GrowTheGapBetweenItsJointsEdges(double start, double shortest, double longest)
    {
        var piston = new PistonDef(1, 2, 3, stroke: 1, start: start);

        Piston.ShortestLength(piston, _built, 40).ShouldBe(shortest, tolerance: 1e-9);
        Piston.LongestLength(piston, _built, 40).ShouldBe(longest, tolerance: 1e-9);
    }

    [Fact]
    public void AServosBiggerJoint_ShortensItsTravel()
    {
        var plain = 2 * NodeDef.PlainJointRadius;
        var withServo = NodeDef.PlainJointRadius + ServoDef.JointRadius;

        var plainTravel = Piston.LongestLength(_piston, _built, plain) - Piston.ShortestLength(_piston, _built, plain);
        var servoTravel = Piston.LongestLength(_piston, _built, withServo) - Piston.ShortestLength(_piston, _built, withServo);

        servoTravel.ShouldBeLessThan(plainTravel);
    }

    [Fact]
    public void WithItsJointsTouching_ItCannotMove()
    {
        Piston.ShortestLength(_piston, _built, 120).ShouldBe(_built);
        Piston.LongestLength(_piston, _built, 120).ShouldBe(_built);
    }

    // So a cylinder from joint A's edge as long as its travel never reaches past joint B's edge.
    [Theory]
    [InlineData(0.1, 0)]
    [InlineData(1, 0)]
    [InlineData(1, 1)]
    [InlineData(0.5, 0.5)]
    public void AtItsShortest_TheGapBetweenItsJointsEdges_IsAtLeastItsTravel(double stroke, double start)
    {
        var piston = new PistonDef(1, 2, 3, stroke: stroke, start: start);
        var radii = NodeDef.PlainJointRadius + ServoDef.JointRadius;

        var shortest = Piston.ShortestLength(piston, _built, radii);
        (shortest - radii).ShouldBeGreaterThanOrEqualTo(Piston.LongestLength(piston, _built, radii) - shortest - 1e-9);
    }

    [Fact]
    public void ShortestAndLongest_ForANewPiston_SpanEightyToOneHundredAndTwentyPercent()
    {
        Piston.ShortestLength(_piston, _built, _pointJoints).ShouldBe(80, tolerance: 1e-9);
        Piston.LongestLength(_piston, _built, _pointJoints).ShouldBe(120, tolerance: 1e-9);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0.25)]
    [InlineData(1)]
    public void LengthInput_IsZeroAtTheShortest_OneAtTheLongest_AndItsStartAsDrawn(double start)
    {
        var piston = new PistonDef(1, 2, 3, stroke: 0.6, start: start);

        Piston.LengthInput(piston, _built, _pointJoints, Piston.ShortestLength(piston, _built, _pointJoints)).ShouldBe(0, tolerance: 1e-12);
        Piston.LengthInput(piston, _built, _pointJoints, Piston.LongestLength(piston, _built, _pointJoints)).ShouldBe(1, tolerance: 1e-12);
        Piston.LengthInput(piston, _built, _pointJoints, _built).ShouldBe(start, tolerance: 1e-12);
        Piston.LengthInput(piston, _built, 40, Piston.ShortestLength(piston, _built, 40)).ShouldBe(0, tolerance: 1e-12);
        Piston.LengthInput(piston, _built, 40, Piston.LongestLength(piston, _built, 40)).ShouldBe(1, tolerance: 1e-12);
        Piston.LengthInput(piston, _built, 40, _built).ShouldBe(start, tolerance: 1e-12);
    }

    [Fact]
    public void SpeedInput_IsPositiveWhileExtending_AndSaturatesSoftly()
    {
        Piston.SpeedInput(0, 200).ShouldBe(0);
        Piston.SpeedInput(200, 200).ShouldBe(Math.Tanh(1), tolerance: 1e-12);
        Piston.SpeedInput(-20000, 200).ShouldBe(-1, tolerance: 1e-9);
    }

    [Fact]
    public void TargetLength_MapsThePositionOutputStraightOntoItsTravel()
    {
        var piston = new PistonDef(1, 2, 3, stroke: 1, start: 0);

        Piston.TargetLength(piston, _built, _pointJoints, -1).ShouldBe(100, tolerance: 1e-9);
        Piston.TargetLength(piston, _built, _pointJoints, 0).ShouldBe(150, tolerance: 1e-9);
        Piston.TargetLength(piston, _built, _pointJoints, 1).ShouldBe(200, tolerance: 1e-9);
        Piston.TargetLength(piston, _built, _pointJoints, 3).ShouldBe(200, tolerance: 1e-9);
    }

    [Theory]
    [InlineData(0, -1)]
    [InlineData(0.25, -0.5)]
    [InlineData(0.5, 0)]
    [InlineData(1, 1)]
    public void DrawnPosition_AsksForItsDrawnLength(double start, double position)
    {
        var piston = new PistonDef(1, 2, 3, stroke: 0.4, start: start);

        Piston.DrawnPosition(piston).ShouldBe(position, tolerance: 1e-12);
        Piston.TargetLength(piston, _built, _pointJoints, Piston.DrawnPosition(piston)).ShouldBe(_built, tolerance: 1e-9);
    }

    [Fact]
    public void DrawnPositions_ListsEachPistonsDrawnPosition_ById()
    {
        var creature = new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(100, 0)), new NodeDef(3, new Vector2D(0, 100))],
            [],
            [],
            [new PistonDef(4, 1, 2, start: 0), new PistonDef(5, 1, 3, start: 1)],
            nextPartId: 6);

        Piston.DrawnPositions(creature).ShouldBe(new Dictionary<int, double> { [4] = -1, [5] = 1 });
    }

    [Fact]
    public void NextForce_AtItsTarget_AndStill_FromRest_IsZero() =>
        Next(length: _built, speed: 0, position: 0, force: 0).ShouldBe(0);

    [Fact]
    public void NextForce_FromRest_BuildsTowardsItsTarget_AtItsTargetForceOverItsRiseTime()
    {
        var push = Next(length: _built, speed: 0, position: 1, force: 0);
        var pull = Next(length: _built, speed: 0, position: -1, force: 0);

        // A whole stroke away, at rest: tanh(3) of the rate, through a gate that is nearly open.
        var rate = _piston.Strength * _step / _piston.RiseTime;
        push.ShouldBe(rate * Math.Tanh(3) / (1 + Math.Exp(-10)), tolerance: 1e-9);
        pull.ShouldBe(-push, tolerance: 1e-9);
    }

    [Fact]
    public void NextForce_HeldStill_FarFromItsTarget_ReachesItsWholeShareWithinAboutItsRiseTime_AndNoMore()
    {
        var forces = Hold(strength: 1, riseTime: _piston.RiseTime);

        forces[(int)(0.9 * _piston.RiseTime / _step)].ShouldBeLessThan(_piston.Strength);
        forces[(int)(1.1 * _piston.RiseTime / _step)].ShouldBe(_piston.Strength);
        forces.ShouldAllBe(force => force <= _piston.Strength);
        Hold(strength: 0.2, riseTime: _piston.RiseTime).Max().ShouldBe(OutputSignals.StrengthFromOutput(0.2, _piston.Strength));
    }

    [Fact]
    public void NextForce_ALongerRiseTime_BuildsUpMoreSlowly()
    {
        var quick = Hold(strength: 1, riseTime: 0.1);
        var slow = Hold(strength: 1, riseTime: 0.5);

        slow[3].ShouldBe(quick[3] / 5, tolerance: 1e-6);
    }

    [Fact]
    public void NextForce_DiesAway_OnceItMovesAsFastAsItWants_AndFasterStillCutsIt()
    {
        var pushing = _piston.Strength / 2;
        var atSpeed = Next(length: _built, speed: _piston.MaxSpeed, position: 1, force: pushing);
        var tooFast = Next(length: _built, speed: 2 * _piston.MaxSpeed, position: 1, force: pushing);
        var tooSlow = Next(length: _built, speed: 0, position: 1, force: pushing);

        atSpeed.ShouldBeLessThan(pushing);
        tooFast.ShouldBeLessThan(atSpeed / 1000);
        tooSlow.ShouldBeGreaterThan(pushing);
    }

    [Fact]
    public void NextForce_WithStrengthZero_IsZero() =>
        Next(length: _built, speed: -1000, position: 1, force: 0, strength: 0).ShouldBe(0);

    [Fact]
    public void NextForce_PastTheEndOfItsStroke_PullsBackWithAtMostTheChosenShare_TheEndStopsAreNotPowered()
    {
        var share = OutputSignals.StrengthFromOutput(0.2, _piston.Strength);
        Next(length: 150, speed: 0, position: 1, force: -_piston.Strength, strength: 0.2).ShouldBe(-share);
        Next(length: 150, speed: 0, position: 1, force: 0, strength: 0.2).ShouldBeLessThan(0);
        Next(length: 50, speed: 0, position: -1, force: 0, strength: 0.2).ShouldBeGreaterThan(0);
    }

    // Light and heavy pairs at the slider ends where its force can brake in time. A light pair at
    // the shortest Rise time and a low Max speed still swings around its target; the user's
    // settings, the brain and fitness (#546) handle that, not the force.
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
        // The force alone, against an idealised stop: the real end stops (#701) are a Godot joint
        // (Creature.CreateEndStops), measured in the sim, not here.
        var forces = Run(pairMass: 2, strength: 1, load: 2 * _piston.Strength, steps: 300, out var length, out _, position: 0, loadSteps: 120, endStops: true);

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
