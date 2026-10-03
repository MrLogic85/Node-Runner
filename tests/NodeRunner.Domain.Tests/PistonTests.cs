namespace NodeRunner.Domain.Tests;

public sealed class PistonTests
{
    private const double _built = 100;

    private static readonly PistonDef _piston = new(1, 2, 3);

    [Fact]
    public void LengthInput_IsZeroAsBuilt_AndOneAtEitherEndOfTheStroke()
    {
        Piston.LengthInput(_built, _built, 0.3).ShouldBe(0);
        Piston.LengthInput(130, _built, 0.3).ShouldBe(1, tolerance: 1e-12);
        Piston.LengthInput(70, _built, 0.3).ShouldBe(-1, tolerance: 1e-12);
    }

    [Fact]
    public void SpeedInput_IsPositiveWhileExtending_AndSaturatesSoftly()
    {
        Piston.SpeedInput(0, 200).ShouldBe(0);
        Piston.SpeedInput(200, 200).ShouldBe(Math.Tanh(1), tolerance: 1e-12);
        Piston.SpeedInput(-20000, 200).ShouldBe(-1, tolerance: 1e-9);
    }

    [Fact]
    public void TargetLength_MapsThePositionOutputOntoTheStroke()
    {
        Piston.TargetLength(-1, _built, 0.3).ShouldBe(70, tolerance: 1e-9);
        Piston.TargetLength(0, _built, 0.3).ShouldBe(_built, tolerance: 1e-9);
        Piston.TargetLength(1, _built, 0.3).ShouldBe(130, tolerance: 1e-9);
    }

    [Fact]
    public void Step_AtItsTarget_AndStill_PushesNothing() =>
        Step(length: _built, speed: 0, position: 0).Force.ShouldBe(0);

    [Fact]
    public void Step_PushesTowardsALongerTarget_AndPullsTowardsAShorterOne()
    {
        Step(length: _built, speed: 0, position: 1).Force.ShouldBeGreaterThan(0);
        Step(length: _built, speed: 0, position: -1).Force.ShouldBeLessThan(0);
    }

    [Fact]
    public void Step_NeverUsesMoreThanTheChosenShareOfItsStrength()
    {
        Step(length: _built, speed: -1000, position: 1, strength: 0, pairMass: 100).Force
            .ShouldBe(PortSignals.StrengthFromOutput(0, _piston.Strength), tolerance: 1e-9);
        Step(length: _built, speed: -1000, position: 1, strength: 1, pairMass: 100).Force.ShouldBe(_piston.Strength);
    }

    [Fact]
    public void Step_PastTheEndOfItsStroke_StillUsesOnlyTheChosenShare_TheEndStopsAreNotPowered()
    {
        var share = PortSignals.StrengthFromOutput(0, _piston.Strength);
        Step(length: 150, speed: 1000, position: 1, strength: 0, pairMass: 100).Force.ShouldBe(-share, tolerance: 1e-9);
        Step(length: 50, speed: -1000, position: -1, strength: 0, pairMass: 100).Force.ShouldBe(share, tolerance: 1e-9);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(-2)]
    public void AfterAnEndStopHeldItAgainstMoreThanItsStrength_ItSettlesBackCalmly(double loadInStrengths)
    {
        // The controller alone, against an idealised stop: the real end stops (#701) are a Godot
        // joint (Creature.CreateEndStops), measured in the sim, not here. Once the load lets go it
        // must return to its target without flipping its force every step.
        var forces = Run(pairMass: 0.3, load: loadInStrengths * _piston.Strength, steps: 300, out var length, out _, position: 0, loadSteps: 120, endStops: true);

        forces.Take(120).ShouldAllBe(force => Math.Abs(force) <= _piston.Strength);
        var released = forces.Skip(120).ToList();
        released.Zip(released.Skip(1)).Count(pair => pair.First * pair.Second < 0).ShouldBeLessThanOrEqualTo(3);
        length.ShouldBe(_built, tolerance: 0.5);
    }

    [Theory]
    [InlineData(0.05)]
    [InlineData(0.3)]
    [InlineData(1.8)]
    public void OnAFreePair_ItSettlesAtItsTarget_WithoutFlippingItsForceEveryStep(double pairMass)
    {
        var forces = Run(pairMass, load: 0, steps: 120, out var length, out var topSpeed);

        // It pushes out, then brakes: its force changes sign a few times while settling, not every step.
        forces.Zip(forces.Skip(1)).Count(pair => pair.First * pair.Second < 0).ShouldBeLessThanOrEqualTo(3);
        topSpeed.ShouldBeLessThanOrEqualTo(_piston.MaxSpeed * 1.25);
        length.ShouldBe(Piston.TargetLength(1, _built, _piston.Stroke), tolerance: 0.5);
    }

    [Fact]
    public void UnderASteadyLoad_ItHoldsItsTarget_InsteadOfSagging()
    {
        // Half its Strength pulls the nodes together, like weight on a leg.
        Run(pairMass: 0.3, load: -_piston.Strength / 2, steps: 300, out var length, out _);

        length.ShouldBe(Piston.TargetLength(1, _built, _piston.Stroke), tolerance: 0.5);
    }

    // Two free nodes and the Piston between them, stepped like Godot: speed first, then length.
    // The load pushes them apart (negative: together) for the first loadSteps; endStops models the
    // cylinder's hard ends. Returns its force each step.
    private static List<double> Run(
        double pairMass,
        double load,
        int steps,
        out double length,
        out double topSpeed,
        double position = 1,
        int loadSteps = int.MaxValue,
        bool endStops = false)
    {
        var shortest = Piston.ShortestLength(_built, _piston.Stroke);
        var longest = Piston.LongestLength(_built, _piston.Stroke);
        const double step = 1.0 / 60;
        length = _built;
        var speed = 0.0;
        var control = default(PistonControl);
        var forces = new List<double>();
        topSpeed = 0.0;
        for (var i = 0; i < steps; i++)
        {
            control = Piston.Step(_piston, _built, length, speed, position, strength: 1, pairMass, step, control);
            speed += (control.Force + (i < loadSteps ? load : 0)) / pairMass * step;
            length += speed * step;
            if (endStops && (length < shortest || length > longest))
            {
                length = Math.Clamp(length, shortest, longest);
                speed = 0;
            }
            forces.Add(control.Force);
            topSpeed = Math.Max(topSpeed, Math.Abs(speed));
        }

        return forces;
    }

    private static PistonControl Step(double length, double speed, double position, double strength = 1, double pairMass = 0.3) =>
        Piston.Step(_piston, _built, length, speed, position, strength, pairMass, 1.0 / 60, default);
}
