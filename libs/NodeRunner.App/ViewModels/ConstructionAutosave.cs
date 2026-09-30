using System.ComponentModel;
using NodeRunner.App.Services;

namespace NodeRunner.App.ViewModels;

/// <summary>
/// Keeps Build's saved creation in step with its drawing (#368). Each edit marks the drawing
/// unsaved and raises <see cref="Changed"/>; the host saves with <see cref="Save"/> once edits
/// settle and whenever Build is left or the app pauses.
/// </summary>
public sealed class ConstructionAutosave : IDisposable
{
    private readonly ConstructionViewModel _construction;
    private readonly IConstructionEditWorkflow _edits;
    private readonly Guid _creationId;
    private readonly bool _openedAsNew;

    public ConstructionAutosave(ConstructionViewModel construction, IConstructionEditWorkflow edits, Guid creationId, bool openedAsNew)
    {
        ArgumentNullException.ThrowIfNull(construction);
        ArgumentNullException.ThrowIfNull(edits);
        _construction = construction;
        _edits = edits;
        _creationId = creationId;
        _openedAsNew = openedAsNew;
        _construction.AnatomyChanged += OnAnatomyChanged;
        _construction.PropertyChanged += OnPropertyChanged;
    }

    /// <summary>Raised on every edit that is not saved yet.</summary>
    public event EventHandler? Changed;

    public Guid CreationId => _creationId;

    public bool HasUnsavedEdits { get; private set; }

    /// <summary>
    /// True when leaving Build should remove the creation rather than save it: + New made it for this
    /// visit (openedAsNew) and it has no nodes now.
    /// </summary>
    public bool ShouldDiscardOnLeave => _openedAsNew && _construction.Nodes.Count == 0;

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

        var saved = _edits.PersistEdit(_creationId, _construction.Snapshot(), _construction.BrainShape, _construction.IsMoveOnly);
        HasUnsavedEdits = saved is null;
        return !HasUnsavedEdits;
    }

    public void Dispose()
    {
        _construction.AnatomyChanged -= OnAnatomyChanged;
        _construction.PropertyChanged -= OnPropertyChanged;
    }

    private void OnAnatomyChanged(object? sender, EventArgs e) => MarkUnsaved();

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ConstructionViewModel.BrainShape))
        {
            MarkUnsaved();
        }
    }

    private void MarkUnsaved()
    {
        HasUnsavedEdits = true;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
