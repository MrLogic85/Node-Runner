using NodeRunner.App.Builders;
using NodeRunner.App.Lifecycle;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.Lifecycle;

public sealed class CreatureReadinessTests
{
    [Fact]
    public void Problems_WithEmptyDrawing_AsksForANode()
    {
        CreatureReadiness.Problems(new CreatureDef([], [], []))
            .ShouldBe([UiText.Plain("Add at least one joint before training this creation.")]);
    }

    [Fact]
    public void Problems_WithLooseNodeAndZeroLengthBeam_NamesEachByListPosition()
    {
        var creature = new CreatureDef(
            [new NodeDef(5, new Vector2D(0, 0)), new NodeDef(9, new Vector2D(0, 0)), new NodeDef(12, new Vector2D(4, 0))],
            [new BeamDef(20, 5, 9)],
            []);

        CreatureReadiness.Problems(creature).ShouldBe(
        [
            UiText.Format("Joint {0} has nothing attached. Connect it with a link or remove it.", 3),
            UiText.Format("The beam between joint {0} and joint {1} has zero length. Move one of the joints apart.", 1, 2),
        ]);
    }

    [Fact]
    public void Problems_WithTwoSeparatePieces_SaysTheyAreNotConnected()
    {
        var creature = new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(100, 0)), new NodeDef(3, new Vector2D(0, 300)), new NodeDef(4, new Vector2D(100, 300))],
            [new BeamDef(5, 1, 2)],
            [],
            [new PistonDef(6, 3, 4)]);

        CreatureReadiness.Problems(creature).ShouldBe(
            [UiText.Counted(
                "{0} piece of the creation is not connected to the rest. Connect it with a link or remove it.",
                "{0} pieces of the creation are not connected to each other. Connect them with links or remove all but one.",
                2)]);
        CreatureReadiness.CanTrain(creature).ShouldBeFalse();
    }

    [Fact]
    public void Pieces_JoinedByBeamsPistonsAndSprings_AreOne_AndLeaveLooseJointsOut()
    {
        var nodes = Enumerable.Range(1, 5).Select(id => new NodeDef(id, new Vector2D(id * 100, 0))).ToArray();
        var links = LinkRef.All([new BeamDef(11, 1, 2)], [new PistonDef(12, 2, 3)], [new SpringDef(13, 4, 3)]);

        CreatureReadiness.Pieces(nodes, links).ShouldBe([[1, 2, 3, 4]]);
    }

    [Fact]
    public void Pieces_ListEachInNodeOrder_ByItsFirstNode()
    {
        var nodes = new[] { new NodeDef(7, new Vector2D(0, 0)), new NodeDef(3, new Vector2D(100, 0)), new NodeDef(9, new Vector2D(200, 0)), new NodeDef(1, new Vector2D(300, 0)) };
        var links = LinkRef.All([new BeamDef(20, 1, 7), new BeamDef(21, 9, 3)], [], []);

        CreatureReadiness.Pieces(nodes, links).ShouldBe([[7, 1], [3, 9]]);
    }

    [Fact]
    public void Problems_WithAZeroLengthPiston_SaysSo()
    {
        var creature = new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(0, 0))],
            [],
            [],
            [new PistonDef(301, 1, 2)]);

        CreatureReadiness.Problems(creature).ShouldBe(
            [UiText.Format("The piston between joint {0} and joint {1} has zero length. Move one of the joints apart.", 1, 2)]);
    }

    [Theory]
    [InlineData(CreatureElementKind.Beam, 0, 55.9, false)]
    [InlineData(CreatureElementKind.Beam, 0, 56, true)]
    [InlineData(CreatureElementKind.Beam, 1, 67.9, false)]
    [InlineData(CreatureElementKind.Beam, 1, 68, true)]
    [InlineData(CreatureElementKind.Beam, 2, 79.9, false)]
    [InlineData(CreatureElementKind.Beam, 2, 80, true)]
    [InlineData(CreatureElementKind.Piston, 0, 71.9, false)]
    [InlineData(CreatureElementKind.Piston, 0, 72, true)]
    [InlineData(CreatureElementKind.Piston, 1, 83.9, false)]
    [InlineData(CreatureElementKind.Piston, 1, 84, true)]
    [InlineData(CreatureElementKind.Piston, 2, 95.9, false)]
    [InlineData(CreatureElementKind.Piston, 2, 96, true)]
    [InlineData(CreatureElementKind.Spring, 0, 71.9, false)]
    [InlineData(CreatureElementKind.Spring, 0, 72, true)]
    [InlineData(CreatureElementKind.Spring, 1, 83.9, false)]
    [InlineData(CreatureElementKind.Spring, 1, 84, true)]
    [InlineData(CreatureElementKind.Spring, 2, 95.9, false)]
    [InlineData(CreatureElementKind.Spring, 2, 96, true)]
    public void Problems_WithALinkShorterThanItsMinimum_SaysItIsTooShort(CreatureElementKind kind, int motors, double length, bool fits)
    {
        // #1212: 26 free units between what covers each end, a hub (15) or a joint motor (27), and
        // 16 more for a Piston's or Spring's telescope (#1210).
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
        var tooShort = kind switch
        {
            CreatureElementKind.Piston => UiText.Format("The piston between joint {0} and joint {1} is too short. Move one of the joints apart.", 1, 2),
            CreatureElementKind.Spring => UiText.Format("The spring between joint {0} and joint {1} is too short. Move one of the joints apart.", 1, 2),
            _ => UiText.Format("The beam between joint {0} and joint {1} is too short. Move one of the joints apart.", 1, 2),
        };

        CreatureReadiness.IsTooShort(creature, kind, 1, 2).ShouldBe(!fits);
        if (fits)
        {
            CreatureReadiness.Problems(creature).ShouldNotContain(tooShort);
        }
        else
        {
            CreatureReadiness.Problems(creature).ShouldContain(tooShort);
        }
    }

    [Fact]
    public void Problems_WithAMissingServoLink_SaysItIsMissingALink()
    {
        var creature = new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(120, 0)), new NodeDef(3, new Vector2D(0, 120))],
            [new BeamDef(4, 1, 2), new BeamDef(5, 1, 3)],
            [],
            [new ServoDef(6, 1, null, 5)],
            [],
            [],
            nextPartId: 7);

        CreatureReadiness.Problems(creature).ShouldBe(
            [UiText.Format("{0} is missing a link. Pick its Fixed and Target links or delete it.",
                PartNames.Display(creature.Nodes, creature.Beams, creature.Sensors, creature.Servos, creature.Pistons, creature.Springs, creature.Wheels, creature.TouchSensors, creature.Rubbers, creature.VelocityMotors, creature.Brakes, creature.AngleSensors, 6))]);
    }

    // A Brake takes its sides by the Servo's rules (#1039), so one with a deleted side cannot train either.
    [Fact]
    public void Problems_WithABrakeMissingASide_SaysItIsMissingALink()
    {
        var creature = new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(120, 0)), new NodeDef(3, new Vector2D(0, 120))],
            [new BeamDef(4, 1, 2), new BeamDef(5, 1, 3)],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [new BrakeDef(6, 1, 4, null)],
            nextPartId: 7);

        CreatureReadiness.Problems(creature).ShouldBe(
            [UiText.Format("{0} is missing a link. Pick its Fixed and Target links or delete it.", UiText.Format("Brake {0}", 1))]);
    }

    // An Angle sensor takes its sides by the Servo's rules (#1204): one with a deleted side cannot train until it is replaced.
    [Fact]
    public void Problems_WithAnAngleSensorMissingASide_SaysItIsMissingALink_UntilItIsReplaced()
    {
        var builder = new CreatureBuilder();
        var joint = builder.AddNode(new Vector2D(0, 0));
        var far = builder.AddNode(new Vector2D(120, 0));
        var top = builder.AddNode(new Vector2D(0, 120));
        var first = builder.AddBeam(joint, far);
        builder.AddBeam(joint, top);
        builder.AddBeam(far, top);
        var third = builder.AddBeam(joint, builder.AddNode(new Vector2D(-120, 0)));
        var sensor = builder.AddAngleSensor(joint);
        CreatureReadiness.Problems(builder.Build()).ShouldBeEmpty();

        builder.RemoveBeam(first);

        CreatureReadiness.Problems(builder.Build()).ShouldBe(
            [UiText.Format("{0} is missing a link. Pick its Fixed and Target links or delete it.", UiText.Format("Angle {0}", 1))]);
        builder.SetAngleSensorLink(sensor, fixedRole: true, third);
        CreatureReadiness.Problems(builder.Build()).ShouldBeEmpty();
    }

    [Fact]
    public void Problems_WithAServoOnAOneLinkJoint_AsksForAnotherLink()
    {
        var creature = new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(120, 0))],
            [new BeamDef(4, 1, 2)],
            [],
            [new ServoDef(6, 1, null, 4)],
            [],
            [],
            nextPartId: 7);

        CreatureReadiness.Problems(creature).ShouldBe(
            [UiText.Format("{0} needs two links, or a link and a Wheel, at its joint. Connect another link or add a Wheel there, or delete it.",
                PartNames.Display(creature.Nodes, creature.Beams, creature.Sensors, creature.Servos, creature.Pistons, creature.Springs, creature.Wheels, creature.TouchSensors, creature.Rubbers, creature.VelocityMotors, creature.Brakes, creature.AngleSensors, 6))]);
    }

    [Fact]
    public void Problems_WithAServoTurningItsJointsWheel_HasNone()
    {
        // A link and the Wheel, a one-sided link, are its two sides (#1118).
        var creature = new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(120, 0))],
            [new BeamDef(4, 1, 2)],
            [],
            [new ServoDef(6, 1, 4, 7)],
            [],
            [],
            [new WheelDef(7, 1)],
            nextPartId: 8);

        CreatureReadiness.Problems(creature).ShouldBeEmpty();
    }

    [Fact]
    public void IsTooShort_IgnoresWheelsBrakesTouchSensorsAndRubbers()
    {
        // #1212: only a joint motor covers a link's end beyond the hub; a Wheel lies under its links (#1086).
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
    }

    [Theory]
    [InlineData(CreatureElementKind.Beam, 15, 15, 56)]
    [InlineData(CreatureElementKind.Beam, 27, 15, 68)]
    [InlineData(CreatureElementKind.Beam, 27, 27, 80)]
    [InlineData(CreatureElementKind.Piston, 15, 15, 72)]
    [InlineData(CreatureElementKind.Piston, 27, 15, 84)]
    [InlineData(CreatureElementKind.Piston, 27, 27, 96)]
    [InlineData(CreatureElementKind.Spring, 15, 15, 72)]
    [InlineData(CreatureElementKind.Spring, 27, 15, 84)]
    [InlineData(CreatureElementKind.Spring, 27, 27, 96)]
    public void MinimumLength_AddsTheGapAndTheLinkTypesExtraToWhatCoversEachEnd(CreatureElementKind kind, double coveredA, double coveredB, double minimum)
    {
        CreatureReadiness.MinimumLength(kind, coveredA, coveredB).ShouldBe(minimum);
    }

    [Fact]
    public void MinimumLength_RejectsAPartThatIsNotALink()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => CreatureReadiness.MinimumLength(CreatureElementKind.Sensor, 15, 15));
    }

    [Fact]
    public void CanTrain_WithSingleBeam_IsTrueThoughNothingMoves()
    {
        // No powered part is needed (#845): it trains, and only stands still.
        var creature = new CreatureDef([new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(90, 0))], [new BeamDef(101, 1, 2)], []);

        CreatureReadiness.Problems(creature).ShouldBeEmpty();
        CreatureReadiness.CanTrain(creature).ShouldBeTrue();
    }

    [Fact]
    public void CanTrain_WithOnlyPassiveJoints_IsTrue()
    {
        var creature = new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(90, 0)), new NodeDef(3, new Vector2D(180, 10))],
            [new BeamDef(101, 1, 2), new BeamDef(102, 2, 3)],
            [new SensorDef(201, 101, SensorKind.Accelerometer)]);

        CreatureReadiness.CanTrain(creature).ShouldBeTrue();
    }

    [Fact]
    public void CanTrain_WithAPiston_IsTrue()
    {
        var creature = new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(90, 0)), new NodeDef(3, new Vector2D(180, 10))],
            [new BeamDef(101, 1, 2), new BeamDef(102, 2, 3)],
            [new SensorDef(201, 101, SensorKind.Accelerometer)],
            [new PistonDef(301, 1, 3)]);

        CreatureReadiness.CanTrain(creature).ShouldBeTrue();
    }

    [Fact]
    public void CanTrain_WithAPistonAndALooseNode_IsFalse()
    {
        var creature = new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(90, 0)), new NodeDef(3, new Vector2D(180, 10)), new NodeDef(4, new Vector2D(300, 80))],
            [new BeamDef(101, 1, 2), new BeamDef(102, 2, 3)],
            [new SensorDef(201, 101, SensorKind.Accelerometer)],
            [new PistonDef(301, 1, 3)]);

        CreatureReadiness.CanTrain(creature).ShouldBeFalse();
    }

    [Fact]
    public void Problems_WithNullCreature_Throws()
    {
        Should.Throw<ArgumentNullException>(() => CreatureReadiness.Problems(null!));
    }
}
