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

public sealed record LinkListRow(BuildLink Link, UiText Name, LinkListRowState State)
{
    public bool IsPickable => State != LinkListRowState.Locked;
}

public sealed record LinkListPresentation(
    UiText Name,
    UiText? LockedNote,
    IReadOnlyList<LinkListRow> Rows,
    UiText? HelpText);

/// <summary>The Beams tool's list: Beam, Piston and Spring now, later links locked.</summary>
public static class BuildLinkList
{
    public static LinkListPresentation Create(BuildLink picked) => new(
        UiText.Plain("Links"),
        PartTray.ComingLater,
        [
            Row(BuildLink.Beam, UiText.Plain("Beam"), picked),
            Row(BuildLink.Piston, UiText.Plain("Piston"), picked),
            Row(BuildLink.Spring, UiText.Plain("Spring"), picked),
            Locked(BuildLink.Wing, UiText.Plain("Wing")),
        ],
        HelpText(picked));

    public static bool IsAvailable(BuildLink link) => link is BuildLink.Beam or BuildLink.Piston or BuildLink.Spring;

    /// <summary>How to use the picked link, or null for a link that cannot be picked yet.</summary>
    public static UiText? HelpText(BuildLink link) => link switch
    {
        BuildLink.Beam => UiText.Plain("A rigid rod. Drag joint to joint."),
        BuildLink.Piston => UiText.Plain("The brain pushes and pulls it. Drag joint to joint."),
        BuildLink.Spring => UiText.Plain("Springs back to its drawn length. Drag joint to joint."),
        _ => null,
    };

    private static LinkListRow Row(BuildLink link, UiText name, BuildLink picked) =>
        new(link, name, link == picked ? LinkListRowState.Selected : LinkListRowState.Rest);

    private static LinkListRow Locked(BuildLink link, UiText name) => new(link, name, LinkListRowState.Locked);
}
