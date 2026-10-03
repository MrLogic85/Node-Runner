using NodeRunner.Domain;

namespace NodeRunner.App.Services;

/// <summary>Saves a saved Creation's Build edits; how training is kept is <see cref="ICreationUpdateCoordinator.ApplyEdit"/>'s.</summary>
public sealed class BuildEditWorkflow : IBuildEditWorkflow
{
    private readonly ICreationUpdateCoordinator _coordinator;

    public BuildEditWorkflow(ICreationUpdateCoordinator coordinator)
    {
        ArgumentNullException.ThrowIfNull(coordinator);
        _coordinator = coordinator;
    }

    public CreationDef? PersistEdit(Guid activeCreationId, CreatureDef editedCreature)
    {
        ArgumentNullException.ThrowIfNull(editedCreature);
        if (activeCreationId == Guid.Empty)
        {
            throw new ArgumentException("An active Creation id is required.", nameof(activeCreationId));
        }

        return _coordinator.ApplyEdit(activeCreationId, editedCreature);
    }
}
