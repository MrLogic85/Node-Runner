using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

/// <summary>
/// What the brain's ports are called, in port order (<see cref="BrainPorts"/>): a sense is its
/// part's name and reading, such as "Accel: along", and a Piston's ports are its name and
/// what they measure or set, such as "Piston 1: speed" or "Piston 1: strength" (naming rule:
/// docs/CREATURE_MODEL.md, Sensor–model contract). Each label is one whole template
/// with the part's name as <c>{0}</c>; its space after the colon is a no-break space, so a sentence
/// that names the port does not wrap inside it.
/// </summary>
public sealed record BrainPortLabels(IReadOnlyList<UiText> Inputs, IReadOnlyList<UiText> Outputs)
{
    public static BrainPortLabels Empty { get; } = new([], []);

    public static BrainPortLabels For(CreatureDef creature)
    {
        ArgumentNullException.ThrowIfNull(creature);

        var sensorKinds = creature.Sensors.ToDictionary(sensor => sensor.Id, sensor => sensor.Kind);
        var servoIds = creature.Servos.Select(servo => servo.Id).ToHashSet();
        UiText Label(BrainPort port)
        {
            var name = PartNames.Display(creature.Nodes, creature.Beams, creature.Sensors, creature.Servos, creature.Pistons, creature.Springs, creature.Wheels, port.PartId);
            return sensorKinds.TryGetValue(port.PartId, out var kind)
                ? Reading(kind, port.Channel, name)
                : servoIds.Contains(port.PartId) ? ServoChannel(port.Channel, name)
                : PistonChannel(port.Channel, name);
        }

        var layout = BrainPorts.Of(creature);
        return new BrainPortLabels(
            layout.Inputs.Select(Label).ToArray(),
            layout.Outputs.Select(Label).ToArray());
    }

    // Keyed by the port's channel key, which never changes; whole templates, so each is translated as one.
    private static UiText Reading(SensorKind kind, string channel, UiText name) => (kind, channel) switch
    {
        (SensorKind.Accelerometer, "along") => UiText.Format("{0}:\u00A0along", name),
        (SensorKind.Accelerometer, "across") => UiText.Format("{0}:\u00A0across", name),
        (SensorKind.Camera, "left2") => UiText.Format("{0}:\u00A0far left", name),
        (SensorKind.Camera, "left1") => UiText.Format("{0}:\u00A0left", name),
        (SensorKind.Camera, "centre") => UiText.Format("{0}:\u00A0centre", name),
        (SensorKind.Camera, "right1") => UiText.Format("{0}:\u00A0right", name),
        (SensorKind.Camera, "right2") => UiText.Format("{0}:\u00A0far right", name),
        (SensorKind.Camera, "hit") => UiText.Format("{0}:\u00A0hit", name),
        _ => throw new InvalidOperationException($"No label for {kind} reading {channel}."),
    };

    private static UiText PistonChannel(string channel, UiText name) => channel switch
    {
        BrainPorts.PistonLengthChannel => UiText.Format("{0}:\u00A0length", name),
        BrainPorts.PistonSpeedChannel => UiText.Format("{0}:\u00A0speed", name),
        BrainPorts.PistonPositionChannel => UiText.Format("{0}:\u00A0length", name),
        BrainPorts.PistonStrengthChannel => UiText.Format("{0}:\u00A0strength", name),
        _ => throw new InvalidOperationException($"No label for Piston channel {channel}."),
    };

    private static UiText ServoChannel(string channel, UiText name) => channel switch
    {
        BrainPorts.ServoAngleChannel => UiText.Format("{0}:\u00A0angle", name),
        BrainPorts.ServoSpeedChannel => UiText.Format("{0}:\u00A0speed", name),
        BrainPorts.ServoStrengthChannel => UiText.Format("{0}:\u00A0strength", name),
        _ => throw new InvalidOperationException($"No label for Servo channel {channel}."),
    };
}
