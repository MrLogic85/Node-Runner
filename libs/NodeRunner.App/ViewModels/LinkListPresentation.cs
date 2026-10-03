namespace NodeRunner.App.ViewModels;

/// <summary>The choices shown in the Beams tool's link list (#705).</summary>
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
}

public sealed record LinkListRow(BuildLink Link, string Name, LinkListRowState State)
{
    public bool IsPickable => State != LinkListRowState.Locked;
}

public sealed record LinkListPresentation(
    string Name,
    string LockedNote,
    IReadOnlyList<LinkListRow> Rows,
    string HelpText);

/// <summary>The Beams tool's list: Beam and Piston now, later links locked.</summary>
public static class BuildLinkList
{
    public static LinkListPresentation Create(BuildLink picked) => new(
        "Links",
        PartTray.ComingLater,
        [
            Row(BuildLink.Beam, "Beam", picked),
            Row(BuildLink.Piston, "Piston", picked),
            Locked(BuildLink.Spring, "Spring"),
            Locked(BuildLink.Wing, "Wing"),
        ],
        HelpText(picked));

    public static bool IsAvailable(BuildLink link) => link is BuildLink.Beam or BuildLink.Piston;

    public static string HelpText(BuildLink link) => link switch
    {
        BuildLink.Beam => "A rigid rod. Drag joint to joint.",
        BuildLink.Piston => "The brain pushes and pulls it. Drag joint to joint.",
        _ => string.Empty,
    };

    private static LinkListRow Row(BuildLink link, string name, BuildLink picked) =>
        new(link, name, link == picked ? LinkListRowState.Selected : LinkListRowState.Rest);

    private static LinkListRow Locked(BuildLink link, string name) => new(link, name, LinkListRowState.Locked);
}
