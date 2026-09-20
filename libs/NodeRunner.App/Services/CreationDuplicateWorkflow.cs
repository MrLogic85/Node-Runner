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

    public CreationDef Duplicate(Guid id, CreationDuplicateMode mode)
    {
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }

        var source = _repository.Get(id) ?? throw new KeyNotFoundException($"Creation '{id}' was not found.");
        var training = mode == CreationDuplicateMode.CopyTraining ? source.Training : null;
        var copy = new CreationDef(_newId(), $"{source.Name} Copy", source.Creature, training);
        _repository.Save(copy);
        return copy;
    }
}
