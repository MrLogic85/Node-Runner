using NodeRunner.Domain;

namespace NodeRunner.Theme;

/// <summary>Shared Servo drawing geometry for Build and Training.</summary>
public static class ServoGeometry
{
    public static float HousingReach(IReadOnlyList<NodeDef> nodes, Func<int, double> nodeRadius, LinkRef link, float sensorLength)
    {
        var nodeA = nodes.First(node => node.Id == link.NodeA);
        var nodeB = nodes.First(node => node.Id == link.NodeB);
        var dx = nodeB.Position.X - nodeA.Position.X;
        var dy = nodeB.Position.Y - nodeA.Position.Y;
        var free = Math.Sqrt((dx * dx) + (dy * dy)) - nodeRadius(link.NodeA) - nodeRadius(link.NodeB);
        return ServoPart.HousingReachFor((float)free, sensorLength);
    }
}
