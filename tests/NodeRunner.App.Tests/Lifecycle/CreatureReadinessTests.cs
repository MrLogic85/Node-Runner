using NodeRunner.App.Lifecycle;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.Lifecycle;

public sealed class CreatureReadinessTests
{
    [Fact]
    public void Problems_WithEmptyDrawing_AsksForANode()
    {
        CreatureReadiness.Problems(new CreatureDef([], [], []))
            .ShouldBe(["Add at least one node before training this creation."]);
    }

    [Fact]
    public void Problems_WithLooseNodeAndZeroLengthBeam_NamesEachByListPosition()
    {
        var creature = new CreatureDef(
            [new NodeDef(5, new Vector2D(0, 0), 1), new NodeDef(9, new Vector2D(0, 0), 1), new NodeDef(12, new Vector2D(4, 0), 1)],
            [new BeamDef(20, 5, 9)],
            []);

        CreatureReadiness.Problems(creature).ShouldBe(
        [
            "Node 3 has nothing attached. Connect it with a beam or a piston, or remove it.",
            "The beam between node 1 and node 2 has zero length. Move one of the nodes apart.",
        ]);
    }

    [Theory]
    [InlineData(87.9, false)]
    [InlineData(88, true)]
    public void Problems_WithABeamShorterThanTheMinimumGap_SaysItIsTooShort(double length, bool fits)
    {
        var creature = new CreatureDef([new NodeDef(1, new Vector2D(0, 0), 18), new NodeDef(2, new Vector2D(0, length), 18)], [new BeamDef(101, 1, 2)], []);

        CreatureReadiness.IsTooShort(creature.Nodes[0], creature.Nodes[1]).ShouldBe(!fits);
        CreatureReadiness.Problems(creature).ShouldBe(fits
            ? []
            : ["The beam between node 1 and node 2 is too short. Move one of the nodes apart."]);
    }

    [Fact]
    public void CanTrain_WithSingleBeam_IsFalseBecauseNothingMoves()
    {
        var creature = new CreatureDef([new NodeDef(1, new Vector2D(0, 0), 1), new NodeDef(2, new Vector2D(60, 0), 1)], [new BeamDef(101, 1, 2)], []);

        CreatureReadiness.Problems(creature).ShouldBeEmpty();
        CreatureReadiness.CanTrain(creature).ShouldBeFalse();
    }

    [Fact]
    public void CanTrain_WithOnlyPassiveJoints_IsFalse()
    {
        var creature = new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0), 1), new NodeDef(2, new Vector2D(60, 0), 1), new NodeDef(3, new Vector2D(120, 10), 1)],
            [new BeamDef(101, 1, 2), new BeamDef(102, 2, 3)],
            [new SensorDef(201, 101, SensorKind.Accelerometer)]);

        CreatureReadiness.CanTrain(creature).ShouldBeFalse();
    }

    [Fact]
    public void CanTrain_WithAPiston_IsTrue()
    {
        var creature = new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0), 1), new NodeDef(2, new Vector2D(60, 0), 1), new NodeDef(3, new Vector2D(120, 10), 1)],
            [new BeamDef(101, 1, 2), new BeamDef(102, 2, 3)],
            [new SensorDef(201, 101, SensorKind.Accelerometer)],
            [new PistonDef(301, 1, 3)]);

        CreatureReadiness.CanTrain(creature).ShouldBeTrue();
    }

    [Fact]
    public void CanTrain_WithAPistonAndALooseNode_IsFalse()
    {
        var creature = new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0), 1), new NodeDef(2, new Vector2D(60, 0), 1), new NodeDef(3, new Vector2D(120, 10), 1), new NodeDef(4, new Vector2D(240, 80), 1)],
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
