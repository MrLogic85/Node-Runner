using NodeRunner.App.Builders;
using NodeRunner.App.Lifecycle;
using NodeRunner.App.Repositories;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.Repositories;

/// <summary>
/// Pins what one creature holding every part kind saves as, shares as and gives the brain (#1219),
/// so a refactor of the part kinds (#1213) shows it changed none of them. The pins are the files in
/// Repositories/Pins; docs/SAVE_FORMAT.md → "Pinned saves" owns when they change.
/// </summary>
public sealed class EveryPartPinTests
{
    private static readonly CreationDef _creation = new(Guid.Parse("5b0e2f4c-8a3d-4e61-9c7b-1d2e3f405162"), "Every part", Creature());

    [Fact]
    public void Creature_HoldsEveryPartKind_AndCouldBeBuiltAndPlayed()
    {
        var creature = _creation.Creature;

        foreach (var kind in Enum.GetValues<CreatureElementKind>())
        {
            Count(creature, kind).ShouldBeGreaterThan(0, $"The pinned creature holds no {kind}: add one to Creature() and update the pins.");
        }

        foreach (var kind in Enum.GetValues<SensorKind>())
        {
            creature.Sensors.ShouldContain(sensor => sensor.Kind == kind, $"The pinned creature holds no {kind}: add one to Creature() and update the pins.");
        }

        var rayCounts = Enumerable.Range(1, SensorDef.MaxRays).Where(SensorDef.IsRayCount).Select(rays => (int?)rays);
        creature.Sensors.Where(sensor => sensor.Kind == SensorKind.Camera).Select(camera => camera.Rays).Order().ShouldBe(rayCounts);
        creature.Brakes.Select(brake => brake.Locked).ShouldBe([false, true]);
        new CreatureBuilder(creature).IsWithinBuildLimits().ShouldBeTrue();
        CreatureReadiness.Problems(creature).ShouldBeEmpty();
    }

    [Fact]
    public void CreationJson_MatchesThePin_AndLoadsBackTheSame()
    {
        var json = FileCreationRepository.Format.Serialize(_creation).ReplaceLineEndings("\n") + "\n";

        Pin.Match(json, "every-part.creation.json");
        var loaded = FileCreationRepository.Format.Deserialize(json, "every-part.creation.json").Value;
        SaveJson.Serialize(loaded).ShouldBe(SaveJson.Serialize(_creation));
    }

    [Fact]
    public void BrainPortsAndTheirLabels_MatchThePin()
    {
        var layout = BrainPorts.Of(_creation.Creature);
        var labels = BrainPortLabels.For(_creation.Creature);
        labels.Inputs.Count.ShouldBe(layout.Inputs.Count);
        labels.Outputs.Count.ShouldBe(layout.Outputs.Count);

        var lines = layout.Inputs.Zip(labels.Inputs, (port, label) => Line("in", port, label))
            .Concat(layout.Outputs.Zip(labels.Outputs, (port, label) => Line("out", port, label)));

        Pin.Match(string.Concat(lines.Select(line => line + "\n")), "every-part.ports.txt");
    }

    [Fact]
    public void ShareCode_MatchesThePin_AndReadsBackTheSameBuild()
    {
        var code = CreationShareCode.Create(_creation);

        Pin.Match(code + "\n", "every-part.share-code.txt");
        var read = CreationShareCode.Read(code);
        read.Refusal.ShouldBeNull();
        var build = read.Build.ShouldNotBeNull();
        build.Name.ShouldBe(_creation.Name);
        SaveJson.Serialize(build.Creature).ShouldBe(SaveJson.Serialize(_creation.Creature));
    }

    // One line per port, in port order: its direction, part id, channel key, signal and label
    // (UiText's debug view, so its template and arguments are pinned, not a translation).
    private static string Line(string direction, BrainPort port, UiText label) =>
        $"{direction,-3} {port.PartId,2} {port.Channel,-9} {port.Signal,-12} {label}";

    // Ids 1, 2, 3 … in the order the parts are listed, joints on whole units and settings on their
    // sliders, so the share code keeps it exactly. Every setting is off its default, so a refactor
    // that drops one and falls back to the default fails. Node 1 and Beam 8 are named; the rest get
    // their kind's default name.
    private static CreatureDef Creature() =>
        new(
            nodes:
            [
                new NodeDef(1, new Vector2D(0, 0), "Hip"),
                new NodeDef(2, new Vector2D(200, 0)),
                new NodeDef(3, new Vector2D(400, 0)),
                new NodeDef(4, new Vector2D(600, 0)),
                new NodeDef(5, new Vector2D(100, -200)),
                new NodeDef(6, new Vector2D(300, -200)),
                new NodeDef(7, new Vector2D(500, -200)),
            ],
            beams:
            [
                new BeamDef(8, 1, 2, "Thigh"),
                new BeamDef(9, 2, 3),
                new BeamDef(10, 3, 4),
                new BeamDef(11, 1, 5),
                new BeamDef(12, 5, 2),
                new BeamDef(13, 2, 6),
                new BeamDef(14, 6, 3),
            ],
            sensors:
            [
                new SensorDef(17, 8, SensorKind.Accelerometer),
                new SensorDef(18, 9, SensorKind.Camera, "Eye", aim: 0, rays: 1),
                new SensorDef(19, 10, SensorKind.Camera, aim: -0.5),
                new SensorDef(20, 13, SensorKind.Camera, aim: 0.5, rays: 5, spread: 0.5, range: 300),
            ],
            servos: [new ServoDef(21, 1, fixedLinkId: 8, targetLinkId: 11, strength: 600000, range: 2, start: 0.25, maxSpeed: 3, riseTime: 0.1)],
            pistons: [new PistonDef(15, 3, 7, strength: 20000, stroke: 1, start: 0.25, maxSpeed: 150, riseTime: 0.1)],
            springs: [new SpringDef(16, 7, 4, stiffness: 800, damping: 20, stroke: 0.5, coilLength: 0.5)],
            wheels: [new WheelDef(23, 4, radius: 60)],
            touchSensors: [new TouchSensorDef(27, 1)],
            rubbers: [new RubberDef(28, 6)],
            velocityMotors: [new VelocityMotorDef(22, 2, fixedLinkId: 8, targetLinkId: 9, strength: 300000, maxSpeed: 3.5, riseTime: 0.3)],
            brakes:
            [
                new BrakeDef(24, 4, fixedLinkId: 10, targetLinkId: 23, strength: 1500000),
                new BrakeDef(25, 5, fixedLinkId: 11, targetLinkId: 12, strength: BrakeDef.LockedStrength),
            ],
            angleSensors: [new AngleSensorDef(26, 3, fixedLinkId: 9, targetLinkId: 10)],
            steppers: [new StepperDef(29, 7, fixedLinkId: 15, targetLinkId: 16, strength: 400000, stepsPerTurn: 12, maxFrequency: 1.5)],
            nextPartId: 30);

    private static int Count(CreatureDef creature, CreatureElementKind kind) => kind switch
    {
        CreatureElementKind.Node => creature.Nodes.Count,
        CreatureElementKind.Beam => creature.Beams.Count,
        CreatureElementKind.Sensor => creature.Sensors.Count,
        CreatureElementKind.Servo => creature.Servos.Count,
        CreatureElementKind.Piston => creature.Pistons.Count,
        CreatureElementKind.Spring => creature.Springs.Count,
        CreatureElementKind.Wheel => creature.Wheels.Count,
        CreatureElementKind.TouchSensor => creature.TouchSensors.Count,
        CreatureElementKind.Rubber => creature.Rubbers.Count,
        CreatureElementKind.VelocityMotor => creature.VelocityMotors.Count,
        CreatureElementKind.Brake => creature.Brakes.Count,
        CreatureElementKind.AngleSensor => creature.AngleSensors.Count,
        CreatureElementKind.Stepper => creature.Steppers.Count,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Count has no case for this part kind: add one, then add the kind to Creature()."),
    };
}
