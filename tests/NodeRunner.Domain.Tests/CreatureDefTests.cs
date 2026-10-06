using System.Text.Json;

namespace NodeRunner.Domain.Tests;

public sealed class CreatureDefTests
{
    [Fact]
    public void Constructor_WithValidAnatomy_StoresParts()
    {
        var nodes = new[]
        {
            new NodeDef(1, new Vector2D(0, 0)),
            new NodeDef(2, new Vector2D(2, 0)),
        };
        var beams = new[] { new BeamDef(3, 1, 2) };
        var sensors = new[] { new SensorDef(4, 3, SensorKind.Accelerometer) };

        var creature = new CreatureDef(nodes, beams, sensors);

        creature.Nodes.ToArray().ShouldBe(nodes);
        creature.Beams.ToArray().ShouldBe(beams);
        creature.Sensors.ToArray().ShouldBe(sensors);
    }

    [Fact]
    public void PartCount_CountsEveryKindOfPart()
    {
        var creature = new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(2, 0)), new NodeDef(3, new Vector2D(1, 1))],
            [new BeamDef(4, 1, 2), new BeamDef(5, 2, 3)],
            [new SensorDef(6, 4, SensorKind.Accelerometer)],
            [new PistonDef(7, 1, 3)],
            [new SpringDef(8, 1, 3)],
            nextPartId: 9);

        creature.PartCount.ShouldBe(8);
        new CreatureDef([], [], []).PartCount.ShouldBe(0);
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
                new NodeDef(1, new Vector2D(0, 0)),
                new NodeDef(2, new Vector2D(0, 0)),
                new NodeDef(3, new Vector2D(4, 0)),
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
                new NodeDef(1, new Vector2D(0, 0)),
                new NodeDef(2, new Vector2D(2, 0)),
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
                new NodeDef(1, new Vector2D(0, 0)),
                new NodeDef(2, new Vector2D(2, 0)),
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
                new NodeDef(1, new Vector2D(0, 0)),
                new NodeDef(2, new Vector2D(2, 0)),
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
    public void Constructor_WithTwoSensorKindsOnOneBeam_Throws()
    {
        var action = () => new CreatureDef(
            new[]
            {
                new NodeDef(1, new Vector2D(0, 0)),
                new NodeDef(2, new Vector2D(2, 0)),
            },
            new[] { new BeamDef(101, 1, 2) },
            new[]
            {
                new SensorDef(201, 101, SensorKind.Accelerometer),
                new SensorDef(202, 101, SensorKind.Camera),
            });

        action.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithCameraWithoutAim_GivesItTheDefaultAimFromItsBeam()
    {
        var creature = new CreatureDef(
            new[] { new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(0, 2)) },
            new[] { new BeamDef(101, 1, 2) },
            new[] { new SensorDef(201, 101, SensorKind.Camera) });

        creature.Sensors[0].Aim.ShouldBe(SensorDef.DefaultAim(new Vector2D(0, 0), new Vector2D(0, 2)));
    }

    [Fact]
    public void Constructor_KeepsAGivenAimAndLeavesAccelerometersWithout()
    {
        var creature = new CreatureDef(
            new[] { new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(2, 0)), new NodeDef(3, new Vector2D(4, 0)) },
            new[] { new BeamDef(101, 1, 2), new BeamDef(102, 2, 3) },
            new[] { new SensorDef(201, 101, SensorKind.Camera, aim: 2), new SensorDef(202, 102, SensorKind.Accelerometer) });

        creature.Sensors[0].Aim.ShouldBe(2);
        creature.Sensors[1].Aim.ShouldBeNull();
    }

    [Fact]
    public void JsonRoundTrip_PreservesCreatureDefinition()
    {
        var original = new CreatureDef(
            new[]
            {
                new NodeDef(1, new Vector2D(0, 0)),
                new NodeDef(2, new Vector2D(2, 0)),
                new NodeDef(3, new Vector2D(4, 0)),
            },
            new[] { new BeamDef(101, 1, 2), new BeamDef(102, 2, 3) },
            new[]
            {
                new SensorDef(201, 101, SensorKind.Accelerometer),
                new SensorDef(202, 102, SensorKind.Camera, aim: -0.75),
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
    public void Constructor_WithAPistonToAMissingNode_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(1, 0))],
            [],
            [],
            [new PistonDef(3, 1, 9)]));
    }

    [Fact]
    public void JsonRoundTrip_PreservesPistons()
    {
        var original = new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(1, 0))],
            [],
            [],
            [new PistonDef(3, 1, 2, "Ram", 20000, 0.4, 150)]);

        var roundTripped = JsonSerializer.Deserialize<CreatureDef>(JsonSerializer.Serialize(original));

        roundTripped.ShouldNotBeNull();
        roundTripped.Pistons.ToArray().ShouldBe(original.Pistons.ToArray());
        roundTripped.PistonIndexOf(3).ShouldBe(0);
        roundTripped.NextPartId.ShouldBe(4);
    }

    [Fact]
    public void Constructor_WithASpringToAMissingNode_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(1, 0))],
            [],
            [],
            [],
            [new SpringDef(3, 1, 9)],
            nextPartId: 4));
    }

    [Fact]
    public void Constructor_WithASpringSharingAPistonsId_Throws()
    {
        Should.Throw<ArgumentException>(() => new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(1, 0)), new NodeDef(3, new Vector2D(0, 1))],
            [],
            [],
            [new PistonDef(4, 1, 2)],
            [new SpringDef(4, 2, 3)],
            nextPartId: 5));
    }

    [Fact]
    public void JsonRoundTrip_PreservesSprings()
    {
        var original = new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(1, 0))],
            [],
            [],
            [],
            [new SpringDef(3, 1, 2, "Tail", 800, 0.6)],
            nextPartId: 4);

        var roundTripped = JsonSerializer.Deserialize<CreatureDef>(JsonSerializer.Serialize(original));

        roundTripped.ShouldNotBeNull();
        roundTripped.Springs.ToArray().ShouldBe(original.Springs.ToArray());
        roundTripped.SpringIndexOf(3).ShouldBe(0);
        roundTripped.NextPartId.ShouldBe(4);
    }

    [Fact]
    public void JsonRoundTrip_PreservesServos()
    {
        var original = new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(1, 0)), new NodeDef(3, new Vector2D(0, 1))],
            [new BeamDef(4, 1, 2), new BeamDef(5, 1, 3)],
            [],
            [new ServoDef(6, 1, null, 5, "Hip", strength: 7, range: Math.PI / 2, start: 0.25, maxSpeed: 3, riseTime: 0.4)],
            [],
            [],
            nextPartId: 7);

        var roundTripped = JsonSerializer.Deserialize<CreatureDef>(JsonSerializer.Serialize(original));

        roundTripped.ShouldNotBeNull();
        roundTripped.Servos.ToArray().ShouldBe(original.Servos.ToArray());
        roundTripped.ServoIndexOf(6).ShouldBe(0);
        roundTripped.NextPartId.ShouldBe(7);
    }

    [Fact]
    public void Constructor_WithANullPart_Throws()
    {
        NodeDef[] nodes = [new NodeDef(1, new Vector2D(0, 0)), null!];

        Should.Throw<ArgumentException>(() => new CreatureDef(nodes, [], []));
    }

    [Fact]
    public void Constructor_WithOmittedNextPartId_DefaultsToMaxIdPlusOne()
    {
        var creature = new CreatureDef(
            [new NodeDef(7, new Vector2D(0, 0)), new NodeDef(3, new Vector2D(1, 0))],
            [new BeamDef(12, 7, 3)],
            []);

        creature.NextPartId.ShouldBe(13);
    }

    [Fact]
    public void Lookups_UseIdsSoReferencesSurviveReorderedLists()
    {
        var creature = new CreatureDef(
            [new NodeDef(20, new Vector2D(2, 0)), new NodeDef(10, new Vector2D(0, 0))],
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
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(1, 0))],
            [new BeamDef(1, 1, 2)],
            []);

        action.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithPartIdAtOrBeyondNextPartId_Throws()
    {
        var action = () => new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(1, 0))],
            [new BeamDef(3, 1, 2)],
            [],
            nextPartId: 3);

        action.ShouldThrow<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Names_AreDisplayMetadataAndDoNotAffectReferences()
    {
        var creature = new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0), "Leg"), new NodeDef(2, new Vector2D(1, 0), "Leg")],
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
            new NodeDef(1, new Vector2D(0, 0)),
            new NodeDef(2, new Vector2D(2, 0)),
        };
        var beams = new[] { new BeamDef(3, 1, 2) };
        var sensors = new[] { new SensorDef(4, 3, SensorKind.Accelerometer) };

        var creature = new CreatureDef(nodes, beams, sensors);
        nodes[0] = new NodeDef(10, new Vector2D(99, 99));
        beams[0] = new BeamDef(11, 2, 1);
        sensors[0] = new SensorDef(12, 11, SensorKind.Accelerometer);

        creature.Nodes[0].Position.ShouldBe(new Vector2D(0, 0));
        creature.Beams[0].ShouldBe(new BeamDef(3, 1, 2));
        creature.Sensors[0].ShouldBe(new SensorDef(4, 3, SensorKind.Accelerometer));
    }
}
