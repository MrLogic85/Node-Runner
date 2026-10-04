namespace NodeRunner.App.ViewModels;

public enum ToolPanelMode
{
    None,
    PartsTray,
    LinkList,
    JointHelp,
    SelectHelp,
}

public sealed record ToolPanelPresentation(
    ToolPanelMode Mode,
    UiText? Title)
{
    public static ToolPanelPresentation None { get; } = new(ToolPanelMode.None, null);
}
