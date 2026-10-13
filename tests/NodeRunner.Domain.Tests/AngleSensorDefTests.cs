using System.Text.Json;

namespace NodeRunner.Domain.Tests;

public sealed class AngleSensorDefTests
{
    private static readonly NodeDef[] _nodes = [new(1, new Vector2D(0, 0)), new(2, new Vector2D(100, 0)), new(3, new Vector2D(0, 100))];

    private static readonly BeamDef[] _beams = [new(4, 1, 2), new(5, 1, 3)];

    [Fact]
    public void Constructor_HasNoNameOrSides_ByDefault()
    {
        var sensor = new AngleSensorDef(6, 1);

        (sensor.Name, sensor.FixedLinkId, sensor.TargetLinkId).ShouldBe((null, null, null));
    }

    [Theory]
    [InlineData(0, 1, null, null)]
    [InlineData(6, 0, null, null)]
    [InlineData(6, 1, 0, null)]
    [InlineData(6, 1, null, -4)]
    public void Constructor_WithoutPositiveIds_Throws(int id, int nodeId, int? fixedLinkId, int? targetLinkId)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new AngleSensorDef(id, nodeId, fixedLinkId, targetLinkId));
    }

    [Fact]
    public void Constructor_WithTheSameSideTwice_Throws()
    {
        Should.Throw<ArgumentException>(() => new AngleSensorDef(6, 1, 4, 4));
    }

    [Fact]
    public void WithName_AndWithLinks_KeepItsIdAndTheRest()
    {
        var sensor = new AngleSensorDef(6, 1, 4, 5, "Knee");

        sensor.WithName("Hip").ShouldBe(new AngleSensorDef(6, 1, 4, 5, "Hip"));
        sensor.WithLinks(5, null).ShouldBe(new AngleSensorDef(6, 1, 5, null, "Knee"));
    }

    [Fact]
    public void CreatureDef_AcceptsMissingSidesAndItsJointsWheel_ButNotAnotherJointsLinksOrWheel()
    {
        WheelDef[] wheels = [new(7, 1), new(8, 2)];

        var creature = Creature([new AngleSensorDef(6, 1, null, 7)], wheels: wheels);

        creature.AngleSensors.ShouldBe([new AngleSensorDef(6, 1, null, 7)]);
        creature.AngleSensorIndexOf(6).ShouldBe(0);
        Should.Throw<ArgumentOutOfRangeException>(() => creature.AngleSensorIndexOf(7));
        Should.Throw<ArgumentException>(() => Creature([new AngleSensorDef(6, 1, 4, 8)], wheels: wheels));
        Should.Throw<ArgumentException>(() => Creature([new AngleSensorDef(6, 2, 4, 5)]));
        Should.Throw<ArgumentException>(() => Creature([new AngleSensorDef(6, 9, 4, 5)]));
        Should.Throw<ArgumentException>(() => Creature([new AngleSensorDef(6, 1, 4, 99)]));
    }

    // A joint holds one Servo, Motor, Brake or Angle sensor (#1044).
    [Fact]
    public void CreatureDef_SharesTheJointsMotorOrBrakeSlot_WithAServoAMotorABrakeAndAnotherAngleSensor()
    {
        AngleSensorDef[] sensor = [new AngleSensorDef(6, 1, 4, 5)];

        Should.Throw<ArgumentException>(() => Creature(sensor, servos: [new ServoDef(7, 1, 5, 4)])).Message.ShouldContain("motor or brake");
        Should.Throw<ArgumentException>(() => Creature(sensor, motors: [new VelocityMotorDef(7, 1, 5, 4)])).Message.ShouldContain("motor or brake");
        Should.Throw<ArgumentException>(() => Creature(sensor, brakes: [new BrakeDef(7, 1, 5, 4)])).Message.ShouldContain("motor or brake");
        Should.Throw<ArgumentException>(() => Creature([.. sensor, new AngleSensorDef(7, 1, 5, 4)])).Message.ShouldContain("motor or brake");
    }

    [Fact]
    public void CreatureDef_AcceptsIt_BesideAWheelARubberAndATouchSensor()
    {
        var creature = Creature(
            [new AngleSensorDef(6, 1, 4, 7)],
            wheels: [new WheelDef(7, 1)],
            touchSensors: [new TouchSensorDef(8, 1)],
            rubbers: [new RubberDef(9, 1)]);

        creature.AngleSensors.ShouldHaveSingleItem().NodeId.ShouldBe(1);
        CreatureDef.JointParts(1, creature.MotorsAndBrakes, creature.Wheels, creature.TouchSensors, creature.Rubbers).Select(part => part.Kind)
            .ShouldBe([CreatureElementKind.AngleSensor, CreatureElementKind.Wheel, CreatureElementKind.Rubber, CreatureElementKind.TouchSensor], ignoreOrder: true);
    }

    // It is no joint motor: it has inputs only, no torque path, and its joint keeps a plain joint's collider (#1204).
    [Fact]
    public void CreatureDef_CountsAnAngleSensorAsAPart_ButNotAsAJointMotor_AndKeepsItsJointsRadius()
    {
        var without = Creature([]);
        var with = Creature([new AngleSensorDef(6, 1, 4, 5)]);

        with.PartCount.ShouldBe(without.PartCount + 1);
        with.JointMotors.ShouldBeEmpty();
        with.NodeRadius(1).ShouldBe(without.NodeRadius(1));
        Should.Throw<ArgumentOutOfRangeException>(() => Creature([new AngleSensorDef(10, 1)]));
        Should.Throw<ArgumentException>(() => Creature([new AngleSensorDef(4, 1)]));
    }

    [Fact]
    public void MotorsAndBrakes_ListsTheJointMotors_TheBrakes_ThenTheAngleSensors()
    {
        var creature = Creature([new AngleSensorDef(6, 1, 4, 5)], servos: [new ServoDef(7, 2, 4)], brakes: [new BrakeDef(8, 3, 5)]);

        creature.MotorsAndBrakes.Select(part => (part.Kind, part.Id)).ShouldBe([(CreatureElementKind.Servo, 7), (CreatureElementKind.Brake, 8), (CreatureElementKind.AngleSensor, 6)]);
    }

    [Fact]
    public void HasJointPart_CountsAnAngleSensor()
    {
        CreatureDef.HasJointPart(1, [new AngleSensorDef(2, 1)], [], [], []).ShouldBeTrue();
        CreatureDef.HasJointPart(3, [new AngleSensorDef(2, 1)], [], [], []).ShouldBeFalse();
    }

    [Fact]
    public void JsonRoundTrip_PreservesAngleSensors()
    {
        var original = Creature([new AngleSensorDef(6, 1, 4, 7, "Knee"), new AngleSensorDef(8, 2, null, 4)], wheels: [new WheelDef(7, 1)]);

        var roundTripped = JsonSerializer.Deserialize<CreatureDef>(JsonSerializer.Serialize(original));

        roundTripped.ShouldNotBeNull();
        roundTripped.AngleSensors.ShouldBe(original.AngleSensors);
    }

    private static CreatureDef Creature(
        AngleSensorDef[] angleSensors,
        ServoDef[]? servos = null,
        VelocityMotorDef[]? motors = null,
        BrakeDef[]? brakes = null,
        WheelDef[]? wheels = null,
        TouchSensorDef[]? touchSensors = null,
        RubberDef[]? rubbers = null) =>
        new(_nodes, _beams, [], servos ?? [], [], [], wheels ?? [], touchSensors ?? [], rubbers ?? [], motors ?? [], brakes ?? [], angleSensors, nextPartId: 10);

    [Theory]
    [InlineData(false, false, 19)]
    [InlineData(true, false, 22)]
    [InlineData(false, true, 24)]
    [InlineData(true, true, 24)]
    public void ArcRadius_LiesAGapOutsideTheJointsOutermostPart(bool rubber, bool touch, double radius)
    {
        AngleSensorDef.ArcRadius(rubber, touch).ShouldBe(radius);
    }

    [Fact]
    public void ArcRadiusAt_LooksForARubberAndATouchSensorOnItsJoint()
    {
        AngleSensorDef.ArcRadiusAt(2, rubberNodes: [1, 2], touchSensorNodes: [3]).ShouldBe(22);
        AngleSensorDef.ArcRadiusAt(3, rubberNodes: [1, 2], touchSensorNodes: [3]).ShouldBe(24);
        AngleSensorDef.ArcRadiusAt(4, rubberNodes: [1, 2], touchSensorNodes: [3]).ShouldBe(19);
    }

    [Fact]
    public void Halo_LiesAGapOutsideTheArc_MarkedRoundItsOuterEdgeAsAJointsHalo()
    {
        var bare = AngleSensorDef.ArcRadius(rubber: false, touch: false);

        AngleSensorDef.MarkRadius(bare).ShouldBe(21);
        AngleSensorDef.HaloRadius(bare).ShouldBe(24);
        AngleSensorDef.HaloRadius(AngleSensorDef.ArcRadius(rubber: false, touch: true)).ShouldBe(29);
        (AngleSensorDef.HaloRadius(bare) - (AngleSensorDef.HaloWidth / 2)).ShouldBe(bare + (AngleSensorDef.ArcWidth / 2) + SelectionMarks.Gap);
    }
}
