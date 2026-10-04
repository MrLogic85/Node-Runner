using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Services;

public interface ICreationDuplicateWorkflow
{
    /// <inheritdoc cref="INewCreationWorkflow.Create"/>
    CreationDef Duplicate(Guid id, Func<UiText, string> inPlayerLanguage);
}
