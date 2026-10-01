namespace NodeRunner.App.ViewModels;

/// <summary>Every part the Build tray lists, implemented or not (#374).</summary>
public enum BuildPart
{
    Spring,
    Piston,
    Wing,
    Brake,
    Servo,
    Stepper,
    VelocityMotor,
    Wheel,
    Accelerometer,
    LosSensor,
    Core,
    Battery,
    Generator,
    FuelTank,
}

public enum PartTrayRowState
{
    Available,
    Selected,
    ComingLater,
}

public sealed record PartTrayRow(BuildPart Part, string Name, PartTrayRowState State)
{
    public bool IsAvailable => State != PartTrayRowState.ComingLater;

    /// <summary>Why the row cannot be picked, or empty when it can.</summary>
    public string LockedReason => IsAvailable ? string.Empty : PartTray.ComingLater;
}

public sealed record PartTrayGroup(string Name, string HelpText, IReadOnlyList<PartTrayRow> Rows)
{
    /// <summary>Explains the locked rows once for the whole tab, or empty when none is locked.</summary>
    public string LockedNote => Rows.Any(row => !row.IsAvailable) ? PartTray.ComingLater : string.Empty;
}

/// <summary>
/// The Build Parts tray: four tabs of reference parts (#374). Every implemented part is unlimited
/// until achievements arrive (#525), so rows carry no counts; a part not yet implemented shows
/// "Coming later". Core is a transitional row with its own tool until #127 removes it. While a
/// part's tool is active, its tab's help line is that tool's hint.
/// </summary>
public static class PartTray
{
    public const string ComingLater = "Coming later";

    public static IReadOnlyList<PartTrayGroup> Groups(BuildTool activeTool) =>
        [.. Catalog(activeTool).Select(group => group.Rows.Any(row => row.State == PartTrayRowState.Selected)
            ? group with { HelpText = BuildPresentationViewModel.ToolHint(activeTool) }
            : group)];

    /// <summary>The Build tool a tap on the part's row turns on, or null while the part is not implemented.</summary>
    public static BuildTool? ToolFor(BuildPart part) => part switch
    {
        BuildPart.Core => BuildTool.Core,
        _ => null,
    };

    /// <summary>Whether a tray row, rather than the rail, turns this tool on.</summary>
    public static bool IsTrayTool(BuildTool tool) => Enum.GetValues<BuildPart>().Any(part => ToolFor(part) == tool);

    /// <summary>
    /// The tool after opening tab <paramref name="group"/>: a part's tool belongs to its tab, so
    /// opening another tab puts Move back; null keeps the active tool.
    /// </summary>
    public static BuildTool? ToolOnTabOpened(BuildTool activeTool, int group) =>
        IsTrayTool(activeTool) && Groups(activeTool)[group].Rows.All(row => row.State != PartTrayRowState.Selected)
            ? BuildTool.Move
            : null;

    private static PartTrayGroup[] Catalog(BuildTool activeTool) =>
    [
        new("Links", "Pick one, then drag from one node to another, like the Beam tool.",
        [
            Row(BuildPart.Spring, "Spring", activeTool),
            Row(BuildPart.Piston, "Piston", activeTool),
            Row(BuildPart.Wing, "Wing", activeTool),
        ]),
        new("On a joint", "Drag onto a joint. A joint holds one part.",
        [
            Row(BuildPart.Brake, "Brake", activeTool),
            Row(BuildPart.Servo, "Servo", activeTool),
            Row(BuildPart.Stepper, "Stepper", activeTool),
            Row(BuildPart.VelocityMotor, "Velocity motor", activeTool),
            Row(BuildPart.Wheel, "Wheel", activeTool),
        ]),
        new("Sensors", "Drag onto a beam. A beam holds one of each sensor.",
        [
            Row(BuildPart.Accelerometer, "Accelerometer", activeTool),
            Row(BuildPart.LosSensor, "LOS sensor", activeTool),
            Row(BuildPart.Core, "Core", activeTool),
        ]),
        new("Blocks", "Drag it onto the canvas, then draw beams to its two eyes.",
        [
            Row(BuildPart.Battery, "Battery", activeTool),
            Row(BuildPart.Generator, "Generator", activeTool),
            Row(BuildPart.FuelTank, "Fuel tank", activeTool),
        ]),
    ];

    private static PartTrayRow Row(BuildPart part, string name, BuildTool activeTool) =>
        new(part, name, ToolFor(part) switch
        {
            null => PartTrayRowState.ComingLater,
            var tool when tool == activeTool => PartTrayRowState.Selected,
            _ => PartTrayRowState.Available,
        });
}
