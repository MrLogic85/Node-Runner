using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

/// <summary>Every part the Build tray lists, implemented or not (#374).</summary>
public enum BuildPart
{
    Spring,
    Piston,
    Wing,
    Brake,
    Servo,
    Stepper,
    VelocityMotor,
    Wheel,
    Accelerometer,
    LosSensor,
    Battery,
    Generator,
    FuelTank,
}

public enum PartTrayRowState
{
    Available,
    ComingLater,
}

public sealed record PartTrayRow(BuildPart Part, string Name, PartTrayRowState State)
{
    public bool IsAvailable => State != PartTrayRowState.ComingLater;

    /// <summary>Why the row cannot be picked, or empty when it can.</summary>
    public string LockedReason => IsAvailable ? string.Empty : PartTray.ComingLater;
}

public sealed record PartTrayGroup(string Name, string HelpText, IReadOnlyList<PartTrayRow> Rows)
{
    /// <summary>Explains the locked rows once for the whole tab, or empty when none is locked.</summary>
    public string LockedNote => Rows.Any(row => !row.IsAvailable) ? PartTray.ComingLater : string.Empty;
}

/// <summary>
/// The Build Parts tray: four tabs of reference parts (#374). Implemented rows are available;
/// a part not yet implemented shows "Coming later".
/// </summary>
public static class PartTray
{
    public const string ComingLater = "Coming later";

    public static IReadOnlyList<PartTrayGroup> Groups() => Catalog();

    /// <summary>Whether the tray lets the player pick or drag this part: false while it is "Coming later".</summary>
    public static bool IsAvailable(BuildPart part) =>
        Catalog().SelectMany(group => group.Rows).Single(row => row.Part == part).IsAvailable;

    /// <summary>The sensor a tray part places, or null for a part that is not a sensor.</summary>
    public static SensorKind? SensorKindOf(BuildPart part) => part switch
    {
        BuildPart.Accelerometer => SensorKind.Accelerometer,
        BuildPart.LosSensor => SensorKind.LineOfSight,
        _ => null,
    };

    private static PartTrayGroup[] Catalog() =>
    [
        new("Links", "Pick one, then drag from one node to another, like the Beam tool.",
        [
            Locked(BuildPart.Spring, "Spring"),
            Locked(BuildPart.Piston, "Piston"),
            Locked(BuildPart.Wing, "Wing"),
        ]),
        new("On a joint", "Drag onto a joint. A joint holds one part.",
        [
            Locked(BuildPart.Brake, "Brake"),
            Locked(BuildPart.Servo, "Servo"),
            Locked(BuildPart.Stepper, "Stepper"),
            Locked(BuildPart.VelocityMotor, "Velocity motor"),
            Locked(BuildPart.Wheel, "Wheel"),
        ]),
        new("Sensors", "Drag onto a beam. A beam holds one sensor.",
        [
            Available(BuildPart.Accelerometer, "Accelerometer"),
            Available(BuildPart.LosSensor, "LOS sensor"),
        ]),
        new("Blocks", "Drag it onto the canvas, then draw beams to its two eyes.",
        [
            Locked(BuildPart.Battery, "Battery"),
            Locked(BuildPart.Generator, "Generator"),
            Locked(BuildPart.FuelTank, "Fuel tank"),
        ]),
    ];

    private static PartTrayRow Available(BuildPart part, string name) => new(part, name, PartTrayRowState.Available);

    private static PartTrayRow Locked(BuildPart part, string name) => new(part, name, PartTrayRowState.ComingLater);
}
