using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Services;

public interface INewCreationWorkflow
{
    /// <summary>
    /// Saves and returns a new, empty creation. <paramref name="inPlayerLanguage"/> writes its default
    /// name in the player's language once, as it is saved; from then on the name is the player's own
    /// text and is never translated again (#759).
    /// </summary>
    CreationDef Create(Func<UiText, string> inPlayerLanguage);
}
