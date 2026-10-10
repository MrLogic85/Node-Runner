using System.Text.Json;

namespace NodeRunner.Domain.Tests;

public sealed class WheelDefTests
{
    private static readonly NodeDef[] _nodes = [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(100, 0)), new NodeDef(3, new Vector2D(0, 100))];
    private static readonly BeamDef[] _beams = [new BeamDef(4, 1, 2), new BeamDef(5, 1, 3)];

    [Fact]
    public void Constructor_DefaultsToTheSmallestRadius_And80PercentGrip()
    {
        var wheel = new WheelDef(6, 1);

        wheel.Radius.ShouldBe(40);
        wheel.Radius.ShouldBe(WheelDef.MinRadius);
        wheel.Grip.ShouldBe(0.8);
        wheel.Name.ShouldBeNull();
    }

    [Theory]
    [InlineData(0, 1, 40, 0.8)]
    [InlineData(6, 0, 40, 0.8)]
    [InlineData(6, 1, 39.9, 0.8)]
    [InlineData(6, 1, 100.1, 0.8)]
    [InlineData(6, 1, double.NaN, 0.8)]
    [InlineData(6, 1, 40, -0.1)]
    [InlineData(6, 1, 40, 1.1)]
    [InlineData(6, 1, 40, double.NaN)]
    public void Constructor_OutsideItsRanges_Throws(int id, int nodeId, double radius, double grip)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new WheelDef(id, nodeId, radius: radius, grip: grip));
    }

    [Fact]
    public void WithSettings_KeepsItsIdJointAndName()
    {
        var wheel = new WheelDef(6, 1, "Front");

        var changed = wheel.WithSettings(radius: 100, grip: 0);

        changed.ShouldBe(new WheelDef(6, 1, "Front", radius: 100, grip: 0));
        wheel.WithName("Back").ShouldBe(new WheelDef(6, 1, "Back"));
    }

    [Fact]
    public void CreatureDef_TakesOneWheelPerJoint_OnAJointThatExists_WithItsOwnId()
    {
        var creature = new CreatureDef(_nodes, _beams, [], [], [], [], [new WheelDef(6, 1)], nextPartId: 7);

        creature.Wheels.ShouldBe([new WheelDef(6, 1)]);
        creature.WheelIndexOf(6).ShouldBe(0);
        Should.Throw<ArgumentException>(() => new CreatureDef(_nodes, _beams, [], [], [], [], [new WheelDef(6, 9)], nextPartId: 7));
        Should.Throw<ArgumentException>(() => new CreatureDef(_nodes, _beams, [], [], [], [], [new WheelDef(6, 1), new WheelDef(7, 1)], nextPartId: 8));
        Should.Throw<ArgumentException>(() => new CreatureDef(_nodes, _beams, [], [], [], [], [new WheelDef(4, 2)], nextPartId: 7));
    }

    // A joint holds one part per slot (#1044): a Servo and a Wheel share one, two Servos do not.
    [Fact]
    public void CreatureDef_TakesAServoAndAWheelOnOneJointButNotTwoServos()
    {
        var stacked = new CreatureDef(_nodes, _beams, [], [new ServoDef(6, 1, 4, 5)], [], [], [new WheelDef(7, 1)], nextPartId: 8);

        stacked.NodeRadius(1).ShouldBe(WheelDef.DefaultRadius);
        Should.Throw<ArgumentException>(() => new CreatureDef(_nodes, _beams, [], [new ServoDef(6, 1, 4, 5), new ServoDef(7, 1, 5, 4)], [], [], [], nextPartId: 8));
    }

    [Theory]
    [InlineData(40)]
    [InlineData(100)]
    public void NodeRadius_OfAWheelsJoint_IsTheWheelsRadius(double radius)
    {
        var creature = new CreatureDef(_nodes, _beams, [], [new ServoDef(6, 1, 4, 5)], [], [], [new WheelDef(7, 2, radius: radius)], nextPartId: 8);

        creature.NodeRadius(2).ShouldBe(radius);
        creature.NodeRadius(1).ShouldBe(ServoDef.JointRadius);
        creature.NodeRadius(3).ShouldBe(NodeDef.PlainJointRadius);
    }

    [Fact]
    public void JointRadius_IsTheLargestOfThePlainJointAndEveryRadialPartOnIt()
    {
        CreatureDef.JointRadius(1, [], []).ShouldBe(NodeDef.PlainJointRadius);
        CreatureDef.JointRadius(1, [new ServoDef(6, 1, 4, 5)], []).ShouldBe(ServoDef.JointRadius);
        CreatureDef.JointRadius(1, [new ServoDef(6, 1, 4, 5)], [new WheelDef(7, 1)]).ShouldBe(WheelDef.MinRadius);
        CreatureDef.JointRadius(2, [new ServoDef(6, 1, 4, 5)], [new WheelDef(7, 1)]).ShouldBe(NodeDef.PlainJointRadius);
    }

    [Fact]
    public void JsonRoundTrip_PreservesWheels()
    {
        var original = new CreatureDef(_nodes, _beams, [], [], [], [], [new WheelDef(6, 2, "Front", radius: 70, grip: 0.3)], nextPartId: 7);

        var json = JsonSerializer.Serialize(original);
        var roundTripped = JsonSerializer.Deserialize<CreatureDef>(json);

        roundTripped.ShouldNotBeNull();
        roundTripped.Wheels.ShouldBe(original.Wheels);
    }
}
