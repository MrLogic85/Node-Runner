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

public sealed record LinkListRow(BuildLink Link, UiText Name, LinkListRowState State)
{
    public bool IsPickable => State is LinkListRowState.Rest or LinkListRowState.Selected;
}

/// <summary>
/// The Links list: its rows, what the picked link does (<see cref="PickedInfo"/>, shown right
/// under its row), and one help line for the whole list.
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
    public static LinkListPresentation Create(BuildLink picked, bool creationLocked = false) => new(
        [
            Row(BuildLink.Beam, UiText.Plain("Beam"), picked, creationLocked),
            Row(BuildLink.Piston, UiText.Plain("Piston"), picked, creationLocked),
            Row(BuildLink.Spring, UiText.Plain("Spring"), picked, creationLocked),
            Locked(BuildLink.Wing, UiText.Plain("Wing")),
        ],
        Info(picked),
        HelpText);

    public static bool IsAvailable(BuildLink link) => link is BuildLink.Beam or BuildLink.Piston or BuildLink.Spring;

    /// <summary>Whether drawing <paramref name="link"/> adds brain ports, so a locked creation refuses it (#896).</summary>
    public static bool HasBrainPorts(BuildLink link) => link == BuildLink.Piston;

    public static UiText HelpText { get; } = UiText.Plain("Drag from joint to joint to add the picked link.");

    /// <summary>What the link does, or null for a link that cannot be picked yet.</summary>
    public static UiText? Info(BuildLink link) => link switch
    {
        BuildLink.Beam => PartInfo.Beam,
        BuildLink.Piston => PartInfo.Piston,
        BuildLink.Spring => PartInfo.Spring,
        _ => null,
    };

    private static LinkListRow Row(BuildLink link, UiText name, BuildLink picked, bool creationLocked) =>
        new(link, name, creationLocked && HasBrainPorts(link) ? LinkListRowState.CreationLocked
            : link == picked ? LinkListRowState.Selected
            : LinkListRowState.Rest);

    private static LinkListRow Locked(BuildLink link, UiText name) => new(link, name, LinkListRowState.Locked);
}
