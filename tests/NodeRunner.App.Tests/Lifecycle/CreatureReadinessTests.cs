using NodeRunner.App.Lifecycle;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.Lifecycle;

public sealed class CreatureReadinessTests
{
    private const string _missingSide = "{0} is missing a side. Pick its Fixed and Target parts or delete it.";

    // One row per problem a drawing can have; a drawing with none trains, even if nothing moves (#845).
    [Theory]
    [InlineData("empty")]
    [InlineData("loose joint and zero-length beam")]
    [InlineData("two pieces")]
    [InlineData("zero-length piston")]
    [InlineData("servo missing a side")]
    [InlineData("brake missing a side")]
    [InlineData("servo on a one-link joint")]
    [InlineData("servo turning its joint's wheel")]
    [InlineData("single beam")]
    public void Problems_NameWhatStopsTheDrawingTraining(string drawing)
    {
        var (creature, expected) = Drawing(drawing);

        var problems = CreatureReadiness.Problems(creature);

        problems.ShouldBe(expected);
        CreatureReadiness.CanTrain(creature).ShouldBe(expected.Length == 0);
    }

    // #1212: 26 free units between what covers each end, a hub (15) or a joint motor (27), and 16 more for
    // a Piston's or Spring's telescope (#1210).
    [Theory]
    [InlineData(CreatureElementKind.Beam, 0, 55.9, false)]
    [InlineData(CreatureElementKind.Beam, 0, 56, true)]
    [InlineData(CreatureElementKind.Piston, 1, 83.9, false)]
    [InlineData(CreatureElementKind.Piston, 1, 84, true)]
    [InlineData(CreatureElementKind.Spring, 2, 95.9, false)]
    [InlineData(CreatureElementKind.Spring, 2, 96, true)]
    public void Problems_WithALinkShorterThanItsMinimum_SaysItIsTooShort(CreatureElementKind kind, int motors, double length, bool fits)
    {
        var creature = new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(0, length))],
            kind == CreatureElementKind.Beam ? [new BeamDef(101, 1, 2)] : [],
            [],
            motors >= 1 ? [new ServoDef(201, 1)] : [],
            kind == CreatureElementKind.Piston ? [new PistonDef(301, 1, 2)] : [],
            kind == CreatureElementKind.Spring ? [new SpringDef(401, 1, 2)] : [],
            [],
            [],
            [],
            motors == 2 ? [new VelocityMotorDef(501, 2)] : [],
            [],
            nextPartId: 502);
        var tooShort = UiText.Format($"The {kind.ToString().ToLowerInvariant()} between joint {{0}} and joint {{1}} is too short. Move one of the joints apart.", 1, 2);

        var problems = CreatureReadiness.Problems(creature);

        CreatureReadiness.IsTooShort(creature, kind, 1, 2).ShouldBe(!fits);
        problems.Contains(tooShort).ShouldBe(!fits);
    }

    // #1212: only a joint motor covers a link's end beyond the hub; a Wheel lies under its links (#1086).
    [Fact]
    public void IsTooShort_IgnoresWheelsBrakesTouchSensorsAndRubbers_AndOnlyLinksHaveAMinimum()
    {
        var creature = new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(56, 0)), new NodeDef(3, new Vector2D(0, 120)), new NodeDef(4, new Vector2D(56, 120))],
            [new BeamDef(5, 1, 2), new BeamDef(6, 3, 4)],
            [],
            [],
            [],
            [],
            [new WheelDef(7, 1, radius: WheelDef.MaxRadius)],
            [new TouchSensorDef(8, 2)],
            [new RubberDef(9, 3)],
            [],
            [new BrakeDef(10, 4)],
            nextPartId: 11);

        CreatureReadiness.IsTooShort(creature, CreatureElementKind.Beam, 1, 2).ShouldBeFalse();
        CreatureReadiness.IsTooShort(creature, CreatureElementKind.Beam, 3, 4).ShouldBeFalse();
        Should.Throw<ArgumentOutOfRangeException>(() => CreatureReadiness.MinimumLength(CreatureElementKind.Sensor, 15, 15));
        Should.Throw<ArgumentNullException>(() => CreatureReadiness.Problems(null!));
    }

    private static (CreatureDef Creature, UiText[] Problems) Drawing(string drawing)
    {
        NodeDef[] corner = [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(120, 0)), new NodeDef(3, new Vector2D(0, 120))];
        BeamDef[] cornerBeams = [new BeamDef(4, 1, 2), new BeamDef(5, 1, 3)];
        switch (drawing)
        {
            case "empty":
                return (new CreatureDef([], [], []), [UiText.Plain("Add at least one joint before training this creation.")]);
            case "loose joint and zero-length beam":
                return (
                    new CreatureDef([new NodeDef(5, new Vector2D(0, 0)), new NodeDef(9, new Vector2D(0, 0)), new NodeDef(12, new Vector2D(4, 0))], [new BeamDef(20, 5, 9)], []),
                    [
                        UiText.Format("Joint {0} has nothing attached. Connect it with a link or remove it.", 3),
                        UiText.Format("The beam between joint {0} and joint {1} has zero length. Move one of the joints apart.", 1, 2),
                    ]);
            case "two pieces":
                return (
                    new CreatureDef(
                        [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(100, 0)), new NodeDef(3, new Vector2D(0, 300)), new NodeDef(4, new Vector2D(100, 300))],
                        [new BeamDef(5, 1, 2)],
                        [],
                        [new PistonDef(6, 3, 4)]),
                    [UiText.Counted(
                        "{0} piece of the creation is not connected to the rest. Connect it with a link or remove it.",
                        "{0} pieces of the creation are not connected to each other. Connect them with links or remove all but one.",
                        2)]);
            case "zero-length piston":
                return (
                    new CreatureDef([new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(0, 0))], [], [], [new PistonDef(301, 1, 2)]),
                    [UiText.Format("The piston between joint {0} and joint {1} has zero length. Move one of the joints apart.", 1, 2)]);
            case "servo missing a side":
                var servo = new CreatureDef(corner, cornerBeams, [], [new ServoDef(6, 1, null, 5)], [], [], nextPartId: 7);
                return (servo, [UiText.Format(_missingSide, PartNames.Display(servo.Parts, 6))]);

            // A Brake takes its sides by the Servo's rules (#1039), so one with a deleted side cannot train either.
            case "brake missing a side":
                return (
                    new CreatureDef(corner, cornerBeams, [], [], [], [], [], [], [], [], [new BrakeDef(6, 1, 4, null)], nextPartId: 7),
                    [UiText.Format(_missingSide, UiText.Format("Brake {0}", 1))]);
            case "servo on a one-link joint":
                var oneLink = new CreatureDef(corner[..2], [cornerBeams[0]], [], [new ServoDef(6, 1, null, 4)], [], [], nextPartId: 7);
                return (oneLink, [UiText.Format(
                    "{0} needs two links, or a link and a Wheel, at its joint. Connect another link or add a Wheel there, or delete it.",
                    PartNames.Display(oneLink.Parts, 6))]);

            // A link and the Wheel, a one-sided link, are its two sides (#1118).
            case "servo turning its joint's wheel":
                return (new CreatureDef(corner[..2], [cornerBeams[0]], [], [new ServoDef(6, 1, 4, 7)], [], [], [new WheelDef(7, 1)], nextPartId: 8), []);
            default:
                return (new CreatureDef(corner[..2], [cornerBeams[0]], []), []);
        }
    }
}
