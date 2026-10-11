using NodeRunner.Domain;

namespace NodeRunner.Theme;

/// <summary>Shared Servo drawing geometry for Build and Training.</summary>
public static class ServoGeometry
{
    /// <summary>How far a Servo's housing reaches along its Fixed <paramref name="link"/>, given what covers each end (<see cref="CreatureDef.CoveredRadius(int, IEnumerable{ServoDef})"/>, #1086).</summary>
    public static float HousingReach(IReadOnlyList<NodeDef> nodes, Func<int, double> coveredRadius, LinkRef link, float sensorLength)
    {
        var nodeA = nodes.First(node => node.Id == link.NodeA);
        var nodeB = nodes.First(node => node.Id == link.NodeB);
        var dx = nodeB.Position.X - nodeA.Position.X;
        var dy = nodeB.Position.Y - nodeA.Position.Y;
        var free = Math.Sqrt((dx * dx) + (dy * dy)) - coveredRadius(link.NodeA) - coveredRadius(link.NodeB);
        return ServoPart.HousingReachFor((float)free, sensorLength);
    }
}
