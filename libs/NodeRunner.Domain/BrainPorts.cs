namespace NodeRunner.Domain;

/// <summary>
/// Collects the brain ports every part of a creature declares (#534) and puts them in runtime
/// order: by part id, then in the order the part declares them. A motor's ports belong to its
/// joint's node, one motor after another by the id of the beam it turns (<see cref="JointMotor"/>).
/// The order depends only on ids, so moving or resizing parts, or adding one, never reorders the
/// others. Which beam a motor turns still follows <see cref="MotorTopology"/>'s beam list order. Stateless, like <see cref="MotorTopology"/>. See docs/CREATURE_MODEL.md.
/// </summary>
public static class BrainPorts
{
    public static BrainPortLayout Of(CreatureDef creature)
    {
        ArgumentNullException.ThrowIfNull(creature);

        var blocks = new List<(int PartId, int Order, BrainPort[] Ports)>();
        foreach (var sensor in creature.Sensors)
        {
            blocks.Add((sensor.Id, 0, SensorPorts(sensor).ToArray()));
        }

        foreach (var connection in MotorTopology.BuildNodeConnections(creature).Where(connection => connection.IsMotorized))
        {
            var nodeId = creature.Nodes[connection.NodeIndex].Id;
            var beamId = creature.Beams[connection.OtherBeamIndex].Id;
            blocks.Add((nodeId, beamId, [.. JointMotorInputs(nodeId, beamId), JointMotorOutput(nodeId, beamId)]));
        }

        var ports = blocks
            .OrderBy(block => block.PartId)
            .ThenBy(block => block.Order)
            .SelectMany(block => block.Ports)
            .ToArray();
        return new BrainPortLayout(
            ports.Where(port => port.Direction == PortDirection.Input).ToArray(),
            ports.Where(port => port.Direction == PortDirection.Output).ToArray());
    }

    /// <summary>A sensor part's input ports, in the order it writes its readings.</summary>
    public static IEnumerable<BrainPort> SensorPorts(SensorDef sensor)
    {
        ArgumentNullException.ThrowIfNull(sensor);

        var channels = sensor.Kind switch
        {
            SensorKind.Accelerometer => Accelerometer.ChannelKeys,
            SensorKind.Camera => CameraRays.ChannelKeys,
            _ => throw new InvalidOperationException($"Unknown sensor kind {sensor.Kind}."),
        };
        return channels.Select(channel => new BrainPort(sensor.Id, channel, PortDirection.Input));
    }

    /// <summary>A joint motor's input ports, angle then speed, for the motor at node <paramref name="nodeId"/> turning beam <paramref name="beamId"/>.</summary>
    public static IEnumerable<BrainPort> JointMotorInputs(int nodeId, int beamId) =>
    [
        new(nodeId, JointMotor.AngleChannel(beamId), PortDirection.Input),
        new(nodeId, JointMotor.SpeedChannel(beamId), PortDirection.Input),
    ];

    /// <summary>A joint motor's target output port.</summary>
    public static BrainPort JointMotorOutput(int nodeId, int beamId) =>
        new(nodeId, JointMotor.TargetChannel(beamId), PortDirection.Output);
}
