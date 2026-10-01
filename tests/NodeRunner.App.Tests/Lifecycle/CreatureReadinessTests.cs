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
    public void Problems_WithLooseNodeAndZeroLengthBeam_NamesEach()
    {
        var creature = new CreatureDef(
            [Node(0, 0), Node(0, 0), Node(4, 0)],
            [new BeamDef(0, 1)],
            []);

        CreatureReadiness.Problems(creature).ShouldBe(
        [
            "Node 2 has no beams attached. Connect it with a beam or remove it.",
            "The beam between node 0 and node 1 has zero length. Move one of the nodes apart.",
        ]);
    }

    [Fact]
    public void CanTrain_WithSingleBeam_IsFalseBecauseNothingMoves()
    {
        var creature = new CreatureDef([Node(0, 0), Node(2, 0)], [new BeamDef(0, 1)], []);

        CreatureReadiness.Problems(creature).ShouldBeEmpty();
        CreatureReadiness.CanTrain(creature).ShouldBeFalse();
    }

    [Fact]
    public void CanTrain_WithAMotorRelation_IsTrue()
    {
        var creature = new CreatureDef(
            [Node(0, 0), Node(2, 0), Node(4, 1)],
            [new BeamDef(0, 1), new BeamDef(1, 2)],
            [new CoreDef(0)]);

        CreatureReadiness.CanTrain(creature).ShouldBeTrue();
    }

    [Fact]
    public void CanTrain_WithAMotorRelationAndALooseNode_IsFalse()
    {
        var creature = new CreatureDef(
            [Node(0, 0), Node(2, 0), Node(4, 1), Node(8, 8)],
            [new BeamDef(0, 1), new BeamDef(1, 2)],
            [new CoreDef(0)]);

        CreatureReadiness.CanTrain(creature).ShouldBeFalse();
    }

    [Fact]
    public void Problems_WithNullCreature_Throws()
    {
        Should.Throw<ArgumentNullException>(() => CreatureReadiness.Problems(null!));
    }

    private static NodeDef Node(double x, double y) => new(new Vector2D(x, y), 1);
}
