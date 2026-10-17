using NodeRunner.Domain;

namespace NodeRunner.App.Services;

public interface IBuildEditWorkflow
{
    /// <summary>
    /// Saves the edit and returns the saved creation, or null when it no longer exists. A trained
    /// creation's network is refitted from <paramref name="openedNetwork"/> when given.
    /// </summary>
    CreationDef? PersistEdit(Guid activeCreationId, CreatureDef editedCreature, NeuralNetworkDef? openedNetwork = null);
}
