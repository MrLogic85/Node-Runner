using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

/// <summary>
/// How one part kind shows (#1223): the Part settings <see cref="Title"/>, which says what the part is even when it is
/// renamed; the <see cref="DefaultName"/> of the nth part of its kind, which fits in <see cref="NameLimits.Part"/> (#868);
/// and every brain port its parts can have (<see cref="Ports"/>). <see cref="PartKindDisplays.All"/> holds one per
/// <see cref="PartKind"/>.
/// </summary>
public sealed record PartKindDisplay(
    PartKind Kind,
    UiText Title,
    Func<int, UiText> DefaultName,
    IReadOnlyList<PortDisplay> Ports)
{
    /// <summary>How <paramref name="port"/> shows on a part of this kind; a port with no entry throws.</summary>
    public PortDisplay Port(BrainPort port)
    {
        ArgumentNullException.ThrowIfNull(port);
        return Ports.FirstOrDefault(entry => entry.Direction == port.Direction && entry.Channel == port.Channel)
            ?? throw new InvalidOperationException($"No display entry for {Kind} {port.Direction} {port.Channel}.");
    }
}

/// <summary>
/// How one brain port shows, keyed by its direction and channel key from <see cref="BrainPorts"/>, never by translated
/// text. <see cref="Label"/> is its full name, which BrainFocus shows: one whole template with the part's name as
/// <c>{0}</c>, such as "Piston 1: speed", whose space after the colon is a no-break space so a sentence that names the
/// port does not wrap inside it (naming rule: docs/CREATURE_MODEL.md, Sensor–model contract). <see cref="ShortLabel"/>
/// is what the part callout calls it beside its live bar, and <see cref="Range"/> the range that bar draws (#1064).
/// </summary>
public sealed record PortDisplay(PortDirection Direction, string Channel, Func<UiText, UiText> Label, UiText ShortLabel, PortRange Range);

/// <summary>The one display table: an entry per <see cref="PartKind"/>, in its order.</summary>
public static class PartKindDisplays
{
    private const PortRange _signed = PortRange.MinusOneToOne;
    private const PortRange _unsigned = PortRange.ZeroToOne;

    public static IReadOnlyList<PartKindDisplay> All { get; } =
    [
        new(PartKind.Node, UiText.Plain("Joint"), number => UiText.Format("Joint {0}", number), []),
        new(PartKind.Beam, UiText.Plain("Beam"), number => UiText.Format("Beam {0}", number), []),
        new(PartKind.Accelerometer, UiText.Plain("Accelerometer"), number => UiText.Format("Accel {0}", number),
        [
            In("along", name => UiText.Format("{0}:\u00A0along", name), UiText.Plain("along"), _signed),
            In("across", name => UiText.Format("{0}:\u00A0across", name), UiText.Plain("across"), _signed),
        ]),
        new(PartKind.Camera, UiText.Plain("Camera"), number => UiText.Format("Camera {0}", number),
        [
            In("left2", name => UiText.Format("{0}:\u00A0far left", name), UiText.Plain("far left"), _unsigned),
            In("left1", name => UiText.Format("{0}:\u00A0left", name), UiText.Plain("left"), _unsigned),
            In(BrainPorts.CameraCentreChannel, name => UiText.Format("{0}:\u00A0centre", name), UiText.Plain("centre"), _unsigned),
            In("right1", name => UiText.Format("{0}:\u00A0right", name), UiText.Plain("right"), _unsigned),
            In("right2", name => UiText.Format("{0}:\u00A0far right", name), UiText.Plain("far right"), _unsigned),
            In(BrainPorts.CameraHitChannel, name => UiText.Format("{0}:\u00A0hit", name), UiText.Plain("hit"), _unsigned),
        ]),
        new(PartKind.Servo, UiText.Plain("Servo"), number => UiText.Format("Servo {0}", number),
        [
            In(BrainPorts.ServoAngleChannel, name => UiText.Format("{0}:\u00A0angle", name), UiText.Plain("angle"), _signed),
            In(BrainPorts.ServoSpeedChannel, name => UiText.Format("{0}:\u00A0speed", name), UiText.Plain("speed"), _signed),
            Out(BrainPorts.ServoAngleOutputChannel, name => UiText.Format("{0}:\u00A0angle", name), UiText.Plain("angle"), _signed),
            Out(BrainPorts.ServoStrengthChannel, name => UiText.Format("{0}:\u00A0strength", name), UiText.Plain("strength"), _unsigned),
        ]),
        new(PartKind.Piston, UiText.Plain("Piston"), number => UiText.Format("Piston {0}", number),
        [
            In(BrainPorts.PistonLengthChannel, name => UiText.Format("{0}:\u00A0length", name), UiText.Plain("length"), _unsigned),
            In(BrainPorts.PistonSpeedChannel, name => UiText.Format("{0}:\u00A0speed", name), UiText.Plain("speed"), _signed),

            // Its target length shows like the length it measures.
            Out(BrainPorts.PistonPositionChannel, name => UiText.Format("{0}:\u00A0length", name), UiText.Plain("length"), _unsigned),
            Out(BrainPorts.PistonStrengthChannel, name => UiText.Format("{0}:\u00A0strength", name), UiText.Plain("strength"), _unsigned),
        ]),
        new(PartKind.Spring, UiText.Plain("Spring"), number => UiText.Format("Spring {0}", number), []),
        new(PartKind.Wheel, UiText.Plain("Wheel"), number => UiText.Format("Wheel {0}", number), []),
        new(PartKind.TouchSensor, UiText.Plain("Touch sensor"), number => UiText.Format("Touch {0}", number),
        [
            In(BrainPorts.TouchChannel, name => UiText.Format("{0}:\u00A0touch", name), UiText.Plain("touch"), _unsigned),
        ]),
        new(PartKind.Rubber, UiText.Plain("Rubber"), number => UiText.Format("Rubber {0}", number), []),
        new(PartKind.VelocityMotor, UiText.Plain("Motor"), number => UiText.Format("Motor {0}", number),
        [
            In(BrainPorts.VelocityMotorAngleSinChannel, name => UiText.Format("{0}:\u00A0angle sin", name), UiText.Plain("angle sin"), _signed),
            In(BrainPorts.VelocityMotorAngleCosChannel, name => UiText.Format("{0}:\u00A0angle cos", name), UiText.Plain("angle cos"), _signed),
            In(BrainPorts.VelocityMotorSpeedChannel, name => UiText.Format("{0}:\u00A0speed", name), UiText.Plain("speed"), _signed),
            Out(BrainPorts.VelocityMotorSpeedOutputChannel, name => UiText.Format("{0}:\u00A0speed", name), UiText.Plain("speed"), _signed),
            Out(BrainPorts.VelocityMotorStrengthChannel, name => UiText.Format("{0}:\u00A0strength", name), UiText.Plain("strength"), _unsigned),
        ]),
        new(PartKind.Brake, UiText.Plain("Brake"), number => UiText.Format("Brake {0}", number), []),
        new(PartKind.AngleSensor, UiText.Plain("Angle sensor"), number => UiText.Format("Angle {0}", number),
        [
            In(BrainPorts.AngleSensorAngleSinChannel, name => UiText.Format("{0}:\u00A0angle sin", name), UiText.Plain("angle sin"), _signed),
            In(BrainPorts.AngleSensorAngleCosChannel, name => UiText.Format("{0}:\u00A0angle cos", name), UiText.Plain("angle cos"), _signed),
            In(BrainPorts.AngleSensorSpeedChannel, name => UiText.Format("{0}:\u00A0speed", name), UiText.Plain("speed"), _signed),
        ]),
        new(PartKind.Stepper, UiText.Plain("Stepper"), number => UiText.Format("Stepper {0}", number),
        [
            In(BrainPorts.StepperAngleSinChannel, name => UiText.Format("{0}:\u00A0angle sin", name), UiText.Plain("angle sin"), _signed),
            In(BrainPorts.StepperAngleCosChannel, name => UiText.Format("{0}:\u00A0angle cos", name), UiText.Plain("angle cos"), _signed),
            In(BrainPorts.StepperTickChannel, name => UiText.Format("{0}:\u00A0tick", name), UiText.Plain("tick"), _unsigned),
            Out(BrainPorts.StepperFrequencyChannel, name => UiText.Format("{0}:\u00A0frequency", name), UiText.Plain("frequency"), _signed),
        ]),
    ];

    public static PartKindDisplay Of(PartKind kind) => All[(int)kind];

    private static PortDisplay In(string channel, Func<UiText, UiText> label, UiText shortLabel, PortRange range) =>
        new(PortDirection.Input, channel, label, shortLabel, range);

    private static PortDisplay Out(string channel, Func<UiText, UiText> label, UiText shortLabel, PortRange range) =>
        new(PortDirection.Output, channel, label, shortLabel, range);
}
