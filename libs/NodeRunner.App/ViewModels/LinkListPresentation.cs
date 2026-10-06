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
}

public sealed record LinkListRow(BuildLink Link, UiText Name, LinkListRowState State)
{
    public bool IsPickable => State != LinkListRowState.Locked;
}

/// <summary>
/// The Links list: its rows, what the picked link does (<see cref="PickedInfo"/>, shown right
/// under its row), and one help line for the whole list.
/// </summary>
public sealed record LinkListPresentation(
    IReadOnlyList<LinkListRow> Rows,
    UiText? PickedInfo,
    UiText HelpText);

/// <summary>The Links tool's list: Beam, Piston and Spring now, later links locked.</summary>
public static class BuildLinkList
{
    public static LinkListPresentation Create(BuildLink picked) => new(
        [
            Row(BuildLink.Beam, UiText.Plain("Beam"), picked),
            Row(BuildLink.Piston, UiText.Plain("Piston"), picked),
            Row(BuildLink.Spring, UiText.Plain("Spring"), picked),
            Locked(BuildLink.Wing, UiText.Plain("Wing")),
        ],
        Info(picked),
        HelpText);

    public static bool IsAvailable(BuildLink link) => link is BuildLink.Beam or BuildLink.Piston or BuildLink.Spring;

    public static UiText HelpText { get; } = UiText.Plain("Drag from joint to joint to add the picked link.");

    /// <summary>What the link does, or null for a link that cannot be picked yet.</summary>
    public static UiText? Info(BuildLink link) => link switch
    {
        BuildLink.Beam => PartInfo.Beam,
        BuildLink.Piston => PartInfo.Piston,
        BuildLink.Spring => PartInfo.Spring,
        _ => null,
    };

    private static LinkListRow Row(BuildLink link, UiText name, BuildLink picked) =>
        new(link, name, link == picked ? LinkListRowState.Selected : LinkListRowState.Rest);

    private static LinkListRow Locked(BuildLink link, UiText name) => new(link, name, LinkListRowState.Locked);
}
