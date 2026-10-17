namespace NodeRunner.Domain;

/// <summary>
/// One part kind as the code walks it: how it is selected, which sensor it is if it is one, which joint slot
/// it fills if it sits on a joint, and its network ports in the order it declares them (<see cref="NetworkPorts"/>).
/// </summary>
public sealed record PartKindInfo(
    PartKind Kind,
    CreatureElementKind Element,
    SensorKind? Sensor,
    JointSlot? Slot,
    Func<IPartDef, IEnumerable<NetworkPort>> Ports);

/// <summary>
/// Every part kind, described once, so code that asks which parts sit on a joint or which ports a creature
/// has iterates the kinds instead of naming each one. Stateless. See docs/CREATURE_MODEL.md.
/// </summary>
public static class PartKinds
{
    private static readonly Func<IPartDef, IEnumerable<NetworkPort>> _noPorts = _ => [];

    /// <summary>Every kind, in <see cref="PartKind"/> order.</summary>
    public static IReadOnlyList<PartKindInfo> All { get; } =
    [
        new(PartKind.Node, CreatureElementKind.Node, Sensor: null, Slot: null, _noPorts),
        new(PartKind.Beam, CreatureElementKind.Beam, Sensor: null, Slot: null, _noPorts),
        new(PartKind.Accelerometer, CreatureElementKind.Sensor, SensorKind.Accelerometer, Slot: null, part => NetworkPorts.SensorPorts((SensorDef)part)),
        new(PartKind.Camera, CreatureElementKind.Sensor, SensorKind.Camera, Slot: null, part => NetworkPorts.SensorPorts((SensorDef)part)),
        new(PartKind.Servo, CreatureElementKind.Servo, Sensor: null, JointSlot.HingePart, part => [.. NetworkPorts.ServoInputs(part.Id), .. NetworkPorts.ServoOutputs(part.Id)]),
        new(PartKind.Piston, CreatureElementKind.Piston, Sensor: null, Slot: null, part => [.. NetworkPorts.PistonInputs(part.Id), .. NetworkPorts.PistonOutputs(part.Id)]),
        new(PartKind.Spring, CreatureElementKind.Spring, Sensor: null, Slot: null, _noPorts),
        new(PartKind.Wheel, CreatureElementKind.Wheel, Sensor: null, JointSlot.Wheel, _noPorts),
        new(PartKind.TouchSensor, CreatureElementKind.TouchSensor, Sensor: null, JointSlot.TouchSensor, part => [NetworkPorts.TouchInput(part.Id)]),
        new(PartKind.Rubber, CreatureElementKind.Rubber, Sensor: null, JointSlot.Rubber, _noPorts),
        new(PartKind.VelocityMotor, CreatureElementKind.VelocityMotor, Sensor: null, JointSlot.HingePart, part => [.. NetworkPorts.VelocityMotorInputs(part.Id), .. NetworkPorts.VelocityMotorOutputs(part.Id)]),
        new(PartKind.Brake, CreatureElementKind.Brake, Sensor: null, JointSlot.HingePart, _noPorts),
        new(PartKind.AngleSensor, CreatureElementKind.AngleSensor, Sensor: null, JointSlot.HingePart, part => NetworkPorts.AngleSensorInputs(part.Id)),
        new(PartKind.Stepper, CreatureElementKind.Stepper, Sensor: null, JointSlot.HingePart, part => [.. NetworkPorts.StepperInputs(part.Id), .. NetworkPorts.StepperOutputs(part.Id)]),
    ];

    public static PartKindInfo Of(PartKind kind) =>
        All.FirstOrDefault(info => info.Kind == kind) ?? throw new ArgumentOutOfRangeException(nameof(kind), kind, "Every part kind has an entry in PartKinds.All.");

    /// <summary>The kind a sensor of <paramref name="sensor"/> is.</summary>
    public static PartKindInfo Of(SensorKind sensor) =>
        All.FirstOrDefault(info => info.Sensor == sensor) ?? throw new ArgumentOutOfRangeException(nameof(sensor), sensor, "Every sensor kind has an entry in PartKinds.All.");

    public static JointSlot SlotOf(IJointPartDef part)
    {
        ArgumentNullException.ThrowIfNull(part);
        return Of(part.PartKind).Slot ?? throw new InvalidOperationException($"A {part.PartKind} sits on no joint slot.");
    }
}
