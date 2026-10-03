using NodeRunner.App.Builders;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.Builders;

public sealed class CreatureBuilderTests
{
    [Fact]
    public void AddNode_ReturnsSequentialIds()
    {
        var builder = new CreatureBuilder();

        var first = builder.AddNode(new Vector2D(0, 0));
        var second = builder.AddNode(new Vector2D(2, 0));

        first.ShouldBe(1);
        second.ShouldBe(2);
        builder.Nodes.Count.ShouldBe(2);
    }

    [Fact]
    public void MoveNode_UpdatesPositionAndKeepsName()
    {
        var builder = new CreatureBuilder();
        var node = builder.AddNode(new Vector2D(0, 0));
        builder.Rename(node, "Knee");

        builder.MoveNode(node, new Vector2D(5, 7));

        builder.Nodes[builder.NodeIndexOf(node)].Position.ShouldBe(new Vector2D(5, 7));
        builder.Nodes[builder.NodeIndexOf(node)].Name.ShouldBe("Knee");
    }

    [Fact]
    public void AddBeam_BetweenDistinctNodes_ReturnsId()
    {
        var builder = new CreatureBuilder();
        var a = builder.AddNode(new Vector2D(0, 0));
        var b = builder.AddNode(new Vector2D(2, 0));

        var beamId = builder.AddBeam(a, b);

        beamId.ShouldBe(3);
        builder.Beams[0].NodeA.ShouldBe(a);
        builder.Beams[0].NodeB.ShouldBe(b);
    }

    [Fact]
    public void AddBeam_DuplicateInEitherOrder_Throws()
    {
        var builder = new CreatureBuilder();
        var a = builder.AddNode(new Vector2D(0, 0));
        var b = builder.AddNode(new Vector2D(2, 0));
        builder.AddBeam(a, b);

        var actionSameOrder = () => { builder.AddBeam(a, b); };
        var actionReversed = () => { builder.AddBeam(b, a); };

        actionSameOrder.ShouldThrow<ArgumentException>();
        actionReversed.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void AddSensor_OnExistingBeam_ReturnsId()
    {
        var builder = PairBuilder();
        var beam = builder.Beams[0].Id;

        var added = builder.AddSensor(beam, SensorKind.Accelerometer, out var sensorId, out var reason);

        added.ShouldBeTrue();
        sensorId.ShouldBe(4);
        reason.ShouldBeEmpty();
        builder.Sensors.ShouldBe([new SensorDef(4, beam, SensorKind.Accelerometer)]);
    }

    [Theory]
    [InlineData(SensorKind.Accelerometer, SensorKind.Accelerometer)]
    [InlineData(SensorKind.Accelerometer, SensorKind.Camera)]
    [InlineData(SensorKind.Camera, SensorKind.Accelerometer)]
    [InlineData(SensorKind.Camera, SensorKind.Camera)]
    public void AddSensor_OnABeamThatHasOne_ReturnsReasonAndDoesNotMutate(SensorKind first, SensorKind second)
    {
        var builder = PairBuilder();
        var beam = builder.Beams[0].Id;
        builder.AddSensor(beam, first, out var firstId, out _);

        var added = builder.AddSensor(beam, second, out var secondId, out var reason);

        added.ShouldBeFalse();
        secondId.ShouldBe(0);
        reason.ShouldBe("One sensor per beam");
        builder.Sensors.Select(sensor => (sensor.Id, sensor.BeamId, sensor.Kind)).ShouldBe([(firstId, beam, first)]);
    }

    [Fact]
    public void AddSensor_Camera_AimsAtTheWorldsForwardDownAsBuilt()
    {
        var builder = new CreatureBuilder();
        builder.AddNode(new Vector2D(0, 0));
        builder.AddNode(new Vector2D(0, 100));
        var beam = builder.AddBeam(1, 2);

        builder.AddSensor(beam, SensorKind.Camera, out _, out _);

        builder.Sensors[0].Aim.ShouldBe(CameraRays.DefaultAim(new Vector2D(0, 0), new Vector2D(0, 100)));
    }

    [Fact]
    public void SetCameraAim_IsKeptThroughSplitAndRename()
    {
        var builder = new CreatureBuilder();
        var a = builder.AddNode(new Vector2D(0, 0));
        var b = builder.AddNode(new Vector2D(100, 0));
        var joint = builder.AddNode(new Vector2D(30, 0));
        var beam = builder.AddBeam(a, b);
        builder.AddSensor(beam, SensorKind.Camera, out var sensor, out _);

        builder.SetCameraAim(sensor, 2);
        var split = builder.SplitBeamAtNode(beam, joint);
        builder.Rename(sensor, "Eye");

        builder.Sensors.ShouldBe([new SensorDef(sensor, split.SecondBeamId, SensorKind.Camera, "Eye", 2)]);
    }

    [Fact]
    public void SetCameraAim_OnAnAccelerometer_Throws()
    {
        var builder = PairBuilder();
        builder.AddSensor(builder.Beams[0].Id, SensorKind.Accelerometer, out var sensor, out _);

        Should.Throw<ArgumentOutOfRangeException>(() => builder.SetCameraAim(sensor, 1));
    }

    [Fact]
    public void RemoveSensor_RemovesOnlyThatSensor()
    {
        var builder = PairBuilder();
        builder.AddSensor(builder.Beams[0].Id, SensorKind.Accelerometer, out var sensorId, out _);

        builder.RemoveSensor(sensorId);

        builder.Sensors.ShouldBeEmpty();
    }

    [Fact]
    public void RemoveBeam_CascadesToSensorsOnThatBeam()
    {
        var builder = PairBuilder();
        var beam = builder.Beams[0].Id;
        builder.AddSensor(beam, SensorKind.Accelerometer, out _, out _);
        builder.AddSensor(beam, SensorKind.Camera, out _, out _);

        builder.RemoveBeam(beam);

        builder.Beams.ShouldBeEmpty();
        builder.Sensors.ShouldBeEmpty();
    }

    [Fact]
    public void RemoveNode_CascadesToAttachedBeamsAndSensors()
    {
        var builder = new CreatureBuilder();
        var a = builder.AddNode(new Vector2D(0, 0));
        var b = builder.AddNode(new Vector2D(2, 0));
        var c = builder.AddNode(new Vector2D(4, 0));
        var first = builder.AddBeam(a, b);
        var second = builder.AddBeam(b, c);
        builder.AddSensor(first, SensorKind.Accelerometer, out _, out _);
        builder.AddSensor(second, SensorKind.Accelerometer, out _, out _);

        builder.RemoveNode(b);

        builder.Nodes.Count.ShouldBe(2);
        builder.Beams.ShouldBeEmpty();
        builder.Sensors.ShouldBeEmpty();
    }

    [Fact]
    public void RemoveNode_KeepsSurvivingBeamAndSensorReferencesById()
    {
        var builder = new CreatureBuilder();
        var a = builder.AddNode(new Vector2D(0, 0));
        var b = builder.AddNode(new Vector2D(2, 0));
        var c = builder.AddNode(new Vector2D(4, 0));
        var beam = builder.AddBeam(b, c);
        builder.AddSensor(beam, SensorKind.Accelerometer, out var sensor, out _);

        builder.RemoveNode(a);

        builder.Nodes.Count.ShouldBe(2);
        builder.Beams[0].NodeA.ShouldBe(b);
        builder.Beams[0].NodeB.ShouldBe(c);
        builder.Sensors[0].ShouldBe(new SensorDef(sensor, beam, SensorKind.Accelerometer));
    }

    [Theory]
    [InlineData(30, false)]
    [InlineData(70, true)]
    public void SplitBeamAtNode_MovesSensorsToTheLongerHalf(double jointX, bool toFirstHalf)
    {
        var builder = new CreatureBuilder();
        var a = builder.AddNode(new Vector2D(0, 0));
        var b = builder.AddNode(new Vector2D(100, 0));
        var joint = builder.AddNode(new Vector2D(jointX, 0));
        var beam = builder.AddBeam(a, b);
        builder.AddSensor(beam, SensorKind.Accelerometer, out var sensor, out _);

        var split = builder.SplitBeamAtNode(beam, joint);

        builder.Sensors.Single().BeamId.ShouldBe(toFirstHalf ? split.FirstBeamId : split.SecondBeamId);
        builder.Sensors.Single().Id.ShouldBe(sensor);
    }

    [Fact]
    public void SplitBeamAtNode_TieMovesSensorsToNodeAHalfKeepingIds()
    {
        var builder = new CreatureBuilder();
        var a = builder.AddNode(new Vector2D(0, 0));
        var b = builder.AddNode(new Vector2D(100, 0));
        var joint = builder.AddNode(new Vector2D(50, 0));
        var beam = builder.AddBeam(a, b);
        builder.AddSensor(beam, SensorKind.Accelerometer, out var sensor, out _);

        var split = builder.SplitBeamAtNode(beam, joint);

        builder.Sensors.ShouldBe([new SensorDef(sensor, split.FirstBeamId, SensorKind.Accelerometer)]);
    }

    [Fact]
    public void Rename_ChangesOnlyTheMatchingPartName()
    {
        var builder = PairBuilder();
        var a = builder.Nodes[0].Id;
        var beam = builder.Beams[0].Id;
        builder.AddSensor(beam, SensorKind.Accelerometer, out var sensor, out _);

        builder.Rename(a, "Node");
        builder.Rename(beam, "Beam");
        builder.Rename(sensor, "Accelerometer");

        builder.Nodes[0].Name.ShouldBe("Node");
        builder.Beams[0].Name.ShouldBe("Beam");
        builder.Sensors[0].Name.ShouldBe("Accelerometer");
        builder.Sensors[0].BeamId.ShouldBe(beam);
    }

    [Fact]
    public void Constructor_FromCreature_KeepsIdsAndCounter()
    {
        var source = new CreatureDef(
            [new NodeDef(10, new Vector2D(0, 0)), new NodeDef(20, new Vector2D(2, 0))],
            [new BeamDef(30, 10, 20)],
            [new SensorDef(40, 30, SensorKind.Accelerometer)],
            nextPartId: 99);

        var builder = new CreatureBuilder(source);
        var next = builder.AddNode(new Vector2D(4, 0));

        builder.Build().Nodes.Take(2).ToArray().ShouldBe(source.Nodes.ToArray());
        builder.Build().Sensors.ToArray().ShouldBe(source.Sensors.ToArray());
        next.ShouldBe(99);
        builder.Build().NextPartId.ShouldBe(100);
    }

    [Fact]
    public void Build_WithUnfinishedDrawing_ReturnsItAsItStands()
    {
        var builder = new CreatureBuilder();
        builder.AddNode(new Vector2D(0, 0));
        builder.AddNode(new Vector2D(2, 0));

        var creature = builder.Build();

        creature.Nodes.Count.ShouldBe(2);
        creature.Beams.ShouldBeEmpty();
        creature.Sensors.ShouldBeEmpty();
    }

    [Fact]
    public void TryBuild_WithValidAnatomy_ReturnsCreatureDef()
    {
        var builder = PairBuilder();
        builder.AddSensor(builder.Beams[0].Id, SensorKind.Accelerometer, out _, out _);

        var succeeded = builder.TryBuild(out var creature, out var errors);

        succeeded.ShouldBeTrue();
        creature.ShouldNotBeNull();
        errors.ShouldBeEmpty();
        creature.Nodes.Count.ShouldBe(2);
        creature.Beams.Count.ShouldBe(1);
        creature.Sensors.Count.ShouldBe(1);
    }

    [Fact]
    public void TryBuild_MatchesHardcodedWormShape()
    {
        var builder = new CreatureBuilder();
        var previous = builder.AddNode(new Vector2D(0, 0));
        int? headBeam = null;
        for (var i = 1; i < 5; i++)
        {
            var next = builder.AddNode(new Vector2D(i * 90, 0));
            var beam = builder.AddBeam(previous, next);
            headBeam ??= beam;
            previous = next;
        }

        builder.AddSensor(headBeam!.Value, SensorKind.Accelerometer, out _, out _);

        var succeeded = builder.TryBuild(out var creature, out var errors);

        succeeded.ShouldBeTrue();
        creature.ShouldNotBeNull();
        errors.ShouldBeEmpty();
        creature.Nodes.Count.ShouldBe(5);
        creature.Beams.Count.ShouldBe(4);
        creature.Sensors.Count.ShouldBe(1);
    }

    private static CreatureBuilder PairBuilder()
    {
        var builder = new CreatureBuilder();
        var a = builder.AddNode(new Vector2D(0, 0));
        var b = builder.AddNode(new Vector2D(90, 0));
        builder.AddBeam(a, b);
        return builder;
    }
}
