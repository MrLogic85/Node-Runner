using System.ComponentModel;
using NodeRunner.App.Repositories;
using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

public sealed class CreationsPresentationViewModel : INotifyPropertyChanged
{
    private readonly ICreationRepository _repository;
    private readonly List<CreationCardPresentation> _cards = [];

    public CreationsPresentationViewModel(ICreationRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
    }

    public IReadOnlyList<CreationCardPresentation> Cards => _cards;

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool HasCards => _cards.Count > 0;

    public string EmptyText => "No saved Creations yet.";

    public string? ErrorText { get; private set; }

    public Exception? LoadError { get; private set; }

    public bool HasError => ErrorText is not null;

    public void Refresh()
    {
        try
        {
            var cards = new List<CreationCardPresentation>();
            foreach (var creation in _repository.List().OrderBy(creation => creation.Name, StringComparer.CurrentCulture))
            {
                cards.Add(ToCard(creation));
            }

            _cards.Clear();
            _cards.AddRange(cards);
            ErrorText = null;
            LoadError = null;
        }
        catch (Exception exception) when (FilePersistenceExceptions.IsRecoverable(exception))
        {
            ErrorText = "Could not load Creations.";
            LoadError = exception;
        }

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }

    public CreationCommandIntent? RequestOpen(Guid id) => Request(CreationCommandKind.Open, id);

    public CreationCommandIntent? RequestDuplicate(Guid id) => Request(CreationCommandKind.Duplicate, id);

    public CreationCommandIntent? RequestDelete(Guid id) => Request(CreationCommandKind.Delete, id);

    private CreationCommandIntent? Request(CreationCommandKind kind, Guid id) =>
        _cards.Any(card => card.Id == id)
            ? new CreationCommandIntent(kind, id)
            : null;

    private static CreationCardPresentation ToCard(CreationDef creation) =>
        new(
            creation.Id,
            creation.Name,
            creation.Creature,
            creation.Training is null ? "Not trained yet. Tap to build." : string.Empty,
            CreationCardPresentation.ThumbnailTextFor(creation.Creature),
            creation.Training is { } training ? CreationCardTraining.From(training) : null,
            CanOpen: true,
            CanDuplicate: true,
            CanDelete: true);
}
