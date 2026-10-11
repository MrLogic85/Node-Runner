using System.Text.Json;

namespace NodeRunner.Domain.Tests;

public sealed class RubberDefTests
{
    private static readonly NodeDef[] _nodes = [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(100, 0)), new NodeDef(3, new Vector2D(0, 100))];
    private static readonly BeamDef[] _beams = [new BeamDef(4, 1, 2), new BeamDef(5, 1, 3)];

    [Theory]
    [InlineData(0, 1)]
    [InlineData(6, 0)]
    public void Constructor_WithoutPositiveIds_Throws(int id, int nodeId)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new RubberDef(id, nodeId));
    }

    [Fact]
    public void WithName_KeepsItsIdAndJoint()
    {
        new RubberDef(6, 1).WithName("Sole").ShouldBe(new RubberDef(6, 1, "Sole"));
    }

    [Fact]
    public void CreatureDef_TakesOneRubberPerJoint_OnAJointThatExists_WithItsOwnId()
    {
        var creature = Creature(rubbers: [new RubberDef(6, 1)], nextPartId: 7);

        creature.Rubbers.ShouldBe([new RubberDef(6, 1)]);
        creature.RubberIndexOf(6).ShouldBe(0);
        Should.Throw<ArgumentOutOfRangeException>(() => creature.RubberIndexOf(4));
        Should.Throw<ArgumentException>(() => Creature(rubbers: [new RubberDef(6, 9)], nextPartId: 7));
        Should.Throw<ArgumentException>(() => Creature(rubbers: [new RubberDef(6, 1), new RubberDef(7, 1)], nextPartId: 8));
        Should.Throw<ArgumentException>(() => Creature(rubbers: [new RubberDef(4, 2)], nextPartId: 7));
        Should.Throw<ArgumentOutOfRangeException>(() => Creature(rubbers: [new RubberDef(7, 2)], nextPartId: 7));
    }

    // It has its own joint-part slot (#1142): it shares its joint with a Servo, a Wheel and a Touch sensor.
    [Fact]
    public void CreatureDef_TakesARubberNextToAServoAWheelAndATouchSensor()
    {
        var stacked = Creature([new ServoDef(6, 1, 4, 5)], [new WheelDef(7, 1)], [new TouchSensorDef(8, 1)], [new RubberDef(9, 1)], nextPartId: 10);

        stacked.Rubbers.ShouldHaveSingleItem().NodeId.ShouldBe(1);
    }

    [Fact]
    public void CreatureDef_CountsRubbersAsParts()
    {
        var without = Creature(nextPartId: 7);
        var with = Creature(rubbers: [new RubberDef(6, 2)], nextPartId: 7);

        with.PartCount.ShouldBe(without.PartCount + 1);
    }

    // It adds no radius (#1142): the joint's size, collider and covered edge are its other parts'.
    [Fact]
    public void ARubber_LeavesItsJointsRadiiUnchanged()
    {
        var plain = Creature([new ServoDef(6, 1, 4, 5)], [], nextPartId: 9);
        var rubbered = Creature([new ServoDef(6, 1, 4, 5)], [], rubbers: [new RubberDef(7, 1), new RubberDef(8, 2)], nextPartId: 9);

        foreach (var node in _nodes)
        {
            rubbered.NodeRadius(node.Id).ShouldBe(plain.NodeRadius(node.Id));
            rubbered.CoveredRadius(node.Id).ShouldBe(plain.CoveredRadius(node.Id));
        }

        rubbered.NodeRadius(1).ShouldBe(ServoDef.JointRadius);
        rubbered.NodeRadius(2).ShouldBe(NodeDef.PlainJointRadius);
    }

    [Fact]
    public void JsonRoundTrip_PreservesRubbers()
    {
        var original = Creature(rubbers: [new RubberDef(6, 2, "Sole")], nextPartId: 7);

        var roundTripped = JsonSerializer.Deserialize<CreatureDef>(JsonSerializer.Serialize(original));

        roundTripped.ShouldNotBeNull();
        roundTripped.Rubbers.ShouldBe(original.Rubbers);
    }

    [Fact]
    public void TheOlderConstructor_HasNoRubbers()
    {
        new CreatureDef(_nodes, _beams, [], [], [], [], [], [], nextPartId: 6).Rubbers.ShouldBeEmpty();
    }

    private static CreatureDef Creature(ServoDef[]? servos = null, WheelDef[]? wheels = null, TouchSensorDef[]? touchSensors = null, RubberDef[]? rubbers = null, int nextPartId = 7) =>
        new(_nodes, _beams, [], servos ?? [], [], [], wheels ?? [], touchSensors ?? [], rubbers ?? [], nextPartId);
}
