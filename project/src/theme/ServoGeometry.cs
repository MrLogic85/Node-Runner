using NodeRunner.Domain;

namespace NodeRunner.Theme;

public static class ServoGeometry
{
    /// <summary>How far a Servo's housing reaches along its Fixed <paramref name="link"/>, given what covers each end (<see cref="CreatureDef.CoveredRadius(int, IEnumerable{IJointMotorDef})"/>).</summary>
    public static float HousingReach(IReadOnlyList<NodeDef> nodes, Func<int, double> coveredRadius, LinkRef link, float sensorLength)
    {
        var nodeA = nodes.First(node => node.Id == link.NodeA);
        var nodeB = nodes.First(node => node.Id == link.NodeB);
        var dx = nodeB.Position.X - nodeA.Position.X;
        var dy = nodeB.Position.Y - nodeA.Position.Y;
        var free = Math.Sqrt((dx * dx) + (dy * dy)) - coveredRadius(link.NodeA) - coveredRadius(link.NodeB);
        return ServoPart.HousingReachFor((float)free, sensorLength);
    }

    /// <summary>
    /// What covers a Piston's or Spring's <paramref name="link"/> at its <paramref name="nodeId"/> end:
    /// the joint's <paramref name="coveredRadius"/>, plus the housing's reach when a Servo
    /// there holds the link as its Fixed side, since the housing is drawn over it. No sensor sits on
    /// a Piston or Spring.
    /// </summary>
    public static float LinkCover(IReadOnlyList<NodeDef> nodes, Func<int, double> coveredRadius, IEnumerable<ServoDef> servos, LinkRef link, int nodeId)
    {
        ArgumentNullException.ThrowIfNull(coveredRadius);
        var housing = servos.Any(servo => servo.NodeId == nodeId && servo.FixedLinkId == link.Id)
            ? HousingReach(nodes, coveredRadius, link, sensorLength: 0)
            : 0;
        return (float)coveredRadius(nodeId) + housing;
    }
}
