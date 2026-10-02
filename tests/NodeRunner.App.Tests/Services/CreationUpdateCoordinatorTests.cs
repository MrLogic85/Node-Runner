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

        var applied = coordinator.TryPersistTraining(creation.Id, epoch, TestTraining.State(3, 1, TestTraining.Run));

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
        var applied = coordinator.TryPersistTraining(creation.Id, epochBeforeReset, TestTraining.State(5, 1, TestTraining.Run));

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

        coordinator.ApplyEdit(creation.Id, creation.Creature, moveOnly: true);
        var applied = coordinator.TryPersistTraining(creation.Id, epochBeforeEdit, TestTraining.State(5, 1, TestTraining.Run));

        applied.ShouldBeFalse();
    }

    [Fact]
    public void ApplyEdit_MoveOnly_KeepsTraining()
    {
        var repository = new InMemoryCreationRepository();
        var coordinator = new CreationUpdateCoordinator(repository);
        var creation = CreateCreation("Alpha", withTraining: true);
        repository.Save(creation);
        var moved = MovedCreature();

        var updated = coordinator.ApplyEdit(creation.Id, moved, moveOnly: true);

        updated.ShouldNotBeNull();
        updated.Creature.ShouldBe(moved);
        updated.Training.ShouldBe(creation.Training);
        repository.Get(creation.Id).ShouldBe(updated);
    }

    [Fact]
    public void ApplyEdit_FullEdit_DropsTraining()
    {
        var repository = new InMemoryCreationRepository();
        var coordinator = new CreationUpdateCoordinator(repository);
        // A generation can finish while Build is open on an unlocked Creation; the full edit still
        // drops it rather than keep a genome sized for the old anatomy.
        var creation = CreateCreation("Alpha", withTraining: true);
        repository.Save(creation);
        var loose = new CreatureDef([new NodeDef(1, new Vector2D(0, 0), 1)], [], []);

        var updated = coordinator.ApplyEdit(creation.Id, loose, moveOnly: false);

        updated.ShouldNotBeNull();
        updated.Creature.ShouldBe(loose);
        updated.Training.ShouldBeNull();
        repository.Get(creation.Id).ShouldBe(updated);
    }

    [Fact]
    public void ApplyEdit_WhenMissing_ReturnsNull()
    {
        var coordinator = new CreationUpdateCoordinator(new InMemoryCreationRepository());

        coordinator.ApplyEdit(Guid.NewGuid(), MovedCreature(), moveOnly: false).ShouldBeNull();
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
        var applied = coordinator.TryPersistTraining(creation.Id, epochBeforeDelete, TestTraining.State(5, 1, TestTraining.Run));

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
        coordinator.TryPersistTraining(creation.Id, epoch, TestTraining.State(10, 1, TestTraining.Run));

        var applied = coordinator.TryPersistTraining(creation.Id, epoch, TestTraining.State(4, 1, TestTraining.Run));

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
        coordinator.TryPersistTraining(creation.Id, epoch, TestTraining.State(7, 2, TestTraining.Run));

        var applied = coordinator.TryPersistTraining(creation.Id, epoch, TestTraining.State(7, 3, TestTraining.Run));

        applied.ShouldBeFalse();
        repository.Get(creation.Id)!.Training!.BestFitness.ShouldBe(2);
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
        [new SensorDef(4, 3, SensorKind.Accelerometer)]);

    private static CreationDef CreateCreation(string name, bool withTraining = false)
    {
        var creature = new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0), 1), new NodeDef(2, new Vector2D(2, 0), 1)],
            [new BeamDef(101, 1, 2)],
            [new SensorDef(201, 101, SensorKind.Accelerometer)]);

        return new CreationDef(
            Guid.NewGuid(),
            name,
            creature,
            withTraining ? TestTraining.State(2, 1, TestTraining.Run) : null);
    }
}
