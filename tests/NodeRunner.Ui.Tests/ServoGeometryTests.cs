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
}
