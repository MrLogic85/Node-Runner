using NodeRunner.App.Builders;
using NodeRunner.App.ViewModels;
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
        reason.ShouldBeNull();
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
        reason.ShouldBe(UiText.Plain("One sensor per beam"));
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

        builder.Sensors[0].Aim.ShouldBe(SensorDef.DefaultAim(new Vector2D(0, 0), new Vector2D(0, 100)));
    }

    [Fact]
    public void Aim_IsKeptThroughRename()
    {
        var builder = new CreatureBuilder();
        var a = builder.AddNode(new Vector2D(0, 0));
        var b = builder.AddNode(new Vector2D(100, 0));
        var beam = builder.AddBeam(a, b);
        builder.AddSensor(beam, SensorKind.Camera, out var sensor, out _);

        builder.SetParameter(sensor, PartParameterId.Aim, 2);
        builder.Rename(sensor, "Eye");

        builder.Sensors.ShouldBe([new SensorDef(sensor, beam, SensorKind.Camera, "Eye", 2)]);
    }

    [Fact]
    public void Aim_OnAnAccelerometer_Throws()
    {
        var builder = PairBuilder();
        builder.AddSensor(builder.Beams[0].Id, SensorKind.Accelerometer, out var sensor, out _);

        builder.ParametersOf(sensor).ShouldBeEmpty();
        Should.Throw<ArgumentOutOfRangeException>(() => builder.SetParameter(sensor, PartParameterId.Aim, 1));
    }

    [Fact]
    public void ParametersOf_APiston_AreItsFive_AndSettingOneKeepsTheOthers()
    {
        var builder = PairBuilder();
        var far = builder.AddNode(new Vector2D(180, 0));
        var piston = builder.AddPiston(builder.Nodes[0].Id, far);

        builder.ParametersOf(piston).ShouldBe([PartParameterId.Strength, PartParameterId.Stroke, PartParameterId.StartPosition, PartParameterId.MaxSpeed, PartParameterId.RiseTime]);
        builder.ParametersOf(builder.Beams[0].Id).ShouldBeEmpty();
        builder.SetParameter(piston, PartParameterId.Stroke, 0.4);

        builder.Pistons.Single().ShouldBe(new PistonDef(piston, builder.Nodes[0].Id, far, stroke: 0.4));
        builder.ParameterValue(piston, PartParameterId.Stroke).ShouldBe(0.4);
        builder.SetParameter(piston, PartParameterId.RiseTime, 0.5);
        builder.Pistons.Single().ShouldBe(new PistonDef(piston, builder.Nodes[0].Id, far, stroke: 0.4, riseTime: 0.5));
        builder.ParameterValue(piston, PartParameterId.RiseTime).ShouldBe(0.5);
        builder.SetParameter(piston, PartParameterId.StartPosition, 0);
        builder.Pistons.Single().ShouldBe(new PistonDef(piston, builder.Nodes[0].Id, far, stroke: 0.4, riseTime: 0.5, start: 0));
        builder.ParameterValue(piston, PartParameterId.StartPosition).ShouldBe(0);
    }

    [Fact]
    public void ParametersOf_AServo_AreItsFive_WithAStartPositionOfItsOwn()
    {
        var builder = PairBuilder();
        var a = builder.Nodes[0].Id;
        builder.AddBeam(a, builder.AddNode(new Vector2D(0, 90)));
        var servo = builder.AddServo(a);

        builder.ParametersOf(servo).ShouldBe([PartParameterId.ServoStrength, PartParameterId.Range, PartParameterId.ServoStartPosition, PartParameterId.AngularMaxSpeed, PartParameterId.RiseTime]);
        builder.SetParameter(servo, PartParameterId.ServoStartPosition, 0.25);
        builder.Servos.Single().Start.ShouldBe(0.25);
        builder.ParameterValue(servo, PartParameterId.ServoStartPosition).ShouldBe(0.25);
        Should.Throw<ArgumentOutOfRangeException>(() => builder.SetParameter(servo, PartParameterId.StartPosition, 0.5));
    }

    [Fact]
    public void AddServo_WithGivenLinks_KeepsThem_AndRefusesALinkElsewhere()
    {
        var builder = PairBuilder();
        var a = builder.Nodes[0].Id;
        var second = builder.AddBeam(a, builder.AddNode(new Vector2D(0, 90)));
        var elsewhere = builder.AddBeam(builder.AddNode(new Vector2D(300, 0)), builder.AddNode(new Vector2D(400, 0)));

        Should.Throw<ArgumentException>(() => builder.AddServo(a, elsewhere, null));
        var servo = builder.AddServo(a, null, second);

        builder.Servos.Single().ShouldBe(new ServoDef(servo, a, null, second));
    }

    [Fact]
    public void ParametersOf_ASpring_AreItsFourSettings_AndSettingOneKeepsTheOthers()
    {
        var builder = PairBuilder();
        var far = builder.AddNode(new Vector2D(180, 0));
        var spring = builder.AddSpring(builder.Nodes[0].Id, far);

        builder.ParametersOf(spring).ShouldBe(
            [PartParameterId.Stiffness, PartParameterId.Damping, PartParameterId.Stroke, PartParameterId.CoilLength]);
        builder.SetParameter(spring, PartParameterId.Damping, 0.6);
        builder.SetParameter(spring, PartParameterId.Stroke, 0.3);
        builder.SetParameter(spring, PartParameterId.CoilLength, 0.2);

        builder.Springs.Single().ShouldBe(new SpringDef(spring, builder.Nodes[0].Id, far, damping: 0.6, stroke: 0.3, coilLength: 0.2));
        builder.ParameterValue(spring, PartParameterId.Stiffness).ShouldBe(SpringDef.DefaultStiffness);
        builder.ParameterValue(spring, PartParameterId.Stroke).ShouldBe(0.3);
        builder.ParameterValue(spring, PartParameterId.CoilLength).ShouldBe(0.2);
        builder.Nodes.Single(node => node.Id == far).Position.ShouldBe(new Vector2D(180, 0));
    }

    [Fact]
    public void ALink_OnAPairThatHasOne_IsRefusedWithWhy()
    {
        var builder = PairBuilder();
        var (a, b) = (builder.Nodes[0].Id, builder.Nodes[1].Id);
        var c = builder.AddNode(new Vector2D(0, 90));
        var d = builder.AddNode(new Vector2D(90, 90));
        builder.AddSpring(a, c);
        builder.AddPiston(b, d);

        builder.CanAddSpring(c, a, out var onSpring).ShouldBeFalse();
        onSpring.ShouldBe(CreatureBuilder.SpringJoinsTheseNodesReason);
        builder.CanAddPiston(a, c, out var pistonOnSpring).ShouldBeFalse();
        pistonOnSpring.ShouldBe(CreatureBuilder.SpringJoinsTheseNodesReason);
        builder.CanAddSpring(b, d, out var onPiston).ShouldBeFalse();
        onPiston.ShouldBe(CreatureBuilder.PistonJoinsTheseNodesReason);
        builder.CanAddBeam(a, c, out var beamOnSpring).ShouldBeFalse();
        beamOnSpring.ShouldBe(CreatureBuilder.SpringJoinsTheseNodesReason);
        Should.Throw<ArgumentException>(() => builder.AddBeam(a, c));
        builder.CanAddSpring(a, a, out _).ShouldBeFalse();
        builder.CanAddSpring(c, d, out _).ShouldBeTrue();
    }

    [Fact]
    public void RemoveNode_RemovesItsSprings_AndRenameNamesOne()
    {
        var builder = PairBuilder();
        var (a, b) = (builder.Nodes[0].Id, builder.Nodes[1].Id);
        var c = builder.AddNode(new Vector2D(0, 90));
        var kept = builder.AddSpring(a, c);
        var removed = builder.AddSpring(b, c);
        builder.Rename(kept, "Tail");

        builder.RemoveNode(b);

        builder.Springs.ShouldBe([new SpringDef(kept, a, c, "Tail")]);
        builder.Build().Springs.ShouldBe(builder.Springs);
        Should.Throw<ArgumentOutOfRangeException>(() => builder.SpringIndexOf(removed));
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

    [Fact]
    public void APistonOrSpring_OnABeamsPair_ReplacesTheBeam()
    {
        var pistonBuilder = PairBuilder();
        var (a, b) = (pistonBuilder.Nodes[0].Id, pistonBuilder.Nodes[1].Id);
        pistonBuilder.BeamBetween(b, a).ShouldBe(pistonBuilder.Beams[0].Id);
        pistonBuilder.CanAddPiston(a, b, out var reason).ShouldBeTrue();
        reason.ShouldBeNull();

        var piston = pistonBuilder.AddPiston(b, a);

        pistonBuilder.Beams.ShouldBeEmpty();
        pistonBuilder.Pistons.ShouldBe([new PistonDef(piston, b, a)]);
        pistonBuilder.BeamBetween(a, b).ShouldBeNull();

        var springBuilder = PairBuilder();
        var spring = springBuilder.AddSpring(a, b);

        springBuilder.Beams.ShouldBeEmpty();
        springBuilder.Springs.Single().Id.ShouldBe(spring);
    }

    [Fact]
    public void APistonOrSpring_OnASensorsBeam_IsRefusedWithWhy()
    {
        var builder = PairBuilder();
        var (a, b) = (builder.Nodes[0].Id, builder.Nodes[1].Id);
        builder.AddSensor(builder.Beams[0].Id, SensorKind.Accelerometer, out _, out _);

        builder.CanAddPiston(a, b, out var piston).ShouldBeFalse();
        piston.ShouldBe(CreatureBuilder.SensorSitsOnThisBeamReason);
        builder.CanAddSpring(b, a, out var spring).ShouldBeFalse();
        spring.ShouldBe(CreatureBuilder.SensorSitsOnThisBeamReason);
        builder.CanAddBeam(a, b, out var beam).ShouldBeFalse();
        beam.ShouldBe(CreatureBuilder.BeamJoinsTheseNodesReason);
        Should.Throw<ArgumentException>(() => builder.AddPiston(a, b));
        builder.Beams.Count.ShouldBe(1);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ALinkReplacingAServosBeam_TakesItsRole_UnderANewServoId(bool replaceFixed)
    {
        var builder = PairBuilder();
        var (a, b) = (builder.Nodes[0].Id, builder.Nodes[1].Id);
        var c = builder.AddNode(new Vector2D(0, 90));
        var first = builder.Beams[0].Id;
        var second = builder.AddBeam(a, c);
        var servo = builder.AddServo(a);
        builder.Servos.Single().ShouldBe(new ServoDef(servo, a, first, second));

        var spring = replaceFixed ? builder.AddSpring(a, b) : builder.AddSpring(c, a);

        var moved = builder.Servos.Single();
        moved.Id.ShouldNotBe(servo);
        moved.Id.ShouldBeGreaterThan(spring);
        moved.NodeId.ShouldBe(a);
        (moved.FixedLinkId, moved.TargetLinkId).ShouldBe(replaceFixed ? (spring, second) : (first, spring));
    }

    [Fact]
    public void MoveSensor_ToAFreeBeam_KeepsItsIdKindAndName()
    {
        var builder = TwoBeams(out var from, out var to);
        builder.AddSensor(from, SensorKind.Accelerometer, out var sensor, out _);
        builder.Rename(sensor, "Tilt");

        var moved = builder.MoveSensor(sensor, to, out var reason);

        moved.ShouldBeTrue();
        reason.ShouldBeNull();
        builder.Sensors.ShouldBe([new SensorDef(sensor, to, SensorKind.Accelerometer, "Tilt")]);
    }

    [Fact]
    public void MoveSensor_ToABeamThatHasOne_ReturnsReasonAndDoesNotMutate()
    {
        var builder = TwoBeams(out var from, out var to);
        builder.AddSensor(from, SensorKind.Accelerometer, out var sensor, out _);
        builder.AddSensor(to, SensorKind.Camera, out _, out _);
        var before = builder.Sensors.ToList();

        var moved = builder.MoveSensor(sensor, to, out var reason);

        moved.ShouldBeFalse();
        reason.ShouldBe(UiText.Plain("One sensor per beam"));
        builder.Sensors.ShouldBe(before);
    }

    [Theory]
    [InlineData(false, 0.3 - (Math.PI / 2))]
    [InlineData(true, 0.3 + (Math.PI / 2))]
    public void MoveSensor_Camera_KeepsTheWorldDirectionItLooksIn(bool fromTheDownBeam, double expected)
    {
        // Level → down turns the beam by +π/2 and down → level by −π/2, so both beams' angles count.
        var builder = TwoBeams(out var level, out var down);
        var (from, to) = fromTheDownBeam ? (down, level) : (level, down);
        builder.AddSensor(from, SensorKind.Camera, out var camera, out _);
        builder.SetParameter(camera, PartParameterId.Aim, 0.3);

        builder.MoveSensor(camera, to, out _).ShouldBeTrue();

        builder.Sensors.Single().Aim!.Value.ShouldBe(expected, 1e-9);
    }

    [Fact]
    public void MoveSensor_ToItsOwnBeam_ChangesNothing()
    {
        var builder = TwoBeams(out var from, out _);
        builder.AddSensor(from, SensorKind.Camera, out var camera, out _);
        builder.SetParameter(camera, PartParameterId.Aim, 0.3);
        var before = builder.Sensors.ToList();

        builder.MoveSensor(camera, from, out _).ShouldBeTrue();

        builder.Sensors.ShouldBe(before);
    }

    // A level beam from (0,0) to (90,0), and one from (90,0) straight down to (90,90).
    private static CreatureBuilder TwoBeams(out int level, out int down)
    {
        var builder = PairBuilder();
        level = builder.Beams[0].Id;
        var end = builder.AddNode(new Vector2D(90, 90));
        down = builder.AddBeam(builder.Beams[0].NodeB, end);
        return builder;
    }

    [Fact]
    public void AddWheel_OnAJoint_AddsADefaultWheel_ThatGrowsTheJoint()
    {
        var builder = PairBuilder();
        var joint = builder.Nodes[0].Id;
        var freshId = builder.Build().NextPartId;

        var wheel = builder.AddWheel(joint);

        wheel.ShouldBe(freshId);
        builder.Wheels.ShouldBe([new WheelDef(wheel, joint)]);
        builder.NodeRadius(joint).ShouldBe(WheelDef.DefaultRadius);
        builder.Build().Wheels.ShouldBe(builder.Wheels);
    }

    [Fact]
    public void CanAddWheel_RefusesANonJoint_ASecondWheel_AndAJointWithAnotherPart()
    {
        var builder = PairBuilder();
        var withWheel = builder.Nodes[0].Id;
        var withServo = builder.Nodes[1].Id;
        builder.AddWheel(withWheel);
        builder.AddServo(withServo);

        builder.CanAddWheel(builder.Beams[0].Id, out var notAJoint).ShouldBeFalse();
        builder.CanAddWheel(withWheel, out var secondWheel).ShouldBeFalse();
        builder.CanAddWheel(withServo, out var servoThere).ShouldBeFalse();
        builder.CanAddServo(withWheel, out var wheelThere).ShouldBeFalse();

        notAJoint.ShouldBe(UiText.Plain("Wheels go on a joint"));
        secondWheel.ShouldBe(UiText.Plain("One wheel per joint"));
        servoThere.ShouldBe(CreatureBuilder.OnePartPerJointReason);
        wheelThere.ShouldBe(CreatureBuilder.OnePartPerJointReason);
        Should.Throw<ArgumentException>(() => builder.AddWheel(withWheel));
    }

    [Fact]
    public void ParametersOf_AWheel_AreItsRadiusAndGrip_InWorldUnitsAndAShare()
    {
        var builder = PairBuilder();
        var wheel = builder.AddWheel(builder.Nodes[0].Id);

        builder.ParametersOf(wheel).ShouldBe([PartParameterId.WheelRadius, PartParameterId.Grip]);
        builder.SetParameter(wheel, PartParameterId.WheelRadius, 100);
        builder.SetParameter(wheel, PartParameterId.Grip, 0.3);

        builder.Wheels.Single().Radius.ShouldBe(100);
        builder.Wheels.Single().Grip.ShouldBe(0.3);
        builder.ParameterValue(wheel, PartParameterId.WheelRadius).ShouldBe(100);
        builder.ParameterValue(wheel, PartParameterId.Grip).ShouldBe(0.3);
        builder.NodeRadius(builder.Nodes[0].Id).ShouldBe(100);
        Should.Throw<ArgumentOutOfRangeException>(() => builder.SetParameter(wheel, PartParameterId.Stroke, 0.5));
    }

    [Fact]
    public void RemoveNode_TakesItsWheel_AndRemoveWheel_LeavesTheJoint()
    {
        var builder = PairBuilder();
        var first = builder.Nodes[0].Id;
        var second = builder.Nodes[1].Id;
        builder.AddWheel(first);
        var kept = builder.AddWheel(second);

        builder.RemoveNode(first);
        builder.Wheels.Select(wheel => wheel.Id).ShouldBe([kept]);
        builder.RemoveWheel(kept);

        builder.Wheels.ShouldBeEmpty();
        builder.Nodes.Select(node => node.Id).ShouldBe([second]);
    }

    [Fact]
    public void Rename_AWheel_KeepsItsSettings()
    {
        var builder = PairBuilder();
        var wheel = builder.AddWheel(builder.Nodes[0].Id);
        builder.SetParameter(wheel, PartParameterId.Grip, 0.5);

        builder.Rename(wheel, "Front");

        builder.Wheels.Single().ShouldBe(new WheelDef(wheel, builder.Nodes[0].Id, "Front", grip: 0.5));
    }

    [Fact]
    public void Constructor_FromACreatureWithAWheel_KeepsIt()
    {
        var creature = new CreatureDef([new NodeDef(1, new Vector2D(0, 0))], [], [], [], [], [], [new WheelDef(2, 1, radius: 70)], nextPartId: 3);

        var builder = new CreatureBuilder(creature);

        builder.Wheels.ShouldBe(creature.Wheels);
        builder.Build().Wheels.ShouldBe(creature.Wheels);
        builder.Build().NextPartId.ShouldBe(3);
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
