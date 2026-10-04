using NodeRunner.Domain;

namespace NodeRunner.App.Services;

public interface IBuildEditWorkflow
{
    /// <summary>
    /// Saves the edit and returns the saved creation, or null when it no longer exists. A trained
    /// creation's brain is refitted from <paramref name="openedBrain"/> when given (#689).
    /// </summary>
    CreationDef? PersistEdit(Guid activeCreationId, CreatureDef editedCreature, BrainDef? openedBrain = null);
}
