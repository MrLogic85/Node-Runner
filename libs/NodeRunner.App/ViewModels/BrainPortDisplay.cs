using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

/// <summary>How a port's value is drawn (#1064): a bar centred on 0 for −1…1, a bar filling from empty for 0…1.</summary>
public enum PortRange
{
    MinusOneToOne,
    ZeroToOne,
}

/// <summary>
/// What a port is called beside its live bar in the part callout, such as "speed", and which
/// range its value spans (#1064). One entry per port, keyed by the part's kind, the port's
/// direction and its channel key from <see cref="BrainPorts"/>, never by translated text. These
/// short labels are separate from the full names in <see cref="BrainPortLabels"/>, which BrainFocus
/// shows. A port with no entry throws: a new part adds its ports here (docs/CREATURE_MODEL.md →
/// "Ports").
/// </summary>
public static class BrainPortDisplay
{
    /// <summary>The range <paramref name="port"/>'s value spans, on a part of <paramref name="kind"/>.</summary>
    public static PortRange Range(PartSettingsKind kind, BrainPort port) => Entry(kind, port).Range;

    /// <summary>What <paramref name="port"/> is called beside its bar, on a part of <paramref name="kind"/>.</summary>
    public static UiText ShortLabel(PartSettingsKind kind, BrainPort port) => Entry(kind, port).Label;

    /// <summary>
    /// <paramref name="value"/> as its bar shows it. A Piston's target length leaves the brain as
    /// −1…1 (shortest…longest) but is drawn 0…1, like the measured length it is compared with;
    /// #1129 makes the brain output 0…1 itself.
    /// </summary>
    public static double? ShownValue(PartSettingsKind kind, BrainPort port, double? value) =>
        Entry(kind, port).FromSigned ? (value + 1) / 2 : value;

    private static (UiText Label, PortRange Range, bool FromSigned) Entry(PartSettingsKind kind, BrainPort port) => (kind, port.Direction, port.Channel) switch
    {
        (PartSettingsKind.Accelerometer, PortDirection.Input, "along") => (UiText.Plain("along"), PortRange.MinusOneToOne, false),
        (PartSettingsKind.Accelerometer, PortDirection.Input, "across") => (UiText.Plain("across"), PortRange.MinusOneToOne, false),
        (PartSettingsKind.Camera, PortDirection.Input, "left2") => (UiText.Plain("far left"), PortRange.ZeroToOne, false),
        (PartSettingsKind.Camera, PortDirection.Input, "left1") => (UiText.Plain("left"), PortRange.ZeroToOne, false),
        (PartSettingsKind.Camera, PortDirection.Input, BrainPorts.CameraCentreChannel) => (UiText.Plain("centre"), PortRange.ZeroToOne, false),
        (PartSettingsKind.Camera, PortDirection.Input, "right1") => (UiText.Plain("right"), PortRange.ZeroToOne, false),
        (PartSettingsKind.Camera, PortDirection.Input, "right2") => (UiText.Plain("far right"), PortRange.ZeroToOne, false),
        (PartSettingsKind.Camera, PortDirection.Input, BrainPorts.CameraHitChannel) => (UiText.Plain("hit"), PortRange.ZeroToOne, false),
        (PartSettingsKind.Piston, PortDirection.Input, BrainPorts.PistonLengthChannel) => (UiText.Plain("length"), PortRange.ZeroToOne, false),
        (PartSettingsKind.Piston, PortDirection.Input, BrainPorts.PistonSpeedChannel) => (UiText.Plain("speed"), PortRange.MinusOneToOne, false),
        (PartSettingsKind.Piston, PortDirection.Output, BrainPorts.PistonPositionChannel) => (UiText.Plain("length"), PortRange.ZeroToOne, true),
        (PartSettingsKind.Piston, PortDirection.Output, BrainPorts.PistonStrengthChannel) => (UiText.Plain("strength"), PortRange.ZeroToOne, false),
        (PartSettingsKind.Servo, PortDirection.Input, BrainPorts.ServoAngleChannel) => (UiText.Plain("angle"), PortRange.MinusOneToOne, false),
        (PartSettingsKind.Servo, PortDirection.Input, BrainPorts.ServoSpeedChannel) => (UiText.Plain("speed"), PortRange.MinusOneToOne, false),
        (PartSettingsKind.Servo, PortDirection.Output, BrainPorts.ServoAngleOutputChannel) => (UiText.Plain("angle"), PortRange.MinusOneToOne, false),
        (PartSettingsKind.Servo, PortDirection.Output, BrainPorts.ServoStrengthChannel) => (UiText.Plain("strength"), PortRange.ZeroToOne, false),
        _ => throw new InvalidOperationException($"No callout entry for {kind} {port.Direction} {port.Channel}."),
    };
}
