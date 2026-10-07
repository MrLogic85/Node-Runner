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

    /// <summary>Implemented, but the creation is locked and the part has brain ports, so adding it would change the model (#896).</summary>
    CreationLocked,
}

/// <summary>A tray row; <c>Version</c> is the version that brings a Coming later row's part (#992), null for the others.</summary>
public sealed record PartTrayRow(BuildPart Part, UiText Name, PartTrayRowState State, string? Version = null)
{
    public bool IsAvailable => State == PartTrayRowState.Available;

    /// <summary>Why the row cannot be picked, or null when it can.</summary>
    public UiText? LockedReason => State switch
    {
        PartTrayRowState.Available => null,
        PartTrayRowState.CreationLocked => BuildViewModel.LockedReason,
        _ => PartTray.ComingIn(Version!),
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

    /// <summary>What a tap on a Coming later row says (#992); the catalogs give each row its version.</summary>
    public static UiText ComingIn(string version) => UiText.Format("Coming in version {0}", version);

    public static UiText CreationLockedHelp { get; } = UiText.Plain("Unlock to add parts.");

    public static IReadOnlyList<PartTrayGroup> Groups() => Catalog();

    /// <summary>
    /// The tray for a locked creation: every tray part has brain ports, so every row shows locked,
    /// each tab says how to unlock, and no row looks like it can be dragged out (#896).
    /// </summary>
    public static IReadOnlyList<PartTrayGroup> LockedGroups() =>
    [
        .. Catalog().Select(group => group with
        {
            HelpText = CreationLockedHelp,
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
            Locked(BuildPart.Stepper, UiText.Plain("Stepper"), "0.14.0"),
            Locked(BuildPart.VelocityMotor, UiText.Plain("Velocity motor"), "0.14.0"),
            Locked(BuildPart.Brake, UiText.Plain("Brake"), "0.14.0"),
            Locked(BuildPart.Wheel, UiText.Plain("Wheel"), "0.14.0"),
        ]),
        new(UiText.Plain("Sensors"), UiText.Plain("Drag onto a beam. A beam holds one sensor."),
        [
            Available(BuildPart.Accelerometer, UiText.Plain("Accelerometer")),
            // Implemented, but it adds little on the Flat map, so it waits for maps with terrain (#852).
            Locked(BuildPart.Camera, UiText.Plain("Camera"), "0.14.0"),
            Locked(BuildPart.TouchSensor, UiText.Plain("Touch sensor"), "0.14.0"),
            Locked(BuildPart.Pulse, UiText.Plain("Pulse"), "0.14.0"),
        ]),
        new(UiText.Plain("Blocks"), UiText.Plain("Drag it onto the canvas, then draw beams to its two eyes."),
        [
            Locked(BuildPart.Battery, UiText.Plain("Battery"), "0.18.0"),
            Locked(BuildPart.Generator, UiText.Plain("Generator"), "0.18.0"),
            Locked(BuildPart.FuelTank, UiText.Plain("Fuel tank"), "0.18.0"),
        ]),
    ];

    private static PartTrayRow Available(BuildPart part, UiText name) => new(part, name, PartTrayRowState.Available);

    // The version is the part's GitHub milestone (#992): moving the part to another milestone means updating it here.
    private static PartTrayRow Locked(BuildPart part, UiText name, string version) => new(part, name, PartTrayRowState.ComingLater, version);
}
