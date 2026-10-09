using NodeRunner.App.Repositories;
using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

/// <summary>What the Import screen shows (#899).</summary>
public enum ImportState
{
    /// <summary>Nothing pasted yet.</summary>
    Waiting,

    /// <summary>A build that Add to Creations saves.</summary>
    Preview,

    /// <summary>The last paste was refused; the note says why.</summary>
    Refused,
}

/// <summary>One kind of part in the previewed build, and how many it has.</summary>
public sealed record ImportPartRow(PartSettingsKind Kind, int Count);

/// <summary>
/// The Import screen (#899) for the last paste: the build, the name its Name field starts with and
/// its parts by kind; or a note on what to do or why the paste was refused. <paramref name="TakenNames"/>
/// are the player's creations' names, for the line under the Name field (#1061).
/// </summary>
public sealed record ImportPresentation(
    ImportState State,
    CreationDef? Build,
    string? Name,
    IReadOnlyList<ImportPartRow> Parts,
    UiText? NoteTitle,
    UiText? NoteText,
    IReadOnlyList<string> TakenNames)
{
    public static ImportPresentation Waiting { get; } =
        new(ImportState.Waiting, null, null, [], null, UiText.Plain("Copy a creation's share code, then tap Paste."), []);

    /// <summary>
    /// The warning that a code from an older version was updated to this one, so its build may
    /// differ from the one shared (#1094); null for a code in this version.
    /// </summary>
    public UiText? UpdatedNote { get; init; }

    public bool CanAdd => State == ImportState.Preview;

    /// <summary>The next step is the one Primary (#1061): Paste until a build shows, then Add to Creations.</summary>
    public bool PasteIsPrimary => !CanAdd;

    /// <summary>
    /// The screen for <paramref name="read"/>; the Name field starts with the build's own name, even
    /// one already in <paramref name="takenNames"/>, since two creations may share a name.
    /// </summary>
    public static ImportPresentation For(ShareCodeRead read, IReadOnlyList<string> takenNames)
    {
        ArgumentNullException.ThrowIfNull(read);
        ArgumentNullException.ThrowIfNull(takenNames);
        if (read.Build is not { } build)
        {
            var (title, text) = Refusal(read.Refusal ?? ShareCodeRefusal.Damaged);
            return new(ImportState.Refused, null, null, [], title, text, takenNames);
        }

        return new(ImportState.Preview, build, build.Name, PartsOf(build.Creature), null, null, takenNames)
        {
            UpdatedNote = read.UpdatedFrom is null ? null : UiText.Plain("Shared from an older version, import may differ from original."),
        };
    }

    /// <summary>
    /// The muted line under the Name field while a creation has <paramref name="name"/>, or null. It does not
    /// repeat the name: an article cannot fit every name or language, and the name shows just above.
    /// </summary>
    public UiText? NameLine(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var trimmed = name.Trim();
        return CanAdd && trimmed.Length > 0 && CreationNames.IsTaken(trimmed, TakenNames)
            ? UiText.Plain("You already have one by this name.")
            : null;
    }

    private static (UiText Title, UiText Text) Refusal(ShareCodeRefusal refusal) => refusal switch
    {
        ShareCodeRefusal.Empty => (UiText.Plain("Nothing to paste"), UiText.Plain("Copy a creation's share code first, then tap Paste.")),
        ShareCodeRefusal.NotACreation => (UiText.Plain("Not a share code"), UiText.Plain("This text is not a Node Runner creation.")),
        ShareCodeRefusal.NewerVersion => (UiText.Plain("Made in a newer version"), UiText.Plain("Update Node Runner to open this creation.")),
        ShareCodeRefusal.NothingToBuild => (UiText.Plain("Nothing to build"), UiText.Plain("This creation has no parts yet.")),
        _ => (UiText.Plain("This code is damaged"), UiText.Plain("Part of it is missing or changed. Ask for the code again.")),
    };

    // In the Parts tray's order, which the screen's authored rows follow.
    private static ImportPartRow[] PartsOf(CreatureDef creature) =>
    [
        .. new ImportPartRow[]
        {
            new(PartSettingsKind.Node, creature.Nodes.Count),
            new(PartSettingsKind.Beam, creature.Beams.Count),
            new(PartSettingsKind.Accelerometer, creature.Sensors.Count(sensor => sensor.Kind == SensorKind.Accelerometer)),
            new(PartSettingsKind.Camera, creature.Sensors.Count(sensor => sensor.Kind == SensorKind.Camera)),
            new(PartSettingsKind.Servo, creature.Servos.Count),
            new(PartSettingsKind.Piston, creature.Pistons.Count),
            new(PartSettingsKind.Spring, creature.Springs.Count),
            new(PartSettingsKind.Wheel, creature.Wheels.Count),
        }.Where(row => row.Count > 0),
    ];
}
