using NodeRunner.Domain;
using NodeRunner.Theme;

namespace NodeRunner.Ui.Tests;

public sealed class ServoGeometryTests
{
    private static readonly NodeDef[] _nodes = [new(1, new Vector2D(0, 0)), new(2, new Vector2D(170, 0))];

    [Theory]
    [InlineData(CreatureElementKind.Beam)]
    [InlineData(CreatureElementKind.Piston)]
    public void HousingReach_IsTheSameOnEveryLinkKind(CreatureElementKind kind)
    {
        var link = new LinkRef(10, 1, 2, kind);

        ServoGeometry.HousingReach(_nodes, _ => 27, link, sensorLength: 0).ShouldBe(8);
    }

    [Theory]
    [InlineData(1, 35)]
    [InlineData(2, 15)]
    public void LinkCover_AddsTheHousing_AtTheServoWhoseFixedLinkItIs(int nodeId, float cover)
    {
        // A Piston's telescope starts past the housing drawn over a Servo's Fixed link.
        var link = new LinkRef(10, 1, 2, CreatureElementKind.Piston);
        ServoDef[] servos = [new(20, 1, fixedLinkId: 10, targetLinkId: 11)];

        ServoGeometry.LinkCover(_nodes, id => id == 1 ? 27 : 15, servos, link, nodeId).ShouldBe(cover);
    }

    [Fact]
    public void LinkCover_AddsTheHousing_AtJointB()
    {
        var link = new LinkRef(10, 1, 2, CreatureElementKind.Spring);
        ServoDef[] servos = [new(20, 2, fixedLinkId: 10, targetLinkId: 11)];

        ServoGeometry.LinkCover(_nodes, id => id == 2 ? 27 : 15, servos, link, 2).ShouldBe(35);
    }

    [Fact]
    public void LinkCover_IsTheMotorRingOnly_OnAServosTargetLink()
    {
        var link = new LinkRef(10, 1, 2, CreatureElementKind.Piston);
        ServoDef[] servos = [new(20, 1, fixedLinkId: 11, targetLinkId: 10)];

        ServoGeometry.LinkCover(_nodes, id => id == 1 ? 27 : 15, servos, link, 1).ShouldBe(27);
    }
}
