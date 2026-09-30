using NodeRunner.Domain;

namespace NodeRunner.App.Services;

/// <summary>Saves a saved Creation's Build edits; the lock rule is <see cref="ICreationUpdateCoordinator.ApplyEdit"/>'s.</summary>
public sealed class ConstructionEditWorkflow : IConstructionEditWorkflow
{
    private readonly ICreationUpdateCoordinator _coordinator;

    public ConstructionEditWorkflow(ICreationUpdateCoordinator coordinator)
    {
        ArgumentNullException.ThrowIfNull(coordinator);
        _coordinator = coordinator;
    }

    public ConstructionEditResult PersistEdit(Guid activeCreationId, CreatureDef editedCreature, BrainShapeDef brainShape, bool moveOnly)
    {
        ArgumentNullException.ThrowIfNull(editedCreature);
        ArgumentNullException.ThrowIfNull(brainShape);
        if (activeCreationId == Guid.Empty)
        {
            throw new ArgumentException("An active Creation id is required.", nameof(activeCreationId));
        }

        var updated = _coordinator.ApplyEdit(activeCreationId, editedCreature, brainShape, moveOnly);
        return updated is null
            ? new ConstructionEditResult(null, "Could not save the edited creature; your edit was discarded.")
            : new ConstructionEditResult(updated, $"Saved edits to {updated.Name}.");
    }
}
