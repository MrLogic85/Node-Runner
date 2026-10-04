using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Services;

public interface IExampleCopyWorkflow
{
    /// <inheritdoc cref="INewCreationWorkflow.Create"/>
    CreationDef Copy(Guid exampleId, Func<UiText, string> inPlayerLanguage);
}
