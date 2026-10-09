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
    [InlineData(81.9, false)]
    [InlineData(82, true)]
    public void Problems_WithABeamShorterThanTheMinimumGap_SaysItIsTooShort(double length, bool fits)
    {
        var creature = new CreatureDef([new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(0, length))], [new BeamDef(101, 1, 2)], []);

        CreatureReadiness.IsTooShort(creature, 1, 2).ShouldBe(!fits);
        CreatureReadiness.Problems(creature).ShouldBe(fits
            ? []
            : [UiText.Format("The beam between joint {0} and joint {1} is too short. Move one of the joints apart.", 1, 2)]);
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
            [UiText.Format("{0} is missing a link. Pick two links at its joint or delete it.",
                PartNames.Display(creature.Nodes, creature.Beams, creature.Sensors, creature.Servos, creature.Pistons, creature.Springs, creature.Wheels, 6))]);
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
            [UiText.Format("{0} needs two links at its joint. Connect another link there or delete it.",
                PartNames.Display(creature.Nodes, creature.Beams, creature.Sensors, creature.Servos, creature.Pistons, creature.Springs, creature.Wheels, 6))]);
    }

    [Fact]
    public void Problems_WithAServoJoint_UsesServoRadiusForTooShortCheck()
    {
        var creature = new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(90, 0)), new NodeDef(3, new Vector2D(0, 120))],
            [new BeamDef(4, 1, 2), new BeamDef(5, 1, 3)],
            [],
            [new ServoDef(6, 1, 4, 5)],
            [],
            [],
            nextPartId: 7);

        CreatureReadiness.IsTooShort(new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(90, 0)), NodeDef.PlainJointRadius, NodeDef.PlainJointRadius).ShouldBeFalse();
        CreatureReadiness.IsTooShort(creature, 1, 2).ShouldBeTrue();
        CreatureReadiness.Problems(creature).ShouldContain(UiText.Format("The beam between joint {0} and joint {1} is too short. Move one of the joints apart.", 1, 2));
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
