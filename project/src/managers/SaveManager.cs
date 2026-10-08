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
    private ICreationUpdateCoordinator? _updateCoordinator;
    private INewCreationWorkflow? _newCreationWorkflow;
    private IBuildEditWorkflow? _buildEditWorkflow;
    private ICreationDuplicateWorkflow? _creationDuplicateWorkflow;
    private IExampleCopyWorkflow? _exampleCopyWorkflow;
    private ICreationImportWorkflow? _creationImportWorkflow;
    private DefaultCreationSeeder? _defaultCreationSeeder;
    private CreationsPresentationViewModel? _creationsPresentation;

    public override void _Ready()
    {
        // The save layout is owned by docs/SAVE_FORMAT.md.
        var directory = ProjectSettings.GlobalizePath("user://creations");
        _repository = new FileCreationRepository(new GodotStorageLocation(directory));
        _updateCoordinator = new CreationUpdateCoordinator(_repository);
        _newCreationWorkflow = new NewCreationWorkflow(_repository);
        _buildEditWorkflow = new BuildEditWorkflow(_updateCoordinator);
        var progression = new FileProgressionRepository(new GodotStorageLocation(ProjectSettings.GlobalizePath("user://")));
        _creationDuplicateWorkflow = new CreationDuplicateWorkflow(_repository);
        _exampleCopyWorkflow = new ExampleCopyWorkflow(_repository);
        _creationImportWorkflow = new CreationImportWorkflow(_repository);
        _defaultCreationSeeder = new DefaultCreationSeeder(_exampleCopyWorkflow, progression);
        _creationsPresentation = new CreationsPresentationViewModel(_repository);
    }

    public IReadOnlyList<CreationDef> List()
    {
        return Repository.List();
    }

    public void Save(CreationDef creation)
    {
        Repository.Save(creation);
    }

    // Delete/ResetTraining/ApplyEdit/UpdateIfPresent/Get/
    // CurrentTrainingEpoch/PersistTrainingInBackground all delegate to
    // UpdateCoordinator, which serializes mutations per Creation id,
    // invalidates queued background training snapshots (see #113) that a
    // reset, edit, or delete has superseded, and reads a Creation only once
    // its queued snapshots have landed (#370).
    public bool Delete(Guid id) => UpdateCoordinator.Delete(id);

    public CreationDef? Get(Guid id) => UpdateCoordinator.Get(id);

    public void ResetTraining(Guid id) => UpdateCoordinator.ResetTraining(id);

    public long CurrentTrainingEpoch(Guid id) => UpdateCoordinator.CurrentTrainingEpoch(id);

    public Task PersistTrainingInBackground(Guid id, long expectedEpoch, TrainingStateDef training) =>
        UpdateCoordinator.PersistTrainingInBackground(id, expectedEpoch, training);

    public CreationDef? UpdateIfPresent(Guid id, Func<CreationDef, CreationDef> update) =>
        UpdateCoordinator.UpdateIfPresent(id, update);

    public CreationsPresentationViewModel CreationsPresentation =>
        _creationsPresentation ?? throw new InvalidOperationException("SaveManager is not ready.");

    public IBuildEditWorkflow BuildEditWorkflow =>
        _buildEditWorkflow ?? throw new InvalidOperationException("SaveManager is not ready.");

    // The default names these save are put into the player's language once, as they are saved
    // (#759). The caller passes the translation: managers hold no UI.
    public CreationDef Duplicate(Guid id, Func<UiText, string> inPlayerLanguage) =>
        CreationDuplicateWorkflow.Duplicate(id, inPlayerLanguage);

    public CreationDef CopyExample(Guid exampleId, Func<UiText, string> inPlayerLanguage) =>
        ExampleCopyWorkflow.Copy(exampleId, inPlayerLanguage);

    public CreationDef Import(CreationDef build, string name) =>
        CreationImportWorkflow.Import(build, name);

    public CreationDef CreateNew(Func<UiText, string> inPlayerLanguage) => NewCreationWorkflow.Create(inPlayerLanguage);

    /// <summary>Copies the Walker on the app's first start; see <see cref="DefaultCreationSeeder"/>.</summary>
    public bool SeedDefaultCreations(Func<UiText, string> inPlayerLanguage) =>
        (_defaultCreationSeeder ?? throw new InvalidOperationException("SaveManager is not ready.")).SeedIfNeeded(inPlayerLanguage);

    private ICreationRepository Repository =>
        _repository ?? throw new InvalidOperationException("SaveManager is not ready.");

    private ICreationUpdateCoordinator UpdateCoordinator =>
        _updateCoordinator ?? throw new InvalidOperationException("SaveManager is not ready.");

    private ICreationDuplicateWorkflow CreationDuplicateWorkflow =>
        _creationDuplicateWorkflow ?? throw new InvalidOperationException("SaveManager is not ready.");

    private IExampleCopyWorkflow ExampleCopyWorkflow =>
        _exampleCopyWorkflow ?? throw new InvalidOperationException("SaveManager is not ready.");

    private ICreationImportWorkflow CreationImportWorkflow =>
        _creationImportWorkflow ?? throw new InvalidOperationException("SaveManager is not ready.");

    private INewCreationWorkflow NewCreationWorkflow =>
        _newCreationWorkflow ?? throw new InvalidOperationException("SaveManager is not ready.");

    private sealed class GodotStorageLocation(string directoryPath) : IStorageLocation
    {
        public string DirectoryPath { get; } = directoryPath;
    }
}
