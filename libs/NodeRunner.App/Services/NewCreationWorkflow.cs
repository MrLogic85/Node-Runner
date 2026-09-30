using NodeRunner.App.Repositories;
using NodeRunner.Domain;

namespace NodeRunner.App.Services;

/// <summary>
/// + New on Creations: saves an empty creation straight away, so Build always edits a saved one and
/// every edit autosaves (#368).
/// </summary>
public sealed class NewCreationWorkflow : INewCreationWorkflow
{
    public const string UntitledName = "Untitled Creation";

    private readonly ICreationRepository _repository;
    private readonly Func<Guid> _newId;

    public NewCreationWorkflow(ICreationRepository repository, Func<Guid>? newId = null)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
        _newId = newId ?? Guid.NewGuid;
    }

    public CreationDef Create()
    {
        var creation = new CreationDef(_newId(), UntitledName, new CreatureDef([], [], []), BrainShapeDef.Default);
        _repository.Save(creation);
        return creation;
    }
}
