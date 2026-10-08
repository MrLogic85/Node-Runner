using NodeRunner.Domain;

namespace NodeRunner.App.Services;

public interface ICreationImportWorkflow
{
    /// <summary>
    /// Saves and returns <paramref name="build"/>, read from a share code (#899), as a new, untrained
    /// creation named <paramref name="name"/>.
    /// </summary>
    CreationDef Import(CreationDef build, string name);
}
