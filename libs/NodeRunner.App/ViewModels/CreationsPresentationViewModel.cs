using System.ComponentModel;
using NodeRunner.App.Repositories;
using NodeRunner.App.Services;
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

    public string SavedCueText => "Saved";

    public bool HasAchievementCue { get; private set; }

    public bool CanRestoreExample { get; private set; }

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
            CanRestoreExample = cards.All(card => card.Id != DefaultCreationTemplates.StarterWormId);
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
            : "Ready to train";
        var note = creation.Training is null
            ? "Train or edit"
            : "Duplicate copies training";
        var thumbnail = $"{FormatCount(creation.Creature.Nodes.Count, "node")} · {FormatCount(creation.Creature.Beams.Count, "beam")} · {FormatCount(creation.Creature.Cores.Count, "core")}";
        var savedState = creation.Training is null
            ? "Untrained Creation"
            : $"Saved training · generation {creation.Training.Generation}";
        var unlockCredit = progression?.ExtraCoreUnlockedByCreationId == creation.Id
            ? $"Earned extra core unlock · generation {progression.ExtraCoreUnlockedAtGeneration}"
            : string.Empty;
        var isExample = creation.Id == DefaultCreationTemplates.StarterWormId;
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
            isExample ? "Example" : note,
            thumbnail,
            savedState,
            unlockCredit,
            progress,
            progressText,
            isExample,
            CanOpen: true,
            CanEdit: true,
            CanDuplicate: true,
            CanDelete: !isExample);
    }

    private static string FormatCount(int count, string singular) =>
        count == 1
            ? $"1 {singular}"
            : $"{count} {singular}s";
}
