using NodeRunner.App.Services;

namespace NodeRunner.App.ViewModels;

/// <summary>
/// Keeps Build's saved creation in step with its drawing (#368). Each edit marks the drawing
/// unsaved and raises <see cref="Changed"/>; the host saves with <see cref="Save"/> once edits
/// settle and whenever Build is left or the app pauses.
/// </summary>
public sealed class BuildAutosave : IDisposable
{
    private readonly BuildViewModel _build;
    private readonly IBuildEditWorkflow _edits;
    private readonly Guid _creationId;
    private readonly bool _openedAsNew;

    public BuildAutosave(BuildViewModel build, IBuildEditWorkflow edits, Guid creationId, bool openedAsNew)
    {
        ArgumentNullException.ThrowIfNull(build);
        ArgumentNullException.ThrowIfNull(edits);
        _build = build;
        _edits = edits;
        _creationId = creationId;
        _openedAsNew = openedAsNew;
        _build.AnatomyChanged += OnAnatomyChanged;
    }

    /// <summary>Raised on every edit that is not saved yet.</summary>
    public event EventHandler? Changed;

    public Guid CreationId => _creationId;

    public bool HasUnsavedEdits { get; private set; }

    /// <summary>
    /// True when leaving Build should remove the creation rather than save it: + New made it for this
    /// visit (openedAsNew) and it has no nodes now.
    /// </summary>
    public bool ShouldDiscardOnLeave => _openedAsNew && _build.Nodes.Count == 0;

    /// <summary>
    /// Saves the drawing if it has unsaved edits. Returns false when edits are left unsaved because
    /// the creation is gone; a failed write throws and also leaves them unsaved, so the next save
    /// tries again.
    /// </summary>
    public bool Save()
    {
        if (!HasUnsavedEdits)
        {
            return true;
        }

        var saved = _edits.PersistEdit(_creationId, _build.Snapshot(), _build.IsMoveOnly);
        HasUnsavedEdits = saved is null;
        return !HasUnsavedEdits;
    }

    public void Dispose()
    {
        _build.AnatomyChanged -= OnAnatomyChanged;
    }

    private void OnAnatomyChanged(object? sender, EventArgs e) => MarkUnsaved();

    private void MarkUnsaved()
    {
        HasUnsavedEdits = true;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
