using System.Text.Json;

namespace NodeRunner.Domain.Tests;

public sealed class CreatureDefTests
{
    [Fact]
    public void Constructor_WithValidAnatomy_StoresParts()
    {
        var nodes = new[]
        {
            new NodeDef(new Vector2D(0, 0), 1),
            new NodeDef(new Vector2D(2, 0), 1),
        };
        var beams = new[] { new BeamDef(0, 1) };
        var cores = new[] { new CoreDef(0) };

        var creature = new CreatureDef(nodes, beams, cores);

        creature.Nodes.ToArray().ShouldBe(nodes);
        creature.Beams.ToArray().ShouldBe(beams);
        creature.Cores.ToArray().ShouldBe(cores);
    }

    [Fact]
    public void Constructor_WithNoNodes_Throws()
    {
        var action = () => new CreatureDef([], [], []);

        action.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithNodeMissingAnyBeam_Throws()
    {
        var action = () => new CreatureDef(
            new[]
            {
                new NodeDef(new Vector2D(0, 0), 1),
                new NodeDef(new Vector2D(2, 0), 1),
            },
            [],
            []);

        action.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithOutOfRangeBeamNode_Throws()
    {
        var action = () => new CreatureDef(
            new[]
            {
                new NodeDef(new Vector2D(0, 0), 1),
                new NodeDef(new Vector2D(2, 0), 1),
            },
            new[] { new BeamDef(0, 2) },
            []);

        action.ShouldThrow<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_WithOutOfRangeCoreNode_Throws()
    {
        var action = () => new CreatureDef(
            new[]
            {
                new NodeDef(new Vector2D(0, 0), 1),
                new NodeDef(new Vector2D(2, 0), 1),
            },
            new[] { new BeamDef(0, 1) },
            new[] { new CoreDef(2) });

        action.ShouldThrow<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void JsonRoundTrip_PreservesCreatureDefinition()
    {
        var original = new CreatureDef(
            new[]
            {
                new NodeDef(new Vector2D(0, 0), 1),
                new NodeDef(new Vector2D(2, 0), 1.5),
            },
            new[] { new BeamDef(0, 1) },
            new[] { new CoreDef(0) });

        var json = JsonSerializer.Serialize(original);

        var roundTripped = JsonSerializer.Deserialize<CreatureDef>(json);

        roundTripped.ShouldNotBeNull();
        roundTripped.Nodes.ToArray().ShouldBe(original.Nodes.ToArray());
        roundTripped.Beams.ToArray().ShouldBe(original.Beams.ToArray());
        roundTripped.Cores.ToArray().ShouldBe(original.Cores.ToArray());
    }

    [Fact]
    public void Constructor_DefensivelyCopiesInputCollections()
    {
        var nodes = new[]
        {
            new NodeDef(new Vector2D(0, 0), 1),
            new NodeDef(new Vector2D(2, 0), 1),
        };
        var beams = new[] { new BeamDef(0, 1) };
        var cores = new[] { new CoreDef(0) };

        var creature = new CreatureDef(nodes, beams, cores);
        nodes[0] = new NodeDef(new Vector2D(99, 99), 1);
        beams[0] = new BeamDef(1, 0);
        cores[0] = new CoreDef(1);

        creature.Nodes[0].Position.ShouldBe(new Vector2D(0, 0));
        creature.Beams[0].ShouldBe(new BeamDef(0, 1));
        creature.Cores[0].ShouldBe(new CoreDef(0));
    }
}
