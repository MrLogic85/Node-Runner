using System.ComponentModel;
using NodeRunner.App.Repositories;
using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

public sealed class CreationsPresentationViewModel : INotifyPropertyChanged
{
    private readonly ICreationRepository _repository;
    private readonly IProgressionRepository? _progressionRepository;
    private readonly List<CreationCardPresentation> _cards = [];

    public CreationsPresentationViewModel(ICreationRepository repository, IProgressionRepository? progressionRepository = null)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
        _progressionRepository = progressionRepository;
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
            var progression = _progressionRepository?.Load();
            var cards = new List<CreationCardPresentation>();
            foreach (var creation in _repository.List())
            {
                cards.Add(ToCard(creation, progression));
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

    public CreationCommandIntent? RequestEdit(Guid id) => Request(CreationCommandKind.Edit, id);

    public CreationCommandIntent? RequestDuplicate(Guid id) => Request(CreationCommandKind.Duplicate, id);

    public CreationCommandIntent? RequestDelete(Guid id) => Request(CreationCommandKind.Delete, id);

    private CreationCommandIntent? Request(CreationCommandKind kind, Guid id) =>
        _cards.Any(card => card.Id == id)
            ? new CreationCommandIntent(kind, id)
            : null;

    private static CreationCardPresentation ToCard(CreationDef creation, ProgressionDef? progression)
    {
        var summary = creation.Training is { } training
            ? $"Generation {training.Generation} · trained brain"
            : "Untrained";
        var note = creation.Training is null
            ? "Start fresh"
            : "Duplicate copies training";
        var thumbnail = $"{FormatCount(creation.Creature.Nodes.Count, "node")} · {FormatCount(creation.Creature.Beams.Count, "beam")} · {FormatCount(creation.Creature.Cores.Count, "core")}";
        var savedState = creation.Training is null
            ? "Saved draft"
            : $"Saved training · generation {creation.Training.Generation}";
        var unlockCredit = progression?.ExtraCoreUnlockedByCreationId == creation.Id
            ? $"Earned extra core unlock · generation {progression.ExtraCoreUnlockedAtGeneration}"
            : string.Empty;

        return new CreationCardPresentation(
            creation.Id,
            creation.Name,
            summary,
            note,
            thumbnail,
            savedState,
            unlockCredit,
            CanOpen: true,
            CanEdit: true,
            CanDuplicate: true,
            CanDelete: true);
    }

    private static string FormatCount(int count, string singular) =>
        count == 1
            ? $"1 {singular}"
            : $"{count} {singular}s";
}
