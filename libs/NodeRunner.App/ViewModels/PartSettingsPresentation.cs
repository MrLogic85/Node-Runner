namespace NodeRunner.App.ViewModels;

/// <summary>Which kind of part the Part settings panel shows; the screen picks its glyph from this.</summary>
public enum PartSettingsKind
{
    Node,
    Beam,
    Accelerometer,
    Camera,
}

/// <summary>
/// The Part settings panel for one selected part (#343): its Name first, then what it is joined to,
/// a short note, and Delete unless the Creation is locked. Structure is read-only here; it is drawn
/// and changed on the canvas.
/// </summary>
public sealed record PartSettingsPresentation(
    int Id,
    PartSettingsKind Kind,
    string Name,
    string DefaultName,
    string ConnectionsLabel,
    string ConnectionsValue,
    string Note,
    bool CanDelete);
