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

        coordinator.ApplyEdit(creation.Id, creation.Creature, creation.BrainShape, moveOnly: true);
        var applied = coordinator.TryPersistTraining(creation.Id, epochBeforeEdit, new TrainingStateDef([2, 1], [0.9, 0.9, 0.9], 5, "Tanh"));

        applied.ShouldBeFalse();
    }

    [Fact]
    public void ApplyEdit_MoveOnly_KeepsBrainShapeAndTraining()
    {
        var repository = new InMemoryCreationRepository();
        var coordinator = new CreationUpdateCoordinator(repository);
        var creation = CreateCreation("Alpha", withTraining: true);
        repository.Save(creation);
        var moved = MovedCreature();

        var updated = coordinator.ApplyEdit(creation.Id, moved, new BrainShapeDef(3, 5), moveOnly: true);

        updated.ShouldNotBeNull();
        updated.Creature.ShouldBe(moved);
        updated.BrainShape.ShouldBe(creation.BrainShape);
        updated.Training.ShouldBe(creation.Training);
        repository.Get(creation.Id).ShouldBe(updated);
    }

    [Fact]
    public void ApplyEdit_FullEdit_TakesBrainShapeAndDropsTraining()
    {
        var repository = new InMemoryCreationRepository();
        var coordinator = new CreationUpdateCoordinator(repository);
        // A generation can finish while Build is open on an unlocked Creation; the full edit still
        // drops it rather than keep a genome sized for the old anatomy.
        var creation = CreateCreation("Alpha", withTraining: true);
        repository.Save(creation);
        var loose = new CreatureDef([new NodeDef(1, new Vector2D(0, 0), 1)], [], []);
        var shape = new BrainShapeDef(3, 5);

        var updated = coordinator.ApplyEdit(creation.Id, loose, shape, moveOnly: false);

        updated.ShouldNotBeNull();
        updated.Creature.ShouldBe(loose);
        updated.BrainShape.ShouldBe(shape);
        updated.Training.ShouldBeNull();
        repository.Get(creation.Id).ShouldBe(updated);
    }

    [Fact]
    public void ApplyEdit_WhenMissing_ReturnsNull()
    {
        var coordinator = new CreationUpdateCoordinator(new InMemoryCreationRepository());

        coordinator.ApplyEdit(Guid.NewGuid(), MovedCreature(), BrainShapeDef.Default, moveOnly: false).ShouldBeNull();
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

    private static CreatureDef MovedCreature() => new(
        [new NodeDef(1, new Vector2D(5, 0), 1), new NodeDef(2, new Vector2D(7, 0), 1)],
        [new BeamDef(3, 1, 2)],
        [new CoreDef(4, 1)]);

    private static CreationDef CreateCreation(string name, bool withTraining = false)
    {
        var creature = new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0), 1), new NodeDef(2, new Vector2D(2, 0), 1)],
            [new BeamDef(101, 1, 2)],
            [new CoreDef(201, 1)]);

        return new CreationDef(
            Guid.NewGuid(),
            name,
            creature,
            withTraining ? new TrainingStateDef([2, 1], [0.1, -0.2, 0.3], 2, "Tanh") : null);
    }
}
