using NodeRunner.App.Builders;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.Builders;

public sealed class CreatureBuilderTests
{
    [Fact]
    public void AddNode_ReturnsSequentialIndices()
    {
        var builder = new CreatureBuilder();

        var first = builder.AddNode(new Vector2D(0, 0), 1);
        var second = builder.AddNode(new Vector2D(2, 0), 1);

        first.ShouldBe(0);
        second.ShouldBe(1);
        builder.Nodes.Count.ShouldBe(2);
    }

    [Fact]
    public void MoveNode_UpdatesPositionAndKeepsRadius()
    {
        var builder = new CreatureBuilder();
        var node = builder.AddNode(new Vector2D(0, 0), 1.5);

        builder.MoveNode(node, new Vector2D(5, 7));

        builder.Nodes[node].Position.ShouldBe(new Vector2D(5, 7));
        builder.Nodes[node].Radius.ShouldBe(1.5);
    }

    [Fact]
    public void MoveNode_WithInvalidIndex_Throws()
    {
        var builder = new CreatureBuilder();

        var action = () => builder.MoveNode(0, new Vector2D(1, 1));

        action.ShouldThrow<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void AddBeam_BetweenDistinctNodes_ReturnsIndex()
    {
        var builder = new CreatureBuilder();
        var a = builder.AddNode(new Vector2D(0, 0), 1);
        var b = builder.AddNode(new Vector2D(2, 0), 1);

        var beamIndex = builder.AddBeam(a, b);

        beamIndex.ShouldBe(0);
        builder.Beams[0].NodeA.ShouldBe(a);
        builder.Beams[0].NodeB.ShouldBe(b);
    }

    [Fact]
    public void AddBeam_ToSameNode_Throws()
    {
        var builder = new CreatureBuilder();
        var node = builder.AddNode(new Vector2D(0, 0), 1);

        var action = () => { builder.AddBeam(node, node); };

        action.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void AddBeam_DuplicateInEitherOrder_Throws()
    {
        var builder = new CreatureBuilder();
        var a = builder.AddNode(new Vector2D(0, 0), 1);
        var b = builder.AddNode(new Vector2D(2, 0), 1);
        builder.AddBeam(a, b);

        var actionSameOrder = () => { builder.AddBeam(a, b); };
        var actionReversed = () => { builder.AddBeam(b, a); };

        actionSameOrder.ShouldThrow<ArgumentException>();
        actionReversed.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void AddCore_OnExistingNode_ReturnsIndex()
    {
        var builder = new CreatureBuilder();
        var node = builder.AddNode(new Vector2D(0, 0), 1);

        var coreIndex = builder.AddCore(node);

        coreIndex.ShouldBe(0);
        builder.Cores[0].NodeIndex.ShouldBe(node);
    }

    [Fact]
    public void RemoveBeam_RemovesOnlyThatBeam()
    {
        var builder = new CreatureBuilder();
        var a = builder.AddNode(new Vector2D(0, 0), 1);
        var b = builder.AddNode(new Vector2D(2, 0), 1);
        var c = builder.AddNode(new Vector2D(4, 0), 1);
        builder.AddBeam(a, b);
        var second = builder.AddBeam(b, c);

        builder.RemoveBeam(0);

        builder.Beams.Count.ShouldBe(1);
        builder.Beams[0].NodeA.ShouldBe(b);
        builder.Beams[0].NodeB.ShouldBe(c);
        second.ShouldBe(1);
    }

    [Fact]
    public void RemoveCore_RemovesOnlyThatCore()
    {
        var builder = new CreatureBuilder();
        var node = builder.AddNode(new Vector2D(0, 0), 1);
        builder.AddCore(node);
        builder.AddCore(node);

        builder.RemoveCore(0);

        builder.Cores.Count.ShouldBe(1);
    }

    [Fact]
    public void RemoveNode_CascadesToAttachedBeamsAndCores()
    {
        var builder = new CreatureBuilder();
        var a = builder.AddNode(new Vector2D(0, 0), 1);
        var b = builder.AddNode(new Vector2D(2, 0), 1);
        var c = builder.AddNode(new Vector2D(4, 0), 1);
        builder.AddBeam(a, b);
        builder.AddBeam(b, c);
        builder.AddCore(b);

        builder.RemoveNode(b);

        builder.Nodes.Count.ShouldBe(2);
        builder.Beams.Count.ShouldBe(0);
        builder.Cores.Count.ShouldBe(0);
    }

    [Fact]
    public void RemoveNode_ReindexesSurvivingBeamsAndCores()
    {
        var builder = new CreatureBuilder();
        var a = builder.AddNode(new Vector2D(0, 0), 1);
        var b = builder.AddNode(new Vector2D(2, 0), 1);
        var c = builder.AddNode(new Vector2D(4, 0), 1);
        builder.AddBeam(b, c);
        builder.AddCore(c);

        builder.RemoveNode(a);

        builder.Nodes.Count.ShouldBe(2);
        builder.Beams[0].NodeA.ShouldBe(0);
        builder.Beams[0].NodeB.ShouldBe(1);
        builder.Cores[0].NodeIndex.ShouldBe(1);
    }

    [Fact]
    public void TryBuild_WithNoNodes_ReturnsUnderstandableError()
    {
        var builder = new CreatureBuilder();

        var succeeded = builder.TryBuild(out var creature, out var errors);

        succeeded.ShouldBeFalse();
        creature.ShouldBeNull();
        errors.ShouldContain(e => e.Contains("at least one node", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TryBuild_WithNodeMissingABeam_ReturnsUnderstandableError()
    {
        var builder = new CreatureBuilder();
        builder.AddNode(new Vector2D(0, 0), 1);
        builder.AddNode(new Vector2D(2, 0), 1);

        var succeeded = builder.TryBuild(out var creature, out var errors);

        succeeded.ShouldBeFalse();
        creature.ShouldBeNull();
        errors.ShouldContain(e => e.Contains("no beams attached", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TryBuild_WithZeroLengthBeam_ReturnsUnderstandableError()
    {
        var builder = new CreatureBuilder();
        var a = builder.AddNode(new Vector2D(0, 0), 1);
        var b = builder.AddNode(new Vector2D(0, 0), 1);
        builder.AddBeam(a, b);

        var succeeded = builder.TryBuild(out var creature, out var errors);

        succeeded.ShouldBeFalse();
        creature.ShouldBeNull();
        errors.ShouldContain(e => e.Contains("zero length", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TryBuild_WithValidAnatomy_ReturnsCreatureDef()
    {
        var builder = new CreatureBuilder();
        var a = builder.AddNode(new Vector2D(0, 0), 1);
        var b = builder.AddNode(new Vector2D(2, 0), 1);
        builder.AddBeam(a, b);
        builder.AddCore(a);

        var succeeded = builder.TryBuild(out var creature, out var errors);

        succeeded.ShouldBeTrue();
        creature.ShouldNotBeNull();
        errors.ShouldBeEmpty();
        creature.Nodes.Count.ShouldBe(2);
        creature.Beams.Count.ShouldBe(1);
        creature.Cores.Count.ShouldBe(1);
    }

    [Fact]
    public void TryBuild_MatchesHardcodedWormShape()
    {
        var builder = new CreatureBuilder();
        var previous = builder.AddNode(new Vector2D(0, 0), 18);
        for (var i = 1; i < 5; i++)
        {
            var next = builder.AddNode(new Vector2D(i * 56, 0), 18);
            builder.AddBeam(previous, next);
            previous = next;
        }

        builder.AddCore(0);

        var succeeded = builder.TryBuild(out var creature, out var errors);

        succeeded.ShouldBeTrue();
        creature.ShouldNotBeNull();
        errors.ShouldBeEmpty();
        creature.Nodes.Count.ShouldBe(5);
        creature.Beams.Count.ShouldBe(4);
        creature.Cores.Count.ShouldBe(1);

        var motorRelations = MotorTopology.BuildNodeConnections(creature).Count(connection => connection.IsMotorized);
        motorRelations.ShouldBe(3);
    }
}
