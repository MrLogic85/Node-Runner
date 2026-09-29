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

    public bool HasAchievementCue { get; private set; }

    /// <summary>Marks the Achievements button while something is new; empty hides the badge.</summary>
    public string AchievementBadgeText => HasAchievementCue ? "!" : string.Empty;

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
            HasAchievementCue = progression?.ExtraCoreUnlocked == true;
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

    private static CreationCardPresentation ToCard(CreationDef creation, ProgressionDef? progression)
    {
        var summary = creation.Training is { } training
            ? $"Generation {training.Generation} · trained brain"
            : "Ready to train";
        var savedState = creation.Training is null
            ? "Untrained Creation"
            : $"Saved training · generation {creation.Training.Generation}";
        var unlockCredit = progression?.ExtraCoreUnlockedByCreationId == creation.Id
            ? $"Earned extra core unlock · generation {progression.ExtraCoreUnlockedAtGeneration}"
            : string.Empty;
        var progress = unlockCredit.Length > 0 ? 1f : creation.Training is { Generation: > 0 } trainingProgress
            ? Math.Clamp(trainingProgress.Generation / 20f, 0f, 0.95f)
            : 0f;
        var progressText = unlockCredit.Length > 0
            ? "Achievement complete"
            : progress > 0
                ? "Achievement progress"
                : string.Empty;

        return new CreationCardPresentation(
            creation.Id,
            creation.Name,
            creation.Creature,
            summary,
            CreationCardPresentation.ThumbnailTextFor(creation.Creature),
            savedState,
            unlockCredit,
            progress,
            progressText,
            CanOpen: true,
            CanDuplicate: true,
            CanDelete: true);
    }
}
