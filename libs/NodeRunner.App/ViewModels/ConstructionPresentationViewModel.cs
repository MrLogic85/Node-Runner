namespace NodeRunner.App.ViewModels;

/// <summary>
/// Pure presentation adapter for construction-mode shell state. Godot owns
/// nodes/input/rendering; this class owns stable labels, hints, and command
/// visibility so target screens can reuse the same contract without copying
/// `Main.cs` formatting rules.
/// </summary>
public sealed class ConstructionPresentationViewModel
{
    private readonly ConstructionViewModel _construction;

    public ConstructionPresentationViewModel(ConstructionViewModel construction)
    {
        _construction = construction ?? throw new ArgumentNullException(nameof(construction));
    }

    public string BuildModeButtonText => _construction.IsActive ? "Simulate" : "Build";

    public string InspectorTitle => "Building";

    public string InspectorRole => $"Tool: {_construction.ActiveTool}";

    public string InspectorValues => _construction.StatusMessage ?? ToolHint(_construction.ActiveTool);

    public bool ShowConstructionTools => !_construction.IsMoveOnly;

    public bool ShowCompleteAction => !_construction.IsMoveOnly;

    public bool ShowRebuildAction => _construction.IsMoveOnly;

    public string CoreToolText
    {
        get
        {
            var unlockHint = _construction.MaxCores > 1 ? "unlocked" : "50 fitness";
            return $"Core {_construction.Cores.Count}/{_construction.MaxCores} ({unlockHint})";
        }
    }

    public string CoreToolTooltip => _construction.MaxCores > 1
        ? "Attach or remove a core. Extra core slot unlocked."
        : "Attach or remove a core. Train to unlock a second core slot.";

    public static string ToolHint(ConstructionTool tool)
    {
        return tool switch
        {
            ConstructionTool.Place => "Tap empty space to place a node. Drag a node to move it.",
            ConstructionTool.Beam => "Tap a node, then another node, to connect them with a beam.",
            ConstructionTool.Core => "Tap a node to attach a core, tap again to remove it.",
            ConstructionTool.Delete => "Tap a node or beam to delete it.",
            _ => string.Empty,
        };
    }
}
