using NodeRunner.Domain;

namespace NodeRunner.App.Services;

public interface IBuildEditWorkflow
{
    /// <summary>Saves the edit and returns the saved creation, or null when it no longer exists.</summary>
    CreationDef? PersistEdit(Guid activeCreationId, CreatureDef editedCreature, bool moveOnly);
}
