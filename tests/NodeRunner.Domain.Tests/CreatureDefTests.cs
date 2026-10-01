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
        var sensors = new[] { new SensorDef(4, 3, SensorKind.Accelerometer) };

        var creature = new CreatureDef(nodes, beams, sensors);

        creature.Nodes.ToArray().ShouldBe(nodes);
        creature.Beams.ToArray().ShouldBe(beams);
        creature.Sensors.ToArray().ShouldBe(sensors);
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
    public void Constructor_WithOutOfRangeSensorBeam_Throws()
    {
        var action = () => new CreatureDef(
            new[]
            {
                new NodeDef(1, new Vector2D(0, 0), 1),
                new NodeDef(2, new Vector2D(2, 0), 1),
            },
            new[] { new BeamDef(101, 1, 2) },
            new[] { new SensorDef(201, 999, SensorKind.Accelerometer) });

        action.ShouldThrow<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_WithDuplicateSensorKindOnBeam_Throws()
    {
        var action = () => new CreatureDef(
            new[]
            {
                new NodeDef(1, new Vector2D(0, 0), 1),
                new NodeDef(2, new Vector2D(2, 0), 1),
            },
            new[] { new BeamDef(101, 1, 2) },
            new[]
            {
                new SensorDef(201, 101, SensorKind.Accelerometer),
                new SensorDef(202, 101, SensorKind.Accelerometer),
            });

        action.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithOneOfEachSensorKindOnBeam_Succeeds()
    {
        var creature = new CreatureDef(
            new[]
            {
                new NodeDef(1, new Vector2D(0, 0), 1),
                new NodeDef(2, new Vector2D(2, 0), 1),
            },
            new[] { new BeamDef(101, 1, 2) },
            new[]
            {
                new SensorDef(201, 101, SensorKind.Accelerometer),
                new SensorDef(202, 101, SensorKind.LineOfSight),
            });

        creature.Sensors.Count.ShouldBe(2);
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
            new[]
            {
                new SensorDef(201, 101, SensorKind.Accelerometer),
                new SensorDef(202, 101, SensorKind.LineOfSight),
            });

        var json = JsonSerializer.Serialize(original);

        var roundTripped = JsonSerializer.Deserialize<CreatureDef>(json);

        roundTripped.ShouldNotBeNull();
        roundTripped.Nodes.ToArray().ShouldBe(original.Nodes.ToArray());
        roundTripped.Beams.ToArray().ShouldBe(original.Beams.ToArray());
        roundTripped.Sensors.ToArray().ShouldBe(original.Sensors.ToArray());
        roundTripped.NextPartId.ShouldBe(original.NextPartId);
        json.ShouldContain("\"sensors\":");
        json.ShouldContain("\"Id\":1");
        json.ShouldContain("\"NextPartId\":203");
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
            [new SensorDef(40, 30, SensorKind.Accelerometer)],
            nextPartId: 41);

        creature.NodeIndexOf(10).ShouldBe(1);
        creature.NodeIndexOf(20).ShouldBe(0);
        creature.BeamIndexOf(30).ShouldBe(0);
        creature.SensorIndexOf(40).ShouldBe(0);

        var roundTripped = JsonSerializer.Deserialize<CreatureDef>(JsonSerializer.Serialize(creature));

        roundTripped.ShouldNotBeNull();
        roundTripped.Beams[0].NodeA.ShouldBe(10);
        roundTripped.Beams[0].NodeB.ShouldBe(20);
        roundTripped.Sensors[0].BeamId.ShouldBe(30);
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
            [new SensorDef(4, 3, SensorKind.Accelerometer, "Leg")],
            nextPartId: 5);

        creature.Nodes.Select(node => node.Name).ShouldBe(["Leg", "Leg"]);
        creature.Beams[0].NodeA.ShouldBe(1);
        creature.Sensors[0].BeamId.ShouldBe(3);
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
        var sensors = new[] { new SensorDef(4, 3, SensorKind.Accelerometer) };

        var creature = new CreatureDef(nodes, beams, sensors);
        nodes[0] = new NodeDef(10, new Vector2D(99, 99), 1);
        beams[0] = new BeamDef(11, 2, 1);
        sensors[0] = new SensorDef(12, 11, SensorKind.Accelerometer);

        creature.Nodes[0].Position.ShouldBe(new Vector2D(0, 0));
        creature.Beams[0].ShouldBe(new BeamDef(3, 1, 2));
        creature.Sensors[0].ShouldBe(new SensorDef(4, 3, SensorKind.Accelerometer));
    }
}
