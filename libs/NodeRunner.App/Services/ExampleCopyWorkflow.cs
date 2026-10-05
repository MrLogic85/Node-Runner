using NodeRunner.App.Repositories;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Services;

/// <summary>
/// Saves an identical creation, trained model included, from an example under a new id and the
/// example's name in the player's language, or "Copy of …" when a creation already has that name
/// (#840).
/// </summary>
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

    public CreationDef Copy(Guid exampleId, Func<UiText, string> inPlayerLanguage)
    {
        ArgumentNullException.ThrowIfNull(inPlayerLanguage);
        var example = _examples.FirstOrDefault(example => example.Id == exampleId)
            ?? throw new KeyNotFoundException($"Example '{exampleId}' was not found.");
        var name = inPlayerLanguage(example.Name);
        if (_repository.List().Any(creation => string.Equals(creation.Name, name, StringComparison.CurrentCultureIgnoreCase)))
        {
            name = NameLimits.Fit(inPlayerLanguage(UiText.Format("Copy of {0}", name)), NameLimits.Creation);
        }

        var copy = new CreationDef(_newId(), name, example.Creature, example.Training);
        _repository.Save(copy);
        return copy;
    }
}
