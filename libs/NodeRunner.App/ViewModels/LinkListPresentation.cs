namespace NodeRunner.App.ViewModels;

/// <summary>The choices shown in the Links tool's list (#705).</summary>
public enum BuildLink
{
    Beam,
    Piston,
    Spring,
    Wing,
}

public enum LinkListRowState
{
    Rest,
    Selected,
    Locked,

    /// <summary>Implemented, but the creation is locked and the link has brain ports, like a Piston (#896).</summary>
    CreationLocked,
}

/// <summary>A Links row; <c>Version</c> is the version that brings a <see cref="LinkListRowState.Locked"/> row's link (#992), null for the other rows.</summary>
public sealed record LinkListRow(BuildLink Link, UiText Name, LinkListRowState State, string? Version)
{
    public bool IsPickable => State is LinkListRowState.Rest or LinkListRowState.Selected;
}

/// <summary>
/// The Links list: its rows, what the picked link does and how it is drawn (<see cref="PickedInfo"/>,
/// shown right under its row, null with nothing picked), and one help line for the whole list.
/// </summary>
public sealed record LinkListPresentation(
    IReadOnlyList<LinkListRow> Rows,
    UiText? PickedInfo,
    UiText HelpText);

/// <summary>
/// The Links tool's list: Beam, Piston and Spring now, later links locked. On a locked creation the
/// Piston is locked too, since it has brain ports (#896).
/// </summary>
public static class BuildLinkList
{
    public static LinkListPresentation Create(BuildLink? picked, bool creationLocked = false) => new(
        [
            Row(BuildLink.Beam, UiText.Plain("Beam"), picked, creationLocked),
            Row(BuildLink.Piston, UiText.Plain("Piston"), picked, creationLocked),
            Row(BuildLink.Spring, UiText.Plain("Spring"), picked, creationLocked),
            Locked(BuildLink.Wing, UiText.Plain("Wing"), "0.18.0"),
        ],
        picked is { } shown ? PickedInfo(shown) : null,
        HelpText);

    public static bool IsAvailable(BuildLink link) => link is BuildLink.Beam or BuildLink.Piston or BuildLink.Spring;

    /// <summary>What a tap on a locked link row says (#992), as <see cref="PartTray.ComingLaterReason"/>.</summary>
    public static UiText ComingLaterReason(BuildLink link) =>
        Create(BuildLink.Beam).Rows.Single(row => row.Link == link) is { State: LinkListRowState.Locked } row
            ? PartTray.ComingIn(row.Name, row.Version!)
            : throw new ArgumentOutOfRangeException(nameof(link), link, "Only a locked link has a version.");

    /// <summary>Whether drawing <paramref name="link"/> adds brain ports, so a locked creation refuses it (#896).</summary>
    public static bool HasBrainPorts(BuildLink link) => link == BuildLink.Piston;

    public static UiText HelpText { get; } = UiText.Plain("Tap a link to pick it.");

    /// <summary>How the picked link is drawn, under its <see cref="Info"/> (#1057).</summary>
    public static UiText DrawHelp { get; } = UiText.Plain("Drag from joint to joint to add it.");

    /// <summary>What the link does, or null for a link that cannot be picked yet.</summary>
    public static UiText? Info(BuildLink link) => link switch
    {
        BuildLink.Beam => PartInfo.Beam,
        BuildLink.Piston => PartInfo.Piston,
        BuildLink.Spring => PartInfo.Spring,
        _ => null,
    };

    private static UiText PickedInfo(BuildLink link) =>
        Info(link) is { } info ? UiText.Format("{0}\n{1}", info, DrawHelp) : DrawHelp;

    private static LinkListRow Row(BuildLink link, UiText name, BuildLink? picked, bool creationLocked) =>
        new(link, name, creationLocked && HasBrainPorts(link) ? LinkListRowState.CreationLocked
            : link == picked ? LinkListRowState.Selected
            : LinkListRowState.Rest,
            Version: null);

    // The version is kept as PartTray.Locked's (#992).
    private static LinkListRow Locked(BuildLink link, UiText name, string version) => new(link, name, LinkListRowState.Locked, version);
}
