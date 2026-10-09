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

    /// <summary>A Camera's centre ray key: the ray along its aim, which every ray count has (#578).</summary>
    public const string CameraCentreChannel = "centre";

    /// <summary>A Camera's hit key: whether any of its rays sees the ground (#1032).</summary>
    public const string CameraHitChannel = "hit";

    // Indexed by rays / 2: the keys for 1, 3 and 5 rays.
    private static readonly IReadOnlyList<string>[] _cameraChannels =
    [
        [CameraCentreChannel, CameraHitChannel],
        ["left1", CameraCentreChannel, "right1", CameraHitChannel],
        ["left2", "left1", CameraCentreChannel, "right1", "right2", CameraHitChannel],
    ];

    /// <summary>
    /// A Camera's input keys for <paramref name="rays"/> rays (#578): one per ray from left to right,
    /// named outward from the centre (<c>left2</c>, <c>left1</c>, <c>centre</c>, <c>right1</c>,
    /// <c>right2</c>), then hit. A ray keeps its key whatever the count, so a rebuild with another
    /// count keeps the shared rays' weights (#516). Never change one.
    /// </summary>
    public static IReadOnlyList<string> CameraChannels(int rays) =>
        SensorDef.IsRayCount(rays)
            ? _cameraChannels[rays / 2]
            : throw new ArgumentOutOfRangeException(nameof(rays), $"A Camera has an odd ray count from 1 to {SensorDef.MaxRays}.");

    /// <summary>A Piston's length input key: where it is in its travel, 0 at its shortest length and 1 at its longest.</summary>
    public const string PistonLengthChannel = "length";

    /// <summary>A Piston's speed input key.</summary>
    public const string PistonSpeedChannel = "speed";

    /// <summary>A Piston's position output key: the length to reach.</summary>
    public const string PistonPositionChannel = "position";

    /// <summary>A Piston's strength output key: the share of its Strength to use.</summary>
    public const string PistonStrengthChannel = "strength";

    /// <summary>A Servo's angle input key: where its target link is within its angular range.</summary>
    public const string ServoAngleChannel = "angle";

    /// <summary>A Servo's angular speed input key.</summary>
    public const string ServoSpeedChannel = "speed";

    /// <summary>A Servo's angle output key: the angle to reach.</summary>
    public const string ServoAngleOutputChannel = "angle";

    /// <summary>A Servo's strength output key: the share of its Strength to use.</summary>
    public const string ServoStrengthChannel = "strength";

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

        foreach (var servo in creature.Servos)
        {
            blocks.Add((servo.Id, [.. ServoInputs(servo.Id), .. ServoOutputs(servo.Id)]));
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
            SensorKind.Camera => CameraChannels(sensor.Rays!.Value),
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

    /// <summary>A Servo's input ports, angle then speed.</summary>
    public static IEnumerable<BrainPort> ServoInputs(int servoId) =>
    [
        BrainPort.Input(servoId, ServoAngleChannel),
        BrainPort.Input(servoId, ServoSpeedChannel),
    ];

    /// <summary>A Servo's output ports, angle then strength.</summary>
    public static IEnumerable<BrainPort> ServoOutputs(int servoId) =>
    [
        BrainPort.Output(servoId, ServoAngleOutputChannel, PortSignal.Position),
        BrainPort.Output(servoId, ServoStrengthChannel, PortSignal.Strength),
    ];
}
