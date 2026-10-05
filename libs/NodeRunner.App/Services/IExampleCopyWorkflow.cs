using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Services;

public interface IExampleCopyWorkflow
{
    /// <summary>
    /// Saves and returns a copy of example <paramref name="exampleId"/> under its name in the
    /// player's language, or "Copy of …" when that name is taken; see
    /// <see cref="INewCreationWorkflow.Create"/>.
    /// </summary>
    CreationDef Copy(Guid exampleId, Func<UiText, string> inPlayerLanguage);
}
