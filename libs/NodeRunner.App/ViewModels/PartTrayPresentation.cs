using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

/// <summary>Every part the Build tray lists, implemented or not (#374).</summary>
public enum BuildPart
{
    Brake,
    Servo,
    Stepper,
    VelocityMotor,
    Wheel,
    TouchSensor,
    Accelerometer,
    Camera,
    Pulse,
    Battery,
    Generator,
    FuelTank,
}

public enum PartTrayRowState
{
    Available,
    ComingLater,

    /// <summary>Implemented, but the creation is locked, so no part can be added (#896).</summary>
    CreationLocked,
}

public sealed record PartTrayRow(BuildPart Part, UiText Name, PartTrayRowState State)
{
    public bool IsAvailable => State == PartTrayRowState.Available;

    /// <summary>Why the row cannot be picked, or null when it can.</summary>
    public UiText? LockedReason => State switch
    {
        PartTrayRowState.Available => null,
        PartTrayRowState.CreationLocked => PartTray.CreationLockedReason,
        _ => PartTray.ComingLater,
    };
}

public sealed record PartTrayGroup(UiText Name, UiText HelpText, IReadOnlyList<PartTrayRow> Rows);

/// <summary>
/// The Build Parts tray: three tabs of reference parts (#374). Implemented rows are available;
/// a part not yet implemented, or held back like the Camera (#852), is a locked row; <see cref="ComingLater"/> is the reason a drag of one is refused.
/// </summary>
public static class PartTray
{
    public static UiText ComingLater { get; } = UiText.Plain("Coming later");

    public static UiText CreationLockedReason { get; } = UiText.Plain("Unlock to add or remove parts.");

    public static IReadOnlyList<PartTrayGroup> Groups() => Catalog();

    /// <summary>
    /// The tray for a locked creation: every row shows locked and each tab says how to unlock,
    /// so no row looks like it can be dragged out (#896).
    /// </summary>
    public static IReadOnlyList<PartTrayGroup> LockedGroups() =>
    [
        .. Catalog().Select(group => group with
        {
            HelpText = CreationLockedReason,
            Rows = [.. group.Rows.Select(row => row.IsAvailable ? row with { State = PartTrayRowState.CreationLocked } : row)],
        }),
    ];

    /// <summary>
    /// The tab the tray opens on: the first with a part the player can place (#887), so a
    /// tab of padlocks never reads as every part being locked. The first tab when none has one.
    /// </summary>
    public static int OpeningGroup()
    {
        var index = Array.FindIndex(Catalog(), group => group.Rows.Any(row => row.IsAvailable));
        return Math.Max(index, 0);
    }

    /// <summary>Whether the part is implemented and in the tray: false while it is "Coming later".</summary>
    public static bool IsAvailable(BuildPart part) =>
        Catalog().SelectMany(group => group.Rows).Single(row => row.Part == part).IsAvailable;

    /// <summary>The sensor a tray part places, or null for a part that is not a sensor.</summary>
    public static SensorKind? SensorKindOf(BuildPart part) => part switch
    {
        BuildPart.Accelerometer => SensorKind.Accelerometer,
        BuildPart.Camera => SensorKind.Camera,
        _ => null,
    };

    private static PartTrayGroup[] Catalog() =>
    [
        new(UiText.Plain("Moving parts"), UiText.Plain("Drag onto a joint. A joint holds one part."),
        [
            Available(BuildPart.Servo, UiText.Plain("Servo")),
            Locked(BuildPart.Stepper, UiText.Plain("Stepper")),
            Locked(BuildPart.VelocityMotor, UiText.Plain("Velocity motor")),
            Locked(BuildPart.Brake, UiText.Plain("Brake")),
            Locked(BuildPart.Wheel, UiText.Plain("Wheel")),
        ]),
        new(UiText.Plain("Sensors"), UiText.Plain("Drag onto a beam. A beam holds one sensor."),
        [
            Available(BuildPart.Accelerometer, UiText.Plain("Accelerometer")),
            // Implemented, but it adds little on the Flat map, so it waits for maps with terrain (#852).
            Locked(BuildPart.Camera, UiText.Plain("Camera")),
            Locked(BuildPart.TouchSensor, UiText.Plain("Touch sensor")),
            Locked(BuildPart.Pulse, UiText.Plain("Pulse")),
        ]),
        new(UiText.Plain("Blocks"), UiText.Plain("Drag it onto the canvas, then draw beams to its two eyes."),
        [
            Locked(BuildPart.Battery, UiText.Plain("Battery")),
            Locked(BuildPart.Generator, UiText.Plain("Generator")),
            Locked(BuildPart.FuelTank, UiText.Plain("Fuel tank")),
        ]),
    ];

    private static PartTrayRow Available(BuildPart part, UiText name) => new(part, name, PartTrayRowState.Available);

    private static PartTrayRow Locked(BuildPart part, UiText name) => new(part, name, PartTrayRowState.ComingLater);
}
