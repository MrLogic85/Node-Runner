namespace NodeRunner.Domain;

/// <summary>
/// Collects the brain ports every part of a creature declares (#534) and puts them in runtime
/// order: by part id, then in the order the part declares them. The order depends only on ids, so
/// moving or resizing parts, or adding one, never reorders the others. Joints and Springs are
/// passive and declare no ports (#450, #453). Stateless. See docs/CREATURE_MODEL.md.
/// </summary>
public static class BrainPorts
{
    /// <summary>An Accelerometer's input keys, in order: along and across its beam. Never change one.</summary>
    public static IReadOnlyList<string> AccelerometerChannels { get; } = ["along", "across"];

    /// <summary>A Camera's input keys, one per ray from left to right. Never change one.</summary>
    public static IReadOnlyList<string> CameraChannels { get; } = ["left1", "centre", "right1"];

    /// <summary>A Piston's length input key: how far it is from its built length.</summary>
    public const string PistonLengthChannel = "length";

    /// <summary>A Piston's speed input key.</summary>
    public const string PistonSpeedChannel = "speed";

    /// <summary>A Piston's position output key: the length to reach.</summary>
    public const string PistonPositionChannel = "position";

    /// <summary>A Piston's strength output key: the share of its Strength to use.</summary>
    public const string PistonStrengthChannel = "strength";

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
            SensorKind.Accelerometer => AccelerometerChannels,
            SensorKind.Camera => CameraChannels,
            _ => throw new InvalidOperationException($"Unknown sensor kind {sensor.Kind}."),
        };
        return channels.Select(channel => BrainPort.Input(sensor.Id, channel));
    }

    /// <summary>A Piston's input ports, length then speed.</summary>
    public static IEnumerable<BrainPort> PistonInputs(int pistonId) =>
    [
        BrainPort.Input(pistonId, PistonLengthChannel),
        BrainPort.Input(pistonId, PistonSpeedChannel),
    ];

    /// <summary>A Piston's output ports, position then strength.</summary>
    public static IEnumerable<BrainPort> PistonOutputs(int pistonId) =>
    [
        BrainPort.Output(pistonId, PistonPositionChannel, PortSignal.Position),
        BrainPort.Output(pistonId, PistonStrengthChannel, PortSignal.Strength),
    ];
}
