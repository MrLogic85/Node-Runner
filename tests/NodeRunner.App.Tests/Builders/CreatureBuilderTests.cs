using NodeRunner.App.Builders;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.Builders;

public sealed class CreatureBuilderTests
{
    [Fact]
    public void AddNode_ReturnsSequentialIds()
    {
        var builder = new CreatureBuilder();

        var first = builder.AddNode(new Vector2D(0, 0), 1);
        var second = builder.AddNode(new Vector2D(2, 0), 1);

        first.ShouldBe(1);
        second.ShouldBe(2);
        builder.Nodes.Count.ShouldBe(2);
    }

    [Fact]
    public void MoveNode_UpdatesPositionAndKeepsRadius()
    {
        var builder = new CreatureBuilder();
        var node = builder.AddNode(new Vector2D(0, 0), 1.5);

        builder.MoveNode(node, new Vector2D(5, 7));

        builder.Nodes[builder.NodeIndexOf(node)].Position.ShouldBe(new Vector2D(5, 7));
        builder.Nodes[builder.NodeIndexOf(node)].Radius.ShouldBe(1.5);
    }

    [Fact]
    public void MoveNode_WithInvalidId_Throws()
    {
        var builder = new CreatureBuilder();

        var action = () => builder.MoveNode(1, new Vector2D(1, 1));

        action.ShouldThrow<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void AddBeam_BetweenDistinctNodes_ReturnsId()
    {
        var builder = new CreatureBuilder();
        var a = builder.AddNode(new Vector2D(0, 0), 1);
        var b = builder.AddNode(new Vector2D(2, 0), 1);

        var beamId = builder.AddBeam(a, b);

        beamId.ShouldBe(3);
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
    public void CanAddBeam_MatchesWhatAddBeamAccepts()
    {
        var builder = new CreatureBuilder();
        var a = builder.AddNode(new Vector2D(0, 0), 1);
        var b = builder.AddNode(new Vector2D(2, 0), 1);
        var c = builder.AddNode(new Vector2D(4, 0), 1);
        builder.AddBeam(a, b);

        builder.CanAddBeam(b, c).ShouldBeTrue();
        builder.CanAddBeam(b, a).ShouldBeFalse();
        builder.CanAddBeam(c, c).ShouldBeFalse();
        builder.CanAddBeam(c, 99).ShouldBeFalse();
        builder.CanAddBeam(-1, c).ShouldBeFalse();
    }

    [Fact]
    public void AddCore_OnExistingNode_ReturnsId()
    {
        var builder = new CreatureBuilder();
        var node = builder.AddNode(new Vector2D(0, 0), 1);

        var coreId = builder.AddCore(node);

        coreId.ShouldBe(2);
        builder.Cores[0].NodeId.ShouldBe(node);
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

        builder.RemoveBeam(builder.Beams[0].Id);

        builder.Beams.Count.ShouldBe(1);
        builder.Beams[0].NodeA.ShouldBe(b);
        builder.Beams[0].NodeB.ShouldBe(c);
        second.ShouldBe(5);
    }

    [Fact]
    public void RemoveCore_RemovesOnlyThatCore()
    {
        var builder = new CreatureBuilder();
        var node = builder.AddNode(new Vector2D(0, 0), 1);
        builder.AddCore(node);
        builder.AddCore(node);

        builder.RemoveCore(builder.Cores[0].Id);

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
    public void RemovedIds_AreNotReused()
    {
        var builder = new CreatureBuilder();
        var a = builder.AddNode(new Vector2D(0, 0), 1);
        var b = builder.AddNode(new Vector2D(2, 0), 1);
        var beam = builder.AddBeam(a, b);

        builder.RemoveBeam(beam);
        builder.RemoveNode(b);

        var c = builder.AddNode(new Vector2D(4, 0), 1);
        var newBeam = builder.AddBeam(a, c);

        c.ShouldBeGreaterThan(beam);
        newBeam.ShouldBeGreaterThan(c);
        builder.Build().NextPartId.ShouldBe(newBeam + 1);
    }

    [Fact]
    public void Constructor_FromCreature_KeepsIdsAndCounter()
    {
        var source = new CreatureDef(
            [new NodeDef(10, new Vector2D(0, 0), 1), new NodeDef(20, new Vector2D(2, 0), 1)],
            [new BeamDef(30, 10, 20)],
            [new CoreDef(40, 10)],
            nextPartId: 99);

        var builder = new CreatureBuilder(source);
        var next = builder.AddNode(new Vector2D(4, 0), 1);

        builder.Build().Nodes.Take(2).ToArray().ShouldBe(source.Nodes.ToArray());
        next.ShouldBe(99);
        builder.Build().NextPartId.ShouldBe(100);
    }

    [Fact]
    public void Rename_ChangesOnlyTheMatchingPartName()
    {
        var builder = new CreatureBuilder();
        var a = builder.AddNode(new Vector2D(0, 0), 1);
        var b = builder.AddNode(new Vector2D(2, 0), 1);
        var beam = builder.AddBeam(a, b);
        var core = builder.AddCore(a);

        builder.Rename(a, "Node");
        builder.Rename(beam, "Beam");
        builder.Rename(core, "Core");

        builder.Nodes[0].Name.ShouldBe("Node");
        builder.Beams[0].Name.ShouldBe("Beam");
        builder.Cores[0].Name.ShouldBe("Core");
        builder.Beams[0].NodeA.ShouldBe(a);
        builder.Cores[0].NodeId.ShouldBe(a);
    }

    [Fact]
    public void RemoveNode_KeepsSurvivingBeamAndCoreReferencesById()
    {
        var builder = new CreatureBuilder();
        var a = builder.AddNode(new Vector2D(0, 0), 1);
        var b = builder.AddNode(new Vector2D(2, 0), 1);
        var c = builder.AddNode(new Vector2D(4, 0), 1);
        builder.AddBeam(b, c);
        builder.AddCore(c);

        builder.RemoveNode(a);

        builder.Nodes.Count.ShouldBe(2);
        builder.Beams[0].NodeA.ShouldBe(b);
        builder.Beams[0].NodeB.ShouldBe(c);
        builder.Cores[0].NodeId.ShouldBe(c);
    }

    [Fact]
    public void Build_WithUnfinishedDrawing_ReturnsItAsItStands()
    {
        var builder = new CreatureBuilder();
        builder.AddNode(new Vector2D(0, 0), 1);
        builder.AddNode(new Vector2D(2, 0), 1);
        builder.AddCore(builder.Nodes[0].Id);

        var creature = builder.Build();

        creature.Nodes.Count.ShouldBe(2);
        creature.Beams.ShouldBeEmpty();
        creature.Cores.Count.ShouldBe(1);
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

        builder.AddCore(builder.Nodes[0].Id);

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
