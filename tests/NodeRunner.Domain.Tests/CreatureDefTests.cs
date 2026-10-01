using System.Text.Json;

namespace NodeRunner.Domain.Tests;

public sealed class CreatureDefTests
{
    [Fact]
    public void Constructor_WithValidAnatomy_StoresParts()
    {
        var nodes = new[]
        {
            new NodeDef(1, new Vector2D(0, 0), 1),
            new NodeDef(2, new Vector2D(2, 0), 1),
        };
        var beams = new[] { new BeamDef(3, 1, 2) };
        var cores = new[] { new CoreDef(4, 1) };

        var creature = new CreatureDef(nodes, beams, cores);

        creature.Nodes.ToArray().ShouldBe(nodes);
        creature.Beams.ToArray().ShouldBe(beams);
        creature.Cores.ToArray().ShouldBe(cores);
    }

    [Fact]
    public void Constructor_WithNoNodes_AcceptsAnEmptyDrawing()
    {
        var creature = new CreatureDef([], [], []);

        creature.Nodes.ShouldBeEmpty();
    }

    [Fact]
    public void Constructor_WithUnfinishedDrawing_AcceptsLooseNodesAndZeroLengthBeams()
    {
        var creature = new CreatureDef(
            new[]
            {
                new NodeDef(1, new Vector2D(0, 0), 1),
                new NodeDef(2, new Vector2D(0, 0), 1),
                new NodeDef(3, new Vector2D(4, 0), 1),
            },
            [new BeamDef(101, 1, 2)],
            []);

        creature.Nodes.Count.ShouldBe(3);
        creature.Beams.Count.ShouldBe(1);
    }

    [Fact]
    public void Constructor_WithOutOfRangeBeamNode_Throws()
    {
        var action = () => new CreatureDef(
            new[]
            {
                new NodeDef(1, new Vector2D(0, 0), 1),
                new NodeDef(2, new Vector2D(2, 0), 1),
            },
            new[] { new BeamDef(101, 1, 3) },
            []);

        action.ShouldThrow<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_WithOutOfRangeCoreNode_Throws()
    {
        var action = () => new CreatureDef(
            new[]
            {
                new NodeDef(1, new Vector2D(0, 0), 1),
                new NodeDef(2, new Vector2D(2, 0), 1),
            },
            new[] { new BeamDef(101, 1, 2) },
            new[] { new CoreDef(201, 3) });

        action.ShouldThrow<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void JsonRoundTrip_PreservesCreatureDefinition()
    {
        var original = new CreatureDef(
            new[]
            {
                new NodeDef(1, new Vector2D(0, 0), 1),
                new NodeDef(2, new Vector2D(2, 0), 1.5),
            },
            new[] { new BeamDef(101, 1, 2) },
            new[] { new CoreDef(201, 1) });

        var json = JsonSerializer.Serialize(original);

        var roundTripped = JsonSerializer.Deserialize<CreatureDef>(json);

        roundTripped.ShouldNotBeNull();
        roundTripped.Nodes.ToArray().ShouldBe(original.Nodes.ToArray());
        roundTripped.Beams.ToArray().ShouldBe(original.Beams.ToArray());
        roundTripped.Cores.ToArray().ShouldBe(original.Cores.ToArray());
        roundTripped.NextPartId.ShouldBe(original.NextPartId);
        json.ShouldContain("\"Id\":1");
        json.ShouldContain("\"NextPartId\":202");
    }

    [Fact]
    public void Constructor_WithOmittedNextPartId_DefaultsToMaxIdPlusOne()
    {
        var creature = new CreatureDef(
            [new NodeDef(7, new Vector2D(0, 0), 1), new NodeDef(3, new Vector2D(1, 0), 1)],
            [new BeamDef(12, 7, 3)],
            []);

        creature.NextPartId.ShouldBe(13);
    }

    [Fact]
    public void Lookups_UseIdsSoReferencesSurviveReorderedLists()
    {
        var creature = new CreatureDef(
            [new NodeDef(20, new Vector2D(2, 0), 1), new NodeDef(10, new Vector2D(0, 0), 1)],
            [new BeamDef(30, 10, 20)],
            [new CoreDef(40, 10)],
            nextPartId: 41);

        creature.NodeIndexOf(10).ShouldBe(1);
        creature.NodeIndexOf(20).ShouldBe(0);
        creature.BeamIndexOf(30).ShouldBe(0);
        creature.CoreIndexOf(40).ShouldBe(0);

        var roundTripped = JsonSerializer.Deserialize<CreatureDef>(JsonSerializer.Serialize(creature));

        roundTripped.ShouldNotBeNull();
        roundTripped.Beams[0].NodeA.ShouldBe(10);
        roundTripped.Beams[0].NodeB.ShouldBe(20);
        roundTripped.Cores[0].NodeId.ShouldBe(10);
        roundTripped.NodeIndexOf(10).ShouldBe(1);
    }

    [Fact]
    public void Constructor_WithDuplicatePartId_Throws()
    {
        var action = () => new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0), 1), new NodeDef(2, new Vector2D(1, 0), 1)],
            [new BeamDef(1, 1, 2)],
            []);

        action.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithPartIdAtOrBeyondNextPartId_Throws()
    {
        var action = () => new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0), 1), new NodeDef(2, new Vector2D(1, 0), 1)],
            [new BeamDef(3, 1, 2)],
            [],
            nextPartId: 3);

        action.ShouldThrow<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Names_AreDisplayMetadataAndDoNotAffectReferences()
    {
        var creature = new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0), 1, "Leg"), new NodeDef(2, new Vector2D(1, 0), 1, "Leg")],
            [new BeamDef(3, 1, 2, "Leg")],
            [new CoreDef(4, 1, "Leg")],
            nextPartId: 5);

        creature.Nodes.Select(node => node.Name).ShouldBe(["Leg", "Leg"]);
        creature.Beams[0].NodeA.ShouldBe(1);
        creature.Cores[0].NodeId.ShouldBe(1);
    }

    [Fact]
    public void Constructor_DefensivelyCopiesInputCollections()
    {
        var nodes = new[]
        {
            new NodeDef(1, new Vector2D(0, 0), 1),
            new NodeDef(2, new Vector2D(2, 0), 1),
        };
        var beams = new[] { new BeamDef(3, 1, 2) };
        var cores = new[] { new CoreDef(4, 1) };

        var creature = new CreatureDef(nodes, beams, cores);
        nodes[0] = new NodeDef(10, new Vector2D(99, 99), 1);
        beams[0] = new BeamDef(11, 2, 1);
        cores[0] = new CoreDef(12, 2);

        creature.Nodes[0].Position.ShouldBe(new Vector2D(0, 0));
        creature.Beams[0].ShouldBe(new BeamDef(3, 1, 2));
        creature.Cores[0].ShouldBe(new CoreDef(4, 1));
    }
}
