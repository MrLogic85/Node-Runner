namespace NodeRunner.Domain;

/// <summary>
/// Collects the brain ports every part of a creature declares (#534) and puts them in runtime
/// order: by part id, then in the order the part declares them. The order depends only on ids, so
/// moving or resizing parts, or adding one, never reorders the others. Joints are passive and
/// declare no ports (#450). Stateless. See docs/CREATURE_MODEL.md.
/// </summary>
public static class BrainPorts
{
    public static BrainPortLayout Of(CreatureDef creature)
    {
        ArgumentNullException.ThrowIfNull(creature);

        var blocks = new List<(int PartId, BrainPort[] Ports)>();
        foreach (var sensor in creature.Sensors)
        {
            blocks.Add((sensor.Id, SensorPorts(sensor).ToArray()));
        }

        foreach (var piston in creature.Pistons)
        {
            blocks.Add((piston.Id, [.. PistonInputs(piston.Id), .. PistonOutputs(piston.Id)]));
        }

        var ports = blocks
            .OrderBy(block => block.PartId)
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
        return channels.Select(channel => BrainPort.Input(sensor.Id, channel));
    }

    /// <summary>A Piston's input ports, length then speed (<see cref="Piston"/>).</summary>
    public static IEnumerable<BrainPort> PistonInputs(int pistonId) =>
    [
        BrainPort.Input(pistonId, Piston.LengthChannel),
        BrainPort.Input(pistonId, Piston.SpeedChannel),
    ];

    /// <summary>A Piston's output ports, position then strength (<see cref="Piston"/>).</summary>
    public static IEnumerable<BrainPort> PistonOutputs(int pistonId) =>
    [
        BrainPort.Output(pistonId, Piston.PositionChannel, PortSignal.Position),
        BrainPort.Output(pistonId, Piston.StrengthChannel, PortSignal.Strength),
    ];
}
