using NodeRunner.App.Repositories;
using NodeRunner.App.ViewModels;
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

    public CreationDef Duplicate(Guid id, Func<UiText, string> inPlayerLanguage)
    {
        ArgumentNullException.ThrowIfNull(inPlayerLanguage);
        var source = _repository.Get(id) ?? throw new KeyNotFoundException($"Creation '{id}' was not found.");
        var name = NameLimits.Fit(inPlayerLanguage(CreationNames.CopyOf(source.Name)), NameLimits.Creation);
        var copy = source.CopyAs(_newId(), name);
        _repository.Save(copy);
        return copy;
    }
}
