using NodeRunner.Domain;

namespace NodeRunner.App.Services;

/// <summary>Owns the safe persist-before-live-apply rule for move-only edits.</summary>
public sealed class ConstructionEditWorkflow : IConstructionEditWorkflow
{
    private readonly ICreationUpdateCoordinator _coordinator;

    public ConstructionEditWorkflow(ICreationUpdateCoordinator coordinator)
    {
        ArgumentNullException.ThrowIfNull(coordinator);
        _coordinator = coordinator;
    }

    public ConstructionEditResult PersistMoveOnlyEdit(Guid activeCreationId, CreatureDef editedCreature)
    {
        ArgumentNullException.ThrowIfNull(editedCreature);
        if (activeCreationId == Guid.Empty)
        {
            throw new ArgumentException("An active Creation id is required.", nameof(activeCreationId));
        }

        var updated = _coordinator.ApplyCreatureEdit(activeCreationId, editedCreature);
        return updated is null
            ? new ConstructionEditResult(null, "Could not save the edited creature; your edit was discarded.")
            : new ConstructionEditResult(updated, $"Saved edits to {updated.Name}.");
    }
}
