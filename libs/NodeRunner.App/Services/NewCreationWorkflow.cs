using NodeRunner.App.Repositories;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Services;

/// <summary>
/// + New on Creations: saves an empty creation straight away, so Build always edits a saved one and
/// every edit autosaves. It starts on Settings' grid default.
/// </summary>
public sealed class NewCreationWorkflow : INewCreationWorkflow
{
    public static UiText UntitledName { get; } = UiText.Plain("Untitled Creation");

    private readonly ICreationRepository _repository;
    private readonly Func<BuildGridDef> _defaultGrid;
    private readonly Func<Guid> _newId;

    public NewCreationWorkflow(ICreationRepository repository, Func<BuildGridDef> defaultGrid, Func<Guid>? newId = null)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(defaultGrid);
        _repository = repository;
        _defaultGrid = defaultGrid;
        _newId = newId ?? Guid.NewGuid;
    }

    public CreationDef Create(Func<UiText, string> inPlayerLanguage)
    {
        ArgumentNullException.ThrowIfNull(inPlayerLanguage);
        var creation = new CreationDef(_newId(), inPlayerLanguage(UntitledName), new CreatureDef([], nextPartId: 1)) { Grid = _defaultGrid() };
        _repository.Save(creation);
        return creation;
    }
}
