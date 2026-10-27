using System.ComponentModel;
using NodeRunner.App.Services;

namespace NodeRunner.App.ViewModels;

/// <summary>
/// Keeps Build's saved creation in step with its drawing and grid. Each edit marks it
/// unsaved and raises <see cref="Changed"/>; the host saves with <see cref="Save"/> once edits
/// settle and whenever Build is left or the app pauses.
/// </summary>
public sealed class BuildAutosave : IDisposable
{
    private readonly BuildViewModel _build;
    private readonly IBuildEditWorkflow _edits;
    private readonly Guid _creationId;
    private readonly bool _openedAsNew;
    private bool _creatureUnsaved;
    private bool _gridUnsaved;

    public BuildAutosave(BuildViewModel build, IBuildEditWorkflow edits, Guid creationId, bool openedAsNew)
    {
        ArgumentNullException.ThrowIfNull(build);
        ArgumentNullException.ThrowIfNull(edits);
        _build = build;
        _edits = edits;
        _creationId = creationId;
        _openedAsNew = openedAsNew;
        _build.AnatomyChanged += OnAnatomyChanged;
        _build.PropertyChanged += OnPropertyChanged;
    }

    /// <summary>Raised on every edit that is not saved yet.</summary>
    public event EventHandler? Changed;

    public Guid CreationId => _creationId;

    public bool HasUnsavedEdits => _creatureUnsaved || _gridUnsaved;

    /// <summary>
    /// True when leaving Build should remove the creation rather than save it: + New made it for this
    /// visit (openedAsNew) and it has no nodes now.
    /// </summary>
    public bool ShouldDiscardOnLeave => _openedAsNew && _build.Nodes.Count == 0;

    /// <summary>
    /// Saves the drawing and the grid where they have unsaved edits. Returns false when edits are left unsaved because
    /// the creation is gone; a failed write throws and also leaves them unsaved, so the next save
    /// tries again.
    /// </summary>
    public bool Save()
    {
        if (_creatureUnsaved)
        {
            if (_edits.PersistEdit(_creationId, _build.Snapshot(), _build.OpenedNetwork) is null)
            {
                return false;
            }

            _creatureUnsaved = false;
        }

        // Saved apart from the creature, so a grid change alone never refits the network.
        if (_gridUnsaved)
        {
            if (_edits.PersistGrid(_creationId, _build.Grid) is null)
            {
                return false;
            }

            _gridUnsaved = false;
        }

        return true;
    }

    public void Dispose()
    {
        _build.AnatomyChanged -= OnAnatomyChanged;
        _build.PropertyChanged -= OnPropertyChanged;
    }

    private void OnAnatomyChanged(object? sender, EventArgs e)
    {
        _creatureUnsaved = true;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BuildViewModel.Grid))
        {
            _gridUnsaved = true;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
