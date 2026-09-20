using Godot;
using NodeRunner.App.Repositories;
using NodeRunner.App.Services;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.Managers;

/// <summary>Composition-root facade for durable Creations.</summary>
public partial class SaveManager : Node
{
    private ICreationRepository? _repository;
    private IProgressionRepository? _progressionRepository;
    private ICreationUpdateCoordinator? _updateCoordinator;
    private IConstructionDraftWorkflow? _constructionDraftWorkflow;
    private IConstructionEditWorkflow? _constructionEditWorkflow;
    private ICreationDuplicateWorkflow? _creationDuplicateWorkflow;
    private CreationsPresentationViewModel? _creationsPresentation;

    public override void _Ready()
    {
        var directory = ProjectSettings.GlobalizePath("user://creations");
        _repository = new FileCreationRepository(new GodotStorageLocation(directory));
        _updateCoordinator = new CreationUpdateCoordinator(_repository);
        _constructionDraftWorkflow = new ConstructionDraftWorkflow();
        _constructionEditWorkflow = new ConstructionEditWorkflow(_updateCoordinator);
        _creationDuplicateWorkflow = new CreationDuplicateWorkflow(_repository);
        _creationsPresentation = new CreationsPresentationViewModel(_repository);
        var progressionDirectory = ProjectSettings.GlobalizePath("user://progression");
        _progressionRepository = new FileProgressionRepository(new GodotStorageLocation(progressionDirectory));
    }

    public IReadOnlyList<CreationDef> List()
    {
        return Repository.List();
    }

    public void Save(CreationDef creation)
    {
        Repository.Save(creation);
    }

    // Delete/ResetTraining/ApplyCreatureEdit/UpdateIfPresent/
    // CurrentTrainingEpoch/TryPersistTraining all delegate to
    // UpdateCoordinator, which serializes mutations per Creation id and
    // invalidates queued background training snapshots (see #113) that a
    // reset, edit, or delete has superseded.
    public bool Delete(Guid id) => UpdateCoordinator.Delete(id);

    public CreationDef? Get(Guid id)
    {
        return Repository.Get(id);
    }

    public ConstructionEditResult PersistMoveOnlyEdit(Guid id, CreatureDef editedCreature) =>
        ConstructionEditWorkflow.PersistMoveOnlyEdit(id, editedCreature);

    public void ResetTraining(Guid id) => UpdateCoordinator.ResetTraining(id);

    public long CurrentTrainingEpoch(Guid id) => UpdateCoordinator.CurrentTrainingEpoch(id);

    public bool TryPersistTraining(Guid id, long expectedEpoch, TrainingStateDef training) =>
        UpdateCoordinator.TryPersistTraining(id, expectedEpoch, training);

    public CreationDef? UpdateIfPresent(Guid id, Func<CreationDef, CreationDef> update) =>
        UpdateCoordinator.UpdateIfPresent(id, update);

    public IConstructionDraftWorkflow ConstructionDraftWorkflow =>
        _constructionDraftWorkflow ?? throw new InvalidOperationException("SaveManager is not ready.");

    public CreationsPresentationViewModel CreationsPresentation =>
        _creationsPresentation ?? throw new InvalidOperationException("SaveManager is not ready.");

    private IConstructionEditWorkflow ConstructionEditWorkflow =>
        _constructionEditWorkflow ?? throw new InvalidOperationException("SaveManager is not ready.");

    public ProgressionDef Progression => ProgressionRepository.Load();

    public bool UnlockExtraCore(int generation)
    {
        var current = ProgressionRepository.Load();
        if (current.ExtraCoreUnlocked)
        {
            return false;
        }

        ProgressionRepository.Save(new ProgressionDef(true, generation));
        return true;
    }

    public CreationDef Duplicate(Guid id, CreationDuplicateMode mode = CreationDuplicateMode.CopyTraining)
    {
        return CreationDuplicateWorkflow.Duplicate(id, mode);
    }

    private ICreationRepository Repository =>
        _repository ?? throw new InvalidOperationException("SaveManager is not ready.");

    private IProgressionRepository ProgressionRepository =>
        _progressionRepository ?? throw new InvalidOperationException("SaveManager is not ready.");

    private ICreationUpdateCoordinator UpdateCoordinator =>
        _updateCoordinator ?? throw new InvalidOperationException("SaveManager is not ready.");

    private ICreationDuplicateWorkflow CreationDuplicateWorkflow =>
        _creationDuplicateWorkflow ?? throw new InvalidOperationException("SaveManager is not ready.");

    private sealed class GodotStorageLocation(string directoryPath) : IStorageLocation
    {
        public string DirectoryPath { get; } = directoryPath;
    }
}
