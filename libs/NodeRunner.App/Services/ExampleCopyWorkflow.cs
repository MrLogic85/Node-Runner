using NodeRunner.App.Repositories;
using NodeRunner.Domain;

namespace NodeRunner.App.Services;

/// <summary>Saves an identical creation, trained model included, from an example under a new id.</summary>
public sealed class ExampleCopyWorkflow : IExampleCopyWorkflow
{
    private readonly ICreationRepository _repository;
    private readonly IReadOnlyList<CreationExample> _examples;
    private readonly Func<Guid> _newId;

    public ExampleCopyWorkflow(
        ICreationRepository repository,
        IReadOnlyList<CreationExample>? examples = null,
        Func<Guid>? newId = null)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
        _examples = examples ?? CreationExamples.All;
        _newId = newId ?? Guid.NewGuid;
    }

    public CreationDef Copy(Guid exampleId)
    {
        var example = _examples.FirstOrDefault(example => example.Creation.Id == exampleId)?.Creation
            ?? throw new KeyNotFoundException($"Example '{exampleId}' was not found.");
        var copy = new CreationDef(_newId(), example.Name, example.Creature, example.BrainShape, example.Training, example.IsLocked);
        _repository.Save(copy);
        return copy;
    }
}
