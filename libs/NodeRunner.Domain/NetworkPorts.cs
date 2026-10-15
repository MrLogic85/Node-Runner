namespace NodeRunner.Domain;

/// <summary>
/// Collects the network ports every part of a creature declares (#534), as <see cref="PartKinds"/> gives each kind's,
/// and puts them in runtime order: by part id, then in the order the part declares them. The order depends only on ids, so
/// moving or resizing parts, or adding one, never reorders the others. Joints and Springs are
/// passive and declare no ports (#450, #453). Stateless. See docs/CREATURE_MODEL.md.
/// </summary>
public static class NetworkPorts
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

    /// <summary>A Touch sensor's input key (#1043): how far the ground reaches into its cushion, 0–1 (<c>NodeRunner.Mechanics.Touch.Reading</c>).</summary>
    public const string TouchChannel = "touch";

    /// <summary>A Piston's length input key: where it is in its travel, 0 at its shortest length and 1 at its longest.</summary>
    public const string PistonLengthChannel = "length";

    /// <summary>A Piston's speed input key.</summary>
    public const string PistonSpeedChannel = "speed";

    /// <summary>A Piston's position output key: the length to reach, 0 at its shortest and 1 at its longest (#1129).</summary>
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

    /// <summary>A Velocity motor's angle sine input key (#454): sin of the angle between its sides (#1207), smooth across a whole turn.</summary>
    public const string VelocityMotorAngleSinChannel = "angleSin";

    /// <summary>A Velocity motor's angle cosine input key (#454): cos of the angle between its sides (#1207).</summary>
    public const string VelocityMotorAngleCosChannel = "angleCos";

    /// <summary>A Velocity motor's angular speed input key.</summary>
    public const string VelocityMotorSpeedChannel = "speed";

    /// <summary>A Velocity motor's speed output key: the speed to turn at, its sign the direction.</summary>
    public const string VelocityMotorSpeedOutputChannel = "speed";

    /// <summary>A Velocity motor's strength output key: the share of its Strength to use.</summary>
    public const string VelocityMotorStrengthChannel = "strength";

    /// <summary>An Angle sensor's angle sine input key (#1204): sin of the angle between its sides, as a Velocity motor reads it.</summary>
    public const string AngleSensorAngleSinChannel = "angleSin";

    /// <summary>An Angle sensor's angle cosine input key (#1204): cos of the angle between its sides.</summary>
    public const string AngleSensorAngleCosChannel = "angleCos";

    /// <summary>An Angle sensor's angular speed input key (#1204), on its own fixed scale (<c>NodeRunner.Mechanics.AngleSensor</c>).</summary>
    public const string AngleSensorSpeedChannel = "speed";

    /// <summary>A Stepper's angle sine input key (#1036): sin of the angle between its sides, as a Velocity motor reads it.</summary>
    public const string StepperAngleSinChannel = "angleSin";

    /// <summary>A Stepper's angle cosine input key (#1036): cos of the angle between its sides.</summary>
    public const string StepperAngleCosChannel = "angleCos";

    /// <summary>A Stepper's tick input key (#1263): 0 as it steps, rising to 1 just before its next (<c>NodeRunner.Mechanics.Stepper.TickInput</c>).</summary>
    public const string StepperTickChannel = "tick";

    /// <summary>A Stepper's frequency output key: how often to step and which way, −1…1 of its Max frequency (#1263).</summary>
    public const string StepperFrequencyChannel = "frequency";

    public static NetworkPortLayout Of(CreatureDef creature)
    {
        ArgumentNullException.ThrowIfNull(creature);

        var ports = creature.Parts
            .OrderBy(part => part.Id)
            .SelectMany(part => PartKinds.Of(part.PartKind).Ports(part))
            .ToArray();
        return new NetworkPortLayout(
            ports.Where(port => port.Direction == PortDirection.Input).ToArray(),
            ports.Where(port => port.Direction == PortDirection.Output).ToArray());
    }

    /// <summary>A sensor part's input ports, in the order it writes its readings.</summary>
    public static IEnumerable<NetworkPort> SensorPorts(SensorDef sensor)
    {
        ArgumentNullException.ThrowIfNull(sensor);

        var channels = sensor.Kind switch
        {
            SensorKind.Accelerometer => AccelerometerChannels,
            SensorKind.Camera => CameraChannels(sensor.Rays!.Value),
            _ => throw new InvalidOperationException($"Unknown sensor kind {sensor.Kind}."),
        };
        return channels.Select(channel => NetworkPort.Input(sensor.Id, channel));
    }

    /// <summary>A Touch sensor's one input port.</summary>
    public static NetworkPort TouchInput(int touchSensorId) => NetworkPort.Input(touchSensorId, TouchChannel);

    /// <summary>An Angle sensor's input ports, angle sine, angle cosine, then speed. It has no outputs.</summary>
    public static IEnumerable<NetworkPort> AngleSensorInputs(int angleSensorId) =>
    [
        NetworkPort.Input(angleSensorId, AngleSensorAngleSinChannel),
        NetworkPort.Input(angleSensorId, AngleSensorAngleCosChannel),
        NetworkPort.Input(angleSensorId, AngleSensorSpeedChannel),
    ];

    /// <summary>A Piston's input ports, length then speed.</summary>
    public static IEnumerable<NetworkPort> PistonInputs(int pistonId) =>
    [
        NetworkPort.Input(pistonId, PistonLengthChannel),
        NetworkPort.Input(pistonId, PistonSpeedChannel),
    ];

    /// <summary>A Piston's output ports, position then strength.</summary>
    public static IEnumerable<NetworkPort> PistonOutputs(int pistonId) =>
    [
        NetworkPort.Output(pistonId, PistonPositionChannel, PortSignal.Length),
        NetworkPort.Output(pistonId, PistonStrengthChannel, PortSignal.Strength),
    ];

    /// <summary>A Servo's input ports, angle then speed.</summary>
    public static IEnumerable<NetworkPort> ServoInputs(int servoId) =>
    [
        NetworkPort.Input(servoId, ServoAngleChannel),
        NetworkPort.Input(servoId, ServoSpeedChannel),
    ];

    /// <summary>A Servo's output ports, angle then strength.</summary>
    public static IEnumerable<NetworkPort> ServoOutputs(int servoId) =>
    [
        NetworkPort.Output(servoId, ServoAngleOutputChannel, PortSignal.Position),
        NetworkPort.Output(servoId, ServoStrengthChannel, PortSignal.Strength),
    ];

    /// <summary>A Velocity motor's input ports, angle sine, angle cosine, then speed.</summary>
    public static IEnumerable<NetworkPort> VelocityMotorInputs(int velocityMotorId) =>
    [
        NetworkPort.Input(velocityMotorId, VelocityMotorAngleSinChannel),
        NetworkPort.Input(velocityMotorId, VelocityMotorAngleCosChannel),
        NetworkPort.Input(velocityMotorId, VelocityMotorSpeedChannel),
    ];

    /// <summary>A Velocity motor's output ports, speed then strength.</summary>
    public static IEnumerable<NetworkPort> VelocityMotorOutputs(int velocityMotorId) =>
    [
        NetworkPort.Output(velocityMotorId, VelocityMotorSpeedOutputChannel, PortSignal.Velocity),
        NetworkPort.Output(velocityMotorId, VelocityMotorStrengthChannel, PortSignal.Strength),
    ];

    /// <summary>A Stepper's input ports, angle sine, angle cosine, then tick.</summary>
    public static IEnumerable<NetworkPort> StepperInputs(int stepperId) =>
    [
        NetworkPort.Input(stepperId, StepperAngleSinChannel),
        NetworkPort.Input(stepperId, StepperAngleCosChannel),
        NetworkPort.Input(stepperId, StepperTickChannel),
    ];

    /// <summary>A Stepper's output port: its frequency.</summary>
    public static IEnumerable<NetworkPort> StepperOutputs(int stepperId) =>
    [
        NetworkPort.Output(stepperId, StepperFrequencyChannel, PortSignal.Frequency),
    ];
}
