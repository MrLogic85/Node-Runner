using NodeRunner.App.Repositories;
using NodeRunner.App.Services;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.Services;

public sealed class CreationUpdateCoordinatorTests
{
    [Fact]
    public void TryPersistTraining_EpochUnchanged_WritesTraining()
    {
        var repository = new InMemoryCreationRepository();
        var coordinator = new CreationUpdateCoordinator(repository);
        var creation = CreateCreation("Alpha");
        repository.Save(creation);
        var epoch = coordinator.CurrentTrainingEpoch(creation.Id);

        var applied = coordinator.TryPersistTraining(creation.Id, epoch, new TrainingStateDef([2, 1], [0.5, -0.5, 0.1], 3, "Tanh"));

        applied.ShouldBeTrue();
        repository.Get(creation.Id)!.Training!.Generation.ShouldBe(3);
    }

    [Fact]
    public void TryPersistTraining_AfterReset_DiscardsStaleSnapshotInstead_OfResurrectingTraining()
    {
        // Regression guard for #113: a training snapshot captured before a
        // reset (and delivered afterward, e.g. from a background thread)
        // must not resurrect the training the reset just cleared.
        var repository = new InMemoryCreationRepository();
        var coordinator = new CreationUpdateCoordinator(repository);
        var creation = CreateCreation("Alpha", withTraining: true);
        repository.Save(creation);
        var epochBeforeReset = coordinator.CurrentTrainingEpoch(creation.Id);

        coordinator.ResetTraining(creation.Id);
        var applied = coordinator.TryPersistTraining(creation.Id, epochBeforeReset, new TrainingStateDef([2, 1], [0.9, 0.9, 0.9], 5, "Tanh"));

        applied.ShouldBeFalse();
        repository.Get(creation.Id)!.Training.ShouldBeNull();
    }

    [Fact]
    public void TryPersistTraining_AfterCreatureEdit_DiscardsStaleSnapshot()
    {
        // Regression guard: an edit can change brain topology, so a genome
        // captured before the edit must not be written onto the new body.
        var repository = new InMemoryCreationRepository();
        var coordinator = new CreationUpdateCoordinator(repository);
        var creation = CreateCreation("Alpha");
        repository.Save(creation);
        var epochBeforeEdit = coordinator.CurrentTrainingEpoch(creation.Id);

        coordinator.ApplyCreatureEdit(creation.Id, creation.Creature);
        var applied = coordinator.TryPersistTraining(creation.Id, epochBeforeEdit, new TrainingStateDef([2, 1], [0.9, 0.9, 0.9], 5, "Tanh"));

        applied.ShouldBeFalse();
    }

    [Fact]
    public void TryPersistTraining_AfterDelete_DoesNotResurrectCreation()
    {
        var repository = new InMemoryCreationRepository();
        var coordinator = new CreationUpdateCoordinator(repository);
        var creation = CreateCreation("Alpha");
        repository.Save(creation);
        var epochBeforeDelete = coordinator.CurrentTrainingEpoch(creation.Id);

        coordinator.Delete(creation.Id).ShouldBeTrue();
        var applied = coordinator.TryPersistTraining(creation.Id, epochBeforeDelete, new TrainingStateDef([2, 1], [0.9, 0.9, 0.9], 5, "Tanh"));

        applied.ShouldBeFalse();
        repository.Get(creation.Id).ShouldBeNull();
    }

    [Fact]
    public void DeleteAndCapture_ReturnsDeletedCreationAndRemovesIt()
    {
        var repository = new InMemoryCreationRepository();
        var coordinator = new CreationUpdateCoordinator(repository);
        var creation = CreateCreation("Alpha", withTraining: true);
        repository.Save(creation);

        var deleted = coordinator.DeleteAndCapture(creation.Id);

        deleted.ShouldBe(creation);
        repository.Get(creation.Id).ShouldBeNull();
    }

    [Fact]
    public void TryPersistTraining_OlderGeneration_NeverRegressesNewerPersistedGeneration()
    {
        // Two snapshots queued out of order must not let the older one win.
        var repository = new InMemoryCreationRepository();
        var coordinator = new CreationUpdateCoordinator(repository);
        var creation = CreateCreation("Alpha");
        repository.Save(creation);
        var epoch = coordinator.CurrentTrainingEpoch(creation.Id);
        coordinator.TryPersistTraining(creation.Id, epoch, new TrainingStateDef([2, 1], [0.2, 0.2, 0.2], 10, "Tanh"));

        var applied = coordinator.TryPersistTraining(creation.Id, epoch, new TrainingStateDef([2, 1], [0.1, 0.1, 0.1], 4, "Tanh"));

        applied.ShouldBeFalse();
        repository.Get(creation.Id)!.Training!.Generation.ShouldBe(10);
    }

    [Fact]
    public void TryPersistTraining_EqualGeneration_DoesNotRewriteAlreadyPersistedGeneration()
    {
        var repository = new InMemoryCreationRepository();
        var coordinator = new CreationUpdateCoordinator(repository);
        var creation = CreateCreation("Alpha");
        repository.Save(creation);
        var epoch = coordinator.CurrentTrainingEpoch(creation.Id);
        coordinator.TryPersistTraining(creation.Id, epoch, new TrainingStateDef([2, 1], [0.2, 0.2, 0.2], 7, "Tanh"));

        var applied = coordinator.TryPersistTraining(creation.Id, epoch, new TrainingStateDef([2, 1], [0.3, 0.3, 0.3], 7, "Tanh"));

        applied.ShouldBeFalse();
        repository.Get(creation.Id)!.Training!.BestGenome.ShouldBe([0.2, 0.2, 0.2]);
    }

    [Fact]
    public void TryFinishTrainingSession_WithFinishedGeneration_LocksAndSavesLatestTraining()
    {
        var (repository, coordinator, creation) = Saved(withTraining: false);

        var locked = coordinator.TryFinishTrainingSession(creation.Id, coordinator.CurrentTrainingEpoch(creation.Id), Training(generation: 1));

        locked.ShouldBeTrue();
        var saved = repository.Get(creation.Id)!;
        saved.IsLocked.ShouldBeTrue();
        saved.Training!.Generation.ShouldBe(1);
    }

    [Fact]
    public void TryFinishTrainingSession_BeforeFirstGeneration_LeavesCreationUnlocked()
    {
        var (repository, coordinator, creation) = Saved(withTraining: false);

        var locked = coordinator.TryFinishTrainingSession(creation.Id, coordinator.CurrentTrainingEpoch(creation.Id), latest: null);

        locked.ShouldBeFalse();
        repository.Get(creation.Id)!.IsLocked.ShouldBeFalse();
    }

    [Fact]
    public void TryFinishTrainingSession_ResumedWithoutNewGeneration_LocksSavedTraining()
    {
        // A first session that was interrupted (app closed) saved generations but never finished.
        var (repository, coordinator, creation) = Saved(withTraining: true);

        var locked = coordinator.TryFinishTrainingSession(creation.Id, coordinator.CurrentTrainingEpoch(creation.Id), latest: null);

        locked.ShouldBeTrue();
        var saved = repository.Get(creation.Id)!;
        saved.IsLocked.ShouldBeTrue();
        saved.Training.ShouldBe(creation.Training);
    }

    [Fact]
    public void TryFinishTrainingSession_OlderSnapshot_KeepsNewerSavedTraining()
    {
        var (repository, coordinator, creation) = Saved(withTraining: false);
        var epoch = coordinator.CurrentTrainingEpoch(creation.Id);
        coordinator.TryPersistTraining(creation.Id, epoch, Training(generation: 5));

        coordinator.TryFinishTrainingSession(creation.Id, epoch, Training(generation: 4)).ShouldBeTrue();

        repository.Get(creation.Id)!.Training!.Generation.ShouldBe(5);
    }

    [Fact]
    public void TryFinishTrainingSession_AfterReset_DoesNotLock()
    {
        var (repository, coordinator, creation) = Saved(withTraining: true);
        var epochBeforeReset = coordinator.CurrentTrainingEpoch(creation.Id);
        coordinator.ResetTraining(creation.Id);

        coordinator.TryFinishTrainingSession(creation.Id, epochBeforeReset, Training(generation: 3)).ShouldBeFalse();

        var saved = repository.Get(creation.Id)!;
        saved.IsLocked.ShouldBeFalse();
        saved.Training.ShouldBeNull();
    }

    [Fact]
    public void Lock_SurvivesLaterTrainingAndMoveEdits_AndResetClearsIt()
    {
        var (repository, coordinator, creation) = Saved(withTraining: true);
        coordinator.TryFinishTrainingSession(creation.Id, coordinator.CurrentTrainingEpoch(creation.Id), latest: null);

        coordinator.TryPersistTraining(creation.Id, coordinator.CurrentTrainingEpoch(creation.Id), Training(generation: 9));
        coordinator.ApplyCreatureEdit(creation.Id, creation.Creature);
        repository.Get(creation.Id)!.IsLocked.ShouldBeTrue();

        coordinator.ResetTraining(creation.Id);
        repository.Get(creation.Id)!.IsLocked.ShouldBeFalse();
    }

    [Fact]
    public void UpdateIfPresent_MissingCreation_ReturnsNullWithoutWriting()
    {
        var repository = new InMemoryCreationRepository();
        var coordinator = new CreationUpdateCoordinator(repository);

        coordinator.UpdateIfPresent(Guid.NewGuid(), source => source).ShouldBeNull();
    }

    [Fact]
    public void ResetTraining_MissingCreation_Throws()
    {
        var repository = new InMemoryCreationRepository();
        var coordinator = new CreationUpdateCoordinator(repository);

        // KeyNotFoundException (not InvalidOperationException), so callers
        // treating "missing Creation" as a recoverable condition (#114)
        // can't also swallow a genuine InvalidOperationException lifecycle
        // bug elsewhere in the call chain.
        Should.Throw<KeyNotFoundException>(() => coordinator.ResetTraining(Guid.NewGuid()));
    }

    private static (InMemoryCreationRepository Repository, CreationUpdateCoordinator Coordinator, CreationDef Creation) Saved(bool withTraining)
    {
        var repository = new InMemoryCreationRepository();
        var creation = CreateCreation("Alpha", withTraining);
        repository.Save(creation);
        return (repository, new CreationUpdateCoordinator(repository), creation);
    }

    private static TrainingStateDef Training(int generation) => new([2, 1], [0.5, -0.5, 0.1], generation, "Tanh");

    private static CreationDef CreateCreation(string name, bool withTraining = false)
    {
        var creature = new CreatureDef(
            [new NodeDef(new Vector2D(0, 0), 1), new NodeDef(new Vector2D(2, 0), 1)],
            [new BeamDef(0, 1)],
            [new CoreDef(0)]);

        return new CreationDef(
            Guid.NewGuid(),
            name,
            creature,
            withTraining ? new TrainingStateDef([2, 1], [0.1, -0.2, 0.3], 2, "Tanh") : null);
    }
}
