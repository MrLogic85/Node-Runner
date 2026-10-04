using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Services;

public interface ICreationDuplicateWorkflow
{
    /// <summary>
    /// Saves and returns a copy of creation <paramref name="id"/>, training included, named
    /// "Copy of …" in the player's language; see <see cref="INewCreationWorkflow.Create"/>.
    /// </summary>
    CreationDef Duplicate(Guid id, Func<UiText, string> inPlayerLanguage);
}
