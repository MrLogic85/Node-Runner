using System.Text.Json;

namespace NodeRunner.Domain.Tests;

public sealed class TouchSensorDefTests
{
    private static readonly NodeDef[] _nodes = [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(100, 0)), new NodeDef(3, new Vector2D(0, 100))];
    private static readonly BeamDef[] _beams = [new BeamDef(4, 1, 2), new BeamDef(5, 1, 3)];

    [Theory]
    [InlineData(0, 1)]
    [InlineData(6, 0)]
    public void Constructor_WithoutPositiveIds_Throws(int id, int nodeId)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new TouchSensorDef(id, nodeId));
    }

    [Fact]
    public void WithName_KeepsItsIdAndJoint()
    {
        new TouchSensorDef(6, 1).WithName("Foot").ShouldBe(new TouchSensorDef(6, 1, "Foot"));
    }

    [Fact]
    public void CreatureDef_TakesOneTouchSensorPerJoint_OnAJointThatExists_WithItsOwnId()
    {
        var creature = Creature(touchSensors: [new TouchSensorDef(6, 1)], nextPartId: 7);

        creature.TouchSensors.ShouldBe([new TouchSensorDef(6, 1)]);
        creature.TouchSensorIndexOf(6).ShouldBe(0);
        Should.Throw<ArgumentOutOfRangeException>(() => creature.TouchSensorIndexOf(4));
        Should.Throw<ArgumentException>(() => Creature(touchSensors: [new TouchSensorDef(6, 9)], nextPartId: 7));
        Should.Throw<ArgumentException>(() => Creature(touchSensors: [new TouchSensorDef(6, 1), new TouchSensorDef(7, 1)], nextPartId: 8));
        Should.Throw<ArgumentException>(() => Creature(touchSensors: [new TouchSensorDef(4, 2)], nextPartId: 7));
        Should.Throw<ArgumentOutOfRangeException>(() => Creature(touchSensors: [new TouchSensorDef(7, 2)], nextPartId: 7));
    }

    // It takes no joint-part slot (#1043): it shares its joint with a Servo and a Wheel.
    [Fact]
    public void CreatureDef_TakesATouchSensorNextToAServoAndAWheel()
    {
        var stacked = Creature([new ServoDef(6, 1, 4, 5)], [new WheelDef(7, 1)], [new TouchSensorDef(8, 1)], nextPartId: 9);

        stacked.TouchSensors.ShouldHaveSingleItem().NodeId.ShouldBe(1);
    }

    [Fact]
    public void CreatureDef_CountsTouchSensorsAsParts()
    {
        var without = Creature(nextPartId: 7);
        var with = Creature(touchSensors: [new TouchSensorDef(6, 2)], nextPartId: 7);

        with.PartCount.ShouldBe(without.PartCount + 1);
    }

    // It adds no radius (#1043): the joint's size, collider and covered edge are its other parts'.
    [Fact]
    public void ATouchSensor_LeavesItsJointsRadiiUnchanged()
    {
        var plain = Creature([new ServoDef(6, 1, 4, 5)], [], [], nextPartId: 9);
        var touched = Creature([new ServoDef(6, 1, 4, 5)], [], [new TouchSensorDef(7, 1), new TouchSensorDef(8, 2)], nextPartId: 9);

        foreach (var node in _nodes)
        {
            touched.NodeRadius(node.Id).ShouldBe(plain.NodeRadius(node.Id));
            touched.CoveredRadius(node.Id).ShouldBe(plain.CoveredRadius(node.Id));
        }

        touched.NodeRadius(1).ShouldBe(ServoDef.JointRadius);
        touched.NodeRadius(2).ShouldBe(NodeDef.PlainJointRadius);
    }

    [Fact]
    public void JsonRoundTrip_PreservesTouchSensors()
    {
        var original = Creature(touchSensors: [new TouchSensorDef(6, 2, "Foot")], nextPartId: 7);

        var roundTripped = JsonSerializer.Deserialize<CreatureDef>(JsonSerializer.Serialize(original));

        roundTripped.ShouldNotBeNull();
        roundTripped.TouchSensors.ShouldBe(original.TouchSensors);
    }

    private static CreatureDef Creature(ServoDef[]? servos = null, WheelDef[]? wheels = null, TouchSensorDef[]? touchSensors = null, int nextPartId = 7) =>
        new(_nodes, _beams, [], servos ?? [], [], [], wheels ?? [], touchSensors ?? [], nextPartId);
}
