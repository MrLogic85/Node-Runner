using NodeRunner.Domain;

namespace NodeRunner.App.Services;

public interface INewCreationWorkflow
{
    /// <summary>Saves and returns a new, empty creation.</summary>
    CreationDef Create();
}
