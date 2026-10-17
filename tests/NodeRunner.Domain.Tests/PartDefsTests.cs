namespace NodeRunner.Domain.Tests;

/// <summary>The part records: what each refuses, the settings a new one gets and its With helpers.</summary>
public sealed class PartDefsTests
{
    // One row per guard condition: a NaN and a value past a bound take different branches.
    [Fact]
    public void Constructors_RefuseIdsAndSettingsOutOfRange()
    {
        Func<int, int, int, IPartDef>[] links = [(id, a, b) => new BeamDef(id, a, b), (id, a, b) => new PistonDef(id, a, b), (id, a, b) => new SpringDef(id, a, b)];
        foreach (var link in links)
        {
            Refuses.OutOfRange(() => link(0, 1, 2));
            Refuses.OutOfRange(() => link(5, 0, 2));
            Refuses.OutOfRange(() => link(5, 1, -1));
            Refuses.Invalid(() => link(5, 1, 1));
        }

        Func<int, int, IPartDef>[] jointParts = [(id, node) => new WheelDef(id, node), (id, node) => new TouchSensorDef(id, node), (id, node) => new RubberDef(id, node)];
        foreach (var jointPart in jointParts)
        {
            Refuses.OutOfRange(() => jointPart(0, 1));
            Refuses.OutOfRange(() => jointPart(6, 0));
        }

        Func<int, int, int?, int?, IHingePartDef>[] hingeParts =
        [
            (id, node, fixedLink, targetLink) => new ServoDef(id, node, fixedLink, targetLink),
            (id, node, fixedLink, targetLink) => new VelocityMotorDef(id, node, fixedLink, targetLink),
            (id, node, fixedLink, targetLink) => new StepperDef(id, node, fixedLink, targetLink),
            (id, node, fixedLink, targetLink) => new BrakeDef(id, node, fixedLink, targetLink),
            (id, node, fixedLink, targetLink) => new AngleSensorDef(id, node, fixedLink, targetLink),
        ];
        foreach (var hingePart in hingeParts)
        {
            Refuses.OutOfRange(() => hingePart(0, 1, null, null));
            Refuses.OutOfRange(() => hingePart(6, 0, null, null));
            Refuses.OutOfRange(() => hingePart(6, 1, 0, null));
            Refuses.OutOfRange(() => hingePart(6, 1, null, -4));
            Refuses.Invalid(() => hingePart(6, 1, 4, 4));
        }

        Refuses.OutOfRange(() => new Vector2D(double.NaN, 0));
        Refuses.OutOfRange(() => new Vector2D(0, double.PositiveInfinity));
        Refuses.OutOfRange(() => new NodeDef(0, new Vector2D(0, 0)));
        Refuses.OutOfRange(() => new CreatureElementSelection(CreatureElementKind.Beam, -1));

        Refuses.OutOfRange(() => new SensorDef(0, 2, SensorKind.Camera));
        Refuses.OutOfRange(() => new SensorDef(3, -1, SensorKind.Accelerometer));
        Refuses.OutOfRange(() => new SensorDef(3, 2, (SensorKind)99));
        Refuses.OutOfRange(() => new SensorDef(3, 2, SensorKind.Accelerometer, aim: 0.5));
        Refuses.OutOfRange(() => new SensorDef(3, 2, SensorKind.Camera, aim: double.NaN));
        Refuses.OutOfRange(() => new SensorDef(3, 2, SensorKind.Accelerometer, rays: 3));
        Refuses.OutOfRange(() => new SensorDef(3, 2, SensorKind.Camera, rays: 0));
        Refuses.OutOfRange(() => new SensorDef(3, 2, SensorKind.Camera, rays: 2));
        Refuses.OutOfRange(() => new SensorDef(3, 2, SensorKind.Camera, rays: 7));
        Refuses.OutOfRange(() => new SensorDef(3, 2, SensorKind.Accelerometer, spread: 1));
        Refuses.OutOfRange(() => new SensorDef(3, 2, SensorKind.Camera, spread: double.NaN));
        Refuses.OutOfRange(() => new SensorDef(3, 2, SensorKind.Camera, spread: -0.1));
        Refuses.OutOfRange(() => new SensorDef(3, 2, SensorKind.Camera, spread: 4));
        Refuses.OutOfRange(() => new SensorDef(3, 2, SensorKind.Accelerometer, range: 100));
        Refuses.OutOfRange(() => new SensorDef(3, 2, SensorKind.Camera, range: double.PositiveInfinity));
        Refuses.OutOfRange(() => new SensorDef(3, 2, SensorKind.Camera, range: 0));

        Refuses.OutOfRange(() => new PistonDef(5, 1, 2, strength: 0));
        Refuses.OutOfRange(() => new PistonDef(5, 1, 2, strength: double.NaN));
        Refuses.OutOfRange(() => new PistonDef(5, 1, 2, stroke: 0));
        Refuses.OutOfRange(() => new PistonDef(5, 1, 2, stroke: 2.01));
        Refuses.OutOfRange(() => new PistonDef(5, 1, 2, stroke: double.NaN));
        Refuses.OutOfRange(() => new PistonDef(5, 1, 2, start: -0.01));
        Refuses.OutOfRange(() => new PistonDef(5, 1, 2, start: 1.01));
        Refuses.OutOfRange(() => new PistonDef(5, 1, 2, start: double.NaN));
        Refuses.OutOfRange(() => new PistonDef(5, 1, 2, maxSpeed: 0));
        Refuses.OutOfRange(() => new PistonDef(5, 1, 2, maxSpeed: double.PositiveInfinity));
        Refuses.OutOfRange(() => new PistonDef(5, 1, 2, riseTime: 0));
        Refuses.OutOfRange(() => new PistonDef(5, 1, 2, riseTime: double.NaN));

        Refuses.OutOfRange(() => new SpringDef(5, 1, 2, stiffness: 0));
        Refuses.OutOfRange(() => new SpringDef(5, 1, 2, stiffness: double.NaN));
        Refuses.OutOfRange(() => new SpringDef(5, 1, 2, damping: -0.01));
        Refuses.OutOfRange(() => new SpringDef(5, 1, 2, damping: double.NaN));
        Refuses.OutOfRange(() => new SpringDef(5, 1, 2, stroke: 0));
        Refuses.OutOfRange(() => new SpringDef(5, 1, 2, stroke: 2.01));
        Refuses.OutOfRange(() => new SpringDef(5, 1, 2, stroke: double.NaN));
        Refuses.OutOfRange(() => new SpringDef(5, 1, 2, coilLength: -0.01));
        Refuses.OutOfRange(() => new SpringDef(5, 1, 2, coilLength: 1.01));
        Refuses.OutOfRange(() => new SpringDef(5, 1, 2, coilLength: double.NaN));

        Refuses.OutOfRange(() => new WheelDef(6, 1, radius: 39.9));
        Refuses.OutOfRange(() => new WheelDef(6, 1, radius: 100.1));
        Refuses.OutOfRange(() => new WheelDef(6, 1, radius: double.NaN));

        Refuses.OutOfRange(() => new ServoDef(1, 1, strength: 0));
        Refuses.OutOfRange(() => new ServoDef(1, 1, strength: double.NaN));
        Refuses.OutOfRange(() => new ServoDef(1, 1, range: 19 * Math.PI / 180));
        Refuses.OutOfRange(() => new ServoDef(1, 1, range: Math.Tau + 0.01));
        Refuses.OutOfRange(() => new ServoDef(1, 1, range: double.NaN));
        Refuses.OutOfRange(() => new ServoDef(1, 1, start: -0.1));
        Refuses.OutOfRange(() => new ServoDef(1, 1, start: 1.1));
        Refuses.OutOfRange(() => new ServoDef(1, 1, start: double.NaN));
        Refuses.OutOfRange(() => new ServoDef(1, 1, maxSpeed: 0));
        Refuses.OutOfRange(() => new ServoDef(1, 1, maxSpeed: double.NaN));
        Refuses.OutOfRange(() => new ServoDef(1, 1, riseTime: 0));
        Refuses.OutOfRange(() => new ServoDef(1, 1, riseTime: double.NaN));
        Refuses.OutOfRange(() => new VelocityMotorDef(1, 1, strength: 0));
        Refuses.OutOfRange(() => new VelocityMotorDef(1, 1, strength: double.NaN));
        Refuses.OutOfRange(() => new VelocityMotorDef(1, 1, maxSpeed: 0));
        Refuses.OutOfRange(() => new VelocityMotorDef(1, 1, maxSpeed: double.NaN));
        Refuses.OutOfRange(() => new VelocityMotorDef(1, 1, riseTime: 0));
        Refuses.OutOfRange(() => new VelocityMotorDef(1, 1, riseTime: double.NaN));
        Refuses.OutOfRange(() => new StepperDef(6, 1, strength: 0));
        Refuses.OutOfRange(() => new StepperDef(6, 1, strength: double.NaN));
        Refuses.OutOfRange(() => new StepperDef(6, 1, stepsPerTurn: StepperDef.MinStepsPerTurn - 1));
        Refuses.OutOfRange(() => new StepperDef(6, 1, stepsPerTurn: StepperDef.MaxStepsPerTurn + 1));
        Refuses.OutOfRange(() => new StepperDef(6, 1, maxFrequency: 0));
        Refuses.OutOfRange(() => new StepperDef(6, 1, maxFrequency: double.PositiveInfinity));
        Refuses.OutOfRange(() => new BrakeDef(6, 1, strength: 0));
        Refuses.OutOfRange(() => new BrakeDef(6, 1, strength: double.NaN));
    }

    [Fact]
    public void Constructors_AcceptTheEndsOfEachRange()
    {
        Should.NotThrow(() => new PistonDef(5, 1, 2, stroke: PistonDef.MaxStroke, start: 0));
        Should.NotThrow(() => new PistonDef(5, 1, 2, stroke: 0.1, start: 1));
        Should.NotThrow(() => new SpringDef(5, 1, 2, damping: 0, stroke: SpringDef.MaxStroke, coilLength: 0));
        Should.NotThrow(() => new SpringDef(5, 1, 2, stroke: 0.1, coilLength: 1));
        Should.NotThrow(() => new WheelDef(6, 1, radius: WheelDef.MaxRadius));
        Should.NotThrow(() => new ServoDef(1, 1, range: Math.Tau, start: 0));
        Should.NotThrow(() => new ServoDef(1, 1, range: 20 * Math.PI / 180, start: 1));
        Should.NotThrow(() => new StepperDef(6, 1, stepsPerTurn: StepperDef.MinStepsPerTurn));
        Should.NotThrow(() => new StepperDef(6, 1, stepsPerTurn: StepperDef.MaxStepsPerTurn));
        Should.NotThrow(() => new SensorDef(3, 2, SensorKind.Camera, rays: 1, spread: Math.PI));
        Should.NotThrow(() => new SensorDef(3, 2, SensorKind.Camera, rays: SensorDef.MaxRays, spread: 0));
    }

    // Generation 0 runs a Stepper at half its Max frequency, so 2 Hz starts it at a step a second.
    [Fact]
    public void NewParts_GetTheDesignedSettings()
    {
        new WheelDef(6, 1).Radius.ShouldBe(WheelDef.MinRadius);
        new BrakeDef(6, 1).Strength.ShouldBe(200 * 10000);
        new BrakeDef(6, 1).Locked.ShouldBeFalse();
        new BrakeDef(6, 1, strength: BrakeDef.LockedStrength).Locked.ShouldBeTrue();
        var stepper = new StepperDef(6, 1);
        (stepper.Strength, stepper.StepsPerTurn, stepper.MaxFrequency).ShouldBe((1500000, 8, 2));
        var motor = new VelocityMotorDef(1, 1);
        (motor.Strength, motor.MaxSpeed, motor.RiseTime).ShouldBe((400000, ServoDef.DefaultMaxSpeed, ServoDef.DefaultRiseTime));
        var camera = new SensorDef(3, 2, SensorKind.Camera);
        (camera.Rays, camera.Spread, camera.Range).ShouldBe((3, Math.PI / 2, 220));
        var accelerometer = new SensorDef(3, 2, SensorKind.Accelerometer);
        (accelerometer.Rays, accelerometer.Spread, accelerometer.Range).ShouldBe((null, null, null));
    }

    // Each helper is applied once to a part with every value set, so a helper that drops or changes another value fails.
    [Fact]
    public void WithHelpers_ChangeOnlyWhatTheyName()
    {
        var piston = new PistonDef(5, 1, 2, "Ram", 30000, 0.4, 0.3, 80, 0.4);
        piston.WithSettings(20000, 0.7, 0.2, 100, 0.5).ShouldBe(new PistonDef(5, 1, 2, "Ram", 20000, 0.7, 0.2, 100, 0.5));
        piston.WithName("Kick").ShouldBe(new PistonDef(5, 1, 2, "Kick", 30000, 0.4, 0.3, 80, 0.4));
        var spring = new SpringDef(5, 1, 2, "Tail", 600, 3, 0.8, 0.2);
        spring.WithSettings(800, 0.5, 0.4, 0.9).ShouldBe(new SpringDef(5, 1, 2, "Tail", 800, 0.5, 0.4, 0.9));
        spring.WithName("Knee").ShouldBe(new SpringDef(5, 1, 2, "Knee", 600, 3, 0.8, 0.2));
        var servo = new ServoDef(6, 1, 4, 5, "Elbow", 2000, Math.PI, 0.75, 2, 0.3);
        servo.WithSettings(1000, Math.PI / 2, 0.25, 3, 0.5).ShouldBe(new ServoDef(6, 1, 4, 5, "Elbow", 1000, Math.PI / 2, 0.25, 3, 0.5));
        servo.WithLinks(5, null).ShouldBe(new ServoDef(6, 1, 5, null, "Elbow", 2000, Math.PI, 0.75, 2, 0.3));
        servo.WithName("Hip").ShouldBe(new ServoDef(6, 1, 4, 5, "Hip", 2000, Math.PI, 0.75, 2, 0.3));
        var motor = new VelocityMotorDef(6, 1, 4, 5, "Drive", 2000, 2, 0.3);
        motor.WithSettings(1000, 3, 0.5).ShouldBe(new VelocityMotorDef(6, 1, 4, 5, "Drive", 1000, 3, 0.5));
        motor.WithLinks(5, null).ShouldBe(new VelocityMotorDef(6, 1, 5, null, "Drive", 2000, 2, 0.3));
        motor.WithName("Spin").ShouldBe(new VelocityMotorDef(6, 1, 4, 5, "Spin", 2000, 2, 0.3));
        var stepper = new StepperDef(6, 1, 4, 5, "Ratchet", 200000, 6, 1);
        stepper.WithSettings(100000, 4, 3).ShouldBe(new StepperDef(6, 1, 4, 5, "Ratchet", 100000, 4, 3));
        stepper.WithLinks(5, null).ShouldBe(new StepperDef(6, 1, 5, null, "Ratchet", 200000, 6, 1));
        stepper.WithName("Clock").ShouldBe(new StepperDef(6, 1, 4, 5, "Clock", 200000, 6, 1));
        var brake = new BrakeDef(6, 1, 4, 5, "Grip", 1000);
        brake.WithStrength(BrakeDef.LockedStrength).ShouldBe(new BrakeDef(6, 1, 4, 5, "Grip", BrakeDef.LockedStrength));
        brake.WithLinks(5, null).ShouldBe(new BrakeDef(6, 1, 5, null, "Grip", 1000));
        brake.WithName("Hold").ShouldBe(new BrakeDef(6, 1, 4, 5, "Hold", 1000));
        var angle = new AngleSensorDef(6, 1, 4, 5, "Knee");
        angle.WithLinks(5, null).ShouldBe(new AngleSensorDef(6, 1, 5, null, "Knee"));
        angle.WithName("Hip").ShouldBe(new AngleSensorDef(6, 1, 4, 5, "Hip"));
        var wheel = new WheelDef(6, 1, "Front", 60);
        wheel.WithSettings(100).ShouldBe(new WheelDef(6, 1, "Front", 100));
        wheel.WithName("Back").ShouldBe(new WheelDef(6, 1, "Back", 60));
        new TouchSensorDef(6, 1, "Toe").WithName("Foot").ShouldBe(new TouchSensorDef(6, 1, "Foot"));
        new RubberDef(6, 1, "Pad").WithName("Sole").ShouldBe(new RubberDef(6, 1, "Sole"));
        var eye = new SensorDef(3, 2, SensorKind.Camera, "Eye", 0.5, 5, 1, 300);
        eye.OnBeam(7, 0.2).ShouldBe(new SensorDef(3, 7, SensorKind.Camera, "Eye", 0.2, 5, 1, 300));
        eye.WithName("Look").ShouldBe(new SensorDef(3, 2, SensorKind.Camera, "Look", 0.5, 5, 1, 300));
        eye.WithAim(-1).ShouldBe(new SensorDef(3, 2, SensorKind.Camera, "Eye", -1, 5, 1, 300));
        eye.WithRays(1).ShouldBe(new SensorDef(3, 2, SensorKind.Camera, "Eye", 0.5, 1, 1, 300));
        eye.WithSpread(0.3).ShouldBe(new SensorDef(3, 2, SensorKind.Camera, "Eye", 0.5, 5, 0.3, 300));
        eye.WithRange(150).ShouldBe(new SensorDef(3, 2, SensorKind.Camera, "Eye", 0.5, 5, 1, 150));
    }

    [Fact]
    public void DefaultAim_UndoesTheBeamsTurn_WithinAHalfTurn()
    {
        SensorDef.DefaultAim(new Vector2D(3, 4), new Vector2D(13, 4)).ShouldBe(0, 1e-12);
        SensorDef.DefaultAim(new Vector2D(3, 4), new Vector2D(3, 14)).ShouldBe(-Math.PI / 2, 1e-12);
        SensorDef.DefaultAim(new Vector2D(3, 4), new Vector2D(13, 14)).ShouldBe(-Math.PI / 4, 1e-12);
        Math.Abs(SensorDef.DefaultAim(new Vector2D(0, 0), new Vector2D(-10, 0))).ShouldBe(Math.PI, 1e-12);
        SensorDef.DefaultAim(new Vector2D(1, 1), new Vector2D(1, 1)).ShouldBe(0);
    }
}
