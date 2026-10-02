using NodeRunner.App.Repositories;
using NodeRunner.Domain;

namespace NodeRunner.App.Services;

public sealed class CreationDuplicateWorkflow : ICreationDuplicateWorkflow
{
    private readonly ICreationRepository _repository;
    private readonly Func<Guid> _newId;

    public CreationDuplicateWorkflow(ICreationRepository repository, Func<Guid>? newId = null)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
        _newId = newId ?? Guid.NewGuid;
    }

    public CreationDef Duplicate(Guid id)
    {
        var source = _repository.Get(id) ?? throw new KeyNotFoundException($"Creation '{id}' was not found.");
        var copy = new CreationDef(_newId(), $"Copy of {source.Name}", source.Creature, source.Training);
        _repository.Save(copy);
        return copy;
    }
}
