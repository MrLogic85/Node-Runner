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

    /// <summary>Available and picked: a canvas tap places it (#805).</summary>
    Selected,
    ComingLater,

    /// <summary>Implemented, but the creation is locked and the part has brain ports, so adding it would change the model (#896, <see cref="PartTray.HasBrainPorts"/>).</summary>
    CreationLocked,
}

/// <summary>A tray row; <c>Version</c> is the version that brings a Coming later row's part (#992), null for the other rows.</summary>
public sealed record PartTrayRow(BuildPart Part, UiText Name, PartTrayRowState State, string? Version)
{
    public bool IsAvailable => State is PartTrayRowState.Available or PartTrayRowState.Selected;
}

public sealed record PartTrayGroup(UiText Name, IReadOnlyList<PartTrayRow> Rows);

/// <summary>
/// The Parts tray (#805): its tabs, what the picked part does and where it goes (<see cref="PickedInfo"/>,
/// right under its row as in the Links list, so it is on screen however long the tab), and one help line.
/// </summary>
public sealed record PartTrayPresentation(
    IReadOnlyList<PartTrayGroup> Groups,
    UiText? PickedInfo,
    UiText HelpText);

/// <summary>
/// The Build Parts tray: three tabs of reference parts (#374). Implemented rows are available;
/// a part not yet implemented is a locked row; <see cref="ComingLater"/> is the reason a drag of one is refused.
/// </summary>
public static class PartTray
{
    public static UiText ComingLater { get; } = UiText.Plain("Coming later");

    /// <summary>What a tap on a Coming later row says (#992): the part and the version that brings it.</summary>
    public static UiText ComingLaterReason(BuildPart part) =>
        Catalog().SelectMany(group => group.Rows).Single(row => row.Part == part) is { State: PartTrayRowState.ComingLater } row
            ? ComingIn(row.Name, row.Version!)
            : throw new ArgumentOutOfRangeException(nameof(part), part, "Only a Coming later part has a version.");

    /// <summary>A Coming later part or link with the version that brings it (#992).</summary>
    public static UiText ComingIn(UiText name, string version) => UiText.Format("{0} comes in version {1}", name, version);

    public static UiText CreationLockedHelp { get; } = UiText.Plain("Unlock to add parts the brain uses.");

    /// <summary>The tray's help line (#805).</summary>
    public static UiText PickHelp { get; } = UiText.Plain("Tap a part to pick it, or drag it onto the creature.");

    public static IReadOnlyList<PartTrayGroup> Groups() => Catalog();

    /// <summary>
    /// The tray with <paramref name="picked"/> selected (#805). On a locked creation each available
    /// part with brain ports shows locked, so it does not look like it can be picked or dragged out,
    /// and the help says how to unlock (#896); a part without, like the Wheel, stays available.
    /// </summary>
    public static PartTrayPresentation Create(BuildPart? picked, bool creationLocked = false)
    {
        var pickedPart = picked is { } part && CanPick(part, creationLocked) ? part : (BuildPart?)null;
        return new(
            [.. Catalog().Select(group => group with { Rows = [.. group.Rows.Select(row => Shown(row, pickedPart, creationLocked))] })],
            pickedPart is { } shown ? PickedInfo(shown) : null,
            creationLocked ? CreationLockedHelp : PickHelp);
    }

    /// <summary>Whether the part can be picked or dragged out now: available, and on a locked creation without brain ports (#896).</summary>
    public static bool CanPick(BuildPart part, bool creationLocked) => IsAvailable(part) && !(creationLocked && HasBrainPorts(part));

    /// <summary>Whether placing <paramref name="part"/> adds brain ports, so a locked creation refuses it (#896): every part but the Wheel (#129).</summary>
    public static bool HasBrainPorts(BuildPart part) => part != BuildPart.Wheel;

    private static PartTrayRow Shown(PartTrayRow row, BuildPart? picked, bool creationLocked) =>
        creationLocked && row.IsAvailable && HasBrainPorts(row.Part) ? row with { State = PartTrayRowState.CreationLocked }
        : row.Part == picked ? row with { State = PartTrayRowState.Selected }
        : row;

    private static UiText PickedInfo(BuildPart part) =>
        Info(part) is { } info ? UiText.Format("{0}\n{1}", info, Placement(part)) : Placement(part);

    /// <summary>What the part does: the same line as its Part settings note (<see cref="PartInfo"/>), or null for a part not built yet.</summary>
    public static UiText? Info(BuildPart part) => part switch
    {
        BuildPart.Servo => PartInfo.Servo,
        BuildPart.Wheel => PartInfo.Wheel,
        BuildPart.Accelerometer => PartInfo.Accelerometer,
        BuildPart.Camera => PartInfo.Camera,
        _ => null,
    };

    /// <summary>Where a picked part goes, per part rather than per tab, as a tab can mix placements (#805).</summary>
    public static UiText Placement(BuildPart part) => SensorKindOf(part) is not null
        ? UiText.Plain("Tap a beam to place it. A beam holds one sensor.")
        : part == BuildPart.Wheel
            ? UiText.Plain("Tap a joint to place it. A joint holds one wheel.")
            : UiText.Plain("Tap a joint to place it. A joint holds one part.");

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

    /// <summary>Whether a tray part goes on a joint (a Servo or a Wheel, #129) rather than on a beam.</summary>
    public static bool IsJointPart(BuildPart part) => part is BuildPart.Servo or BuildPart.Wheel;

    /// <summary>
    /// The radius of the joint part <paramref name="placing"/> as placed, round which the canvas
    /// marks each joint while it is placed (#1055); see <see cref="IsJointPart"/>.
    /// </summary>
    public static double PlacingRingRadius(BuildPart placing) => placing switch
    {
        BuildPart.Servo => ServoDef.JointRadius,
        BuildPart.Wheel => WheelDef.DefaultRadius,
        _ => throw new ArgumentOutOfRangeException(nameof(placing), placing, "Only a joint part has a placing ring."),
    };

    private static PartTrayGroup[] Catalog() =>
    [
        new(UiText.Plain("Moving parts"),
        [
            Available(BuildPart.Servo, UiText.Plain("Servo")),
            Locked(BuildPart.Stepper, UiText.Plain("Stepper"), "0.14.3"),
            Locked(BuildPart.VelocityMotor, UiText.Plain("Velocity motor"), "0.14.2"),
            Locked(BuildPart.Brake, UiText.Plain("Brake"), "0.14.2"),
            Available(BuildPart.Wheel, UiText.Plain("Wheel")),
        ]),
        new(UiText.Plain("Sensors"),
        [
            Available(BuildPart.Accelerometer, UiText.Plain("Accelerometer")),
            Available(BuildPart.Camera, UiText.Plain("Camera")),
            Locked(BuildPart.TouchSensor, UiText.Plain("Touch sensor"), "0.14.1"),
            Locked(BuildPart.Pulse, UiText.Plain("Pulse"), "0.14.4"),
        ]),
        new(UiText.Plain("Blocks"),
        [
            Locked(BuildPart.Battery, UiText.Plain("Battery"), "0.18.0"),
            Locked(BuildPart.Generator, UiText.Plain("Generator"), "0.18.0"),
            Locked(BuildPart.FuelTank, UiText.Plain("Fuel tank"), "0.18.0"),
        ]),
    ];

    private static PartTrayRow Available(BuildPart part, UiText name) => new(part, name, PartTrayRowState.Available, Version: null);

    // The version is the part's GitHub milestone (#992): moving the part to another milestone means updating it here.
    private static PartTrayRow Locked(BuildPart part, UiText name, string version) => new(part, name, PartTrayRowState.ComingLater, version);
}
