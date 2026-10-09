using NodeRunner.App.Repositories;
using NodeRunner.App.Services;
using NodeRunner.App.Tests.ViewModels;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;
using NodeRunner.ML.Brains;

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

        coordinator.ApplyEdit(creation.Id, creation.Creature);
        var applied = coordinator.TryPersistTraining(creation.Id, epochBeforeEdit, TestTraining.State(5, 1, TestTraining.Run));

        applied.ShouldBeFalse();
    }

    [Fact]
    public void Updates_KeepTheTrainSettings()
    {
        var repository = new InMemoryCreationRepository();
        var coordinator = new CreationUpdateCoordinator(repository);
        var settings = new TrainSettingsDef(16, 30);
        var creation = CreateCreation("Alpha", withTraining: true).WithTrainSettings(settings);
        repository.Save(creation);

        coordinator.TryPersistTraining(creation.Id, coordinator.CurrentTrainingEpoch(creation.Id), TestTraining.State(3, 1, TestTraining.Run));
        repository.Get(creation.Id)!.TrainSettings.ShouldBe(settings);
        coordinator.ApplyEdit(creation.Id, creation.Creature);
        repository.Get(creation.Id)!.TrainSettings.ShouldBe(settings);
        coordinator.ResetTraining(creation.Id);
        repository.Get(creation.Id)!.TrainSettings.ShouldBe(settings);
    }

    [Fact]
    public void ApplyEdit_GeometryOnly_KeepsTheTrainingUnchanged()
    {
        var repository = new InMemoryCreationRepository();
        var coordinator = new CreationUpdateCoordinator(repository);
        var creation = Trained(PistonCreature(withSecondPiston: false, nodeX: 0));
        repository.Save(creation);
        var moved = PistonCreature(withSecondPiston: false, nodeX: 3);

        var updated = coordinator.ApplyEdit(creation.Id, moved);

        updated.ShouldNotBeNull();
        updated.Creature.ShouldBe(moved);
        updated.Training!.Brain.Neurons.ShouldBe(creation.Training!.Brain.Neurons);
        updated.Training.Brain.Connections.ShouldBe(creation.Training.Brain.Connections);
        repository.Get(creation.Id).ShouldBe(updated);
    }

    [Fact]
    public void ApplyEdit_WithTheOpenedBrain_RefitsIt_AndKeepsTheSavedRecords()
    {
        var repository = new InMemoryCreationRepository();
        var coordinator = new CreationUpdateCoordinator(repository);
        var creation = Trained(PistonCreature(withSecondPiston: true, nodeX: 0));
        repository.Save(creation);
        var opened = creation.Training!.Brain;
        coordinator.ApplyEdit(creation.Id, PistonCreature(withSecondPiston: false, nodeX: 0));
        var saved = repository.Get(creation.Id)!;
        var training = saved.Training!;
        repository.Save(saved.WithCreature(saved.Creature, new TrainingStateDef(training.Brain, 9, training.Latest, training.Best)));

        var updated = coordinator.ApplyEdit(creation.Id, PistonCreature(withSecondPiston: true, nodeX: 0), opened)!;

        updated.Training!.Generation.ShouldBe(9);
        updated.Training.Brain.Neurons.ShouldBe(opened.Neurons, ignoreOrder: true);
        updated.Training.Brain.Connections.ShouldBe(opened.Connections, ignoreOrder: true);
    }

    [Fact]
    public void ApplyEdit_AfterLinksAndJointsChangeOnALockedCreation_KeepsEveryWeight()
    {
        var repository = new InMemoryCreationRepository();
        var coordinator = new CreationUpdateCoordinator(repository);
        var creation = Trained(PistonCreature(withSecondPiston: false, nodeX: 0));
        repository.Save(creation);
        var brain = creation.Training!.Brain;
        var build = new BuildViewModel();
        build.LoadCreation(creation);
        var updates = new List<CreationDef>();
        void Edit(Action edit)
        {
            edit();
            updates.Add(coordinator.ApplyEdit(creation.Id, build.Snapshot(), build.OpenedBrain)!);
        }

        var joint = 0;
        Edit(() => joint = build.PlaceNode(new Vector2D(2, 3)));
        var beam = 0;
        Edit(() => beam = build.ConnectLink(BuildLink.Beam, joint, 3)!.Value);
        var spring = 0;
        Edit(() => spring = build.ConnectLink(BuildLink.Spring, joint, 2)!.Value);
        Edit(() =>
        {
            build.SelectOnly(CreatureElementKind.Beam, beam);
            build.DeleteSelectedParts();
        });
        Edit(() =>
        {
            build.SelectOnly(CreatureElementKind.Spring, spring);
            build.DeleteSelectedParts();
        });
        Edit(() =>
        {
            build.SelectOnly(CreatureElementKind.Node, joint);
            build.DeleteSelectedParts();
        });

        updates.Count.ShouldBe(6);
        updates.ShouldAllBe(update => update.Training!.Generation == creation.Training.Generation);
        foreach (var update in updates)
        {
            update.Training!.Brain.Neurons.ShouldBe(brain.Neurons, ignoreOrder: true);
            update.Training.Brain.Connections.ShouldBe(brain.Connections, ignoreOrder: true);
        }

        updates[^1].Creature.Nodes.ShouldBe(creation.Creature.Nodes);
        updates[^1].Creature.Beams.ShouldBe(creation.Creature.Beams);
        updates[^1].Creature.Springs.ShouldBeEmpty();
        build.IsLocked.ShouldBeTrue();
    }

    [Fact]
    public void ApplyEdit_Rebuild_KeepsMatchedPorts_AndTheTrainingRecords()
    {
        var repository = new InMemoryCreationRepository();
        var coordinator = new CreationUpdateCoordinator(repository);
        var creation = Trained(PistonCreature(withSecondPiston: false, nodeX: 0));
        repository.Save(creation);
        var oldPorts = BrainPorts.Of(creation.Creature);
        var oldGenome = DirectBrain.Compile(creation.Training!.Brain, oldPorts);
        var rebuilt = PistonCreature(withSecondPiston: true, nodeX: 0);
        var newPorts = BrainPorts.Of(rebuilt);

        var updated = coordinator.ApplyEdit(creation.Id, rebuilt)!;

        var training = updated.Training!;
        training.Generation.ShouldBe(creation.Training.Generation);
        training.Latest.ShouldBe(creation.Training.Latest);
        training.Best.ShouldBe(creation.Training.Best);
        var genome = DirectBrain.Compile(training.Brain, newPorts);
        // The first piston's outputs keep their trained weights from its own two inputs and their biases.
        var biasStart = newPorts.Inputs.Count * newPorts.Outputs.Count;
        var oldBiasStart = oldPorts.Inputs.Count * oldPorts.Outputs.Count;
        for (var o = 0; o < oldPorts.Outputs.Count; o++)
        {
            genome[o * newPorts.Inputs.Count].ShouldBe(oldGenome[o * oldPorts.Inputs.Count]);
            genome[(o * newPorts.Inputs.Count) + 1].ShouldBe(oldGenome[(o * oldPorts.Inputs.Count) + 1]);
            genome[biasStart + o].ShouldBe(oldGenome[oldBiasStart + o]);
        }

        // The new piston starts almost passive.
        genome[biasStart + 3].ShouldBe(PortSignals.PassiveStrengthBias);
    }

    [Fact]
    public void ApplyEdit_FiveRaysForThree_KeepsTheWeightsOfTheRaysBothHave()
    {
        // #578: rays keep their keys, so port matching (#516) keeps left1, centre and right1.
        var repository = new InMemoryCreationRepository();
        var coordinator = new CreationUpdateCoordinator(repository);
        var creation = Trained(CameraCreature(rays: 3));
        repository.Save(creation);
        var oldPorts = BrainPorts.Of(creation.Creature);
        var oldGenome = DirectBrain.Compile(creation.Training!.Brain, oldPorts);
        var rebuilt = CameraCreature(rays: 5);
        var newPorts = BrainPorts.Of(rebuilt);

        var genome = DirectBrain.Compile(coordinator.ApplyEdit(creation.Id, rebuilt)!.Training!.Brain, newPorts);

        newPorts.Inputs.Count.ShouldBe(oldPorts.Inputs.Count + 2);
        foreach (var channel in new[] { "left1", "centre", "right1", "hit" })
        {
            var oldInput = oldPorts.Inputs.ToList().FindIndex(port => port.Channel == channel);
            var newInput = newPorts.Inputs.ToList().FindIndex(port => port.Channel == channel);
            for (var o = 0; o < oldPorts.Outputs.Count; o++)
            {
                genome[(o * newPorts.Inputs.Count) + newInput].ShouldBe(oldGenome[(o * oldPorts.Inputs.Count) + oldInput], channel);
            }
        }
    }

    [Fact]
    public void ApplyEdit_Rebuild_SurvivesARestart()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"node-runner-{Guid.NewGuid():N}");
        try
        {
            var repository = new FileCreationRepository(new TestStorageLocation(directory));
            var creation = Trained(PistonCreature(withSecondPiston: false, nodeX: 0));
            repository.Save(creation);

            var updated = new CreationUpdateCoordinator(repository).ApplyEdit(creation.Id, PistonCreature(withSecondPiston: true, nodeX: 0))!;

            var reopened = new FileCreationRepository(new TestStorageLocation(directory)).Get(creation.Id).ShouldNotBeNull();
            reopened.Training!.Brain.Neurons.ShouldBe(updated.Training!.Brain.Neurons);
            reopened.Training.Brain.Connections.ShouldBe(updated.Training.Brain.Connections);
            reopened.Training.Generation.ShouldBe(creation.Training!.Generation);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void ApplyEdit_RemovingAPart_DropsOnlyItsPorts()
    {
        var repository = new InMemoryCreationRepository();
        var coordinator = new CreationUpdateCoordinator(repository);
        var creation = Trained(PistonCreature(withSecondPiston: true, nodeX: 0));
        repository.Save(creation);

        var updated = coordinator.ApplyEdit(creation.Id, PistonCreature(withSecondPiston: false, nodeX: 0))!;

        var brain = updated.Training!.Brain;
        brain.Neurons.ShouldAllBe(neuron => neuron.PartId == 10);
        brain.Connections.Count.ShouldBe(4);
        var kept = creation.Training!.Brain.Connections.Where(gene => brain.Connections.Any(other => other.From == gene.From && other.To == gene.To));
        brain.Connections.ShouldBe(kept);
    }

    [Fact]
    public void ApplyEdit_WhenMissing_ReturnsNull()
    {
        var coordinator = new CreationUpdateCoordinator(new InMemoryCreationRepository());

        coordinator.ApplyEdit(Guid.NewGuid(), MovedCreature()).ShouldBeNull();
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
        repository.Get(creation.Id)!.Training!.Best.Distance.ShouldBe(2);
    }

    [Fact]
    public async Task Get_WaitsForQueuedTraining_SoTheLockIsNeverReadEarly()
    {
        var repository = new GatedRepository();
        var coordinator = new CreationUpdateCoordinator(repository);
        var creation = CreateCreation("Alpha");
        repository.Save(creation);
        repository.Gate.Reset();

        var queued = coordinator.PersistTrainingInBackground(creation.Id, coordinator.CurrentTrainingEpoch(creation.Id), TestTraining.State(3));
        var read = Task.Run(() => coordinator.Get(creation.Id));

        // "Still blocked" needs a timeout: with the gate shut a correct Get can never finish, so
        // this can only miss a regression on a very slow machine, never fail falsely.
        (await Task.WhenAny(read, Task.Delay(50))).ShouldNotBe(read);
        repository.Gate.Set();
        (await read).ShouldNotBeNull().Training.ShouldNotBeNull().Generation.ShouldBe(3);
        queued.IsCompletedSuccessfully.ShouldBeTrue();
    }

    [Fact]
    public void Get_AfterAFailedQueuedWrite_ReadsWhatIsSaved()
    {
        var repository = new GatedRepository();
        var coordinator = new CreationUpdateCoordinator(repository);
        var creation = CreateCreation("Alpha");
        repository.Save(creation);
        repository.FailSaves = true;

        var queued = coordinator.PersistTrainingInBackground(creation.Id, coordinator.CurrentTrainingEpoch(creation.Id), TestTraining.State(3));

        coordinator.Get(creation.Id).ShouldNotBeNull().Training.ShouldBeNull();
        queued.IsFaulted.ShouldBeTrue();
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

    private static CreationDef Trained(CreatureDef creature) =>
        new(Guid.NewGuid(), "Pair", creature, TestTraining.StateFor(creature, 6, bestDistance: 300, new TrainingRunDef(250, 1, 0, MapIds.Flat)));

    // Three nodes; piston 10 joins nodes 1 and 2, the optional piston 11 joins nodes 2 and 3.
    private static CreatureDef PistonCreature(bool withSecondPiston, double nodeX) => new(
        [new NodeDef(1, new Vector2D(nodeX, 0)), new NodeDef(2, new Vector2D(2, 0)), new NodeDef(3, new Vector2D(4, 0))],
        [new BeamDef(4, 1, 3)],
        [],
        withSecondPiston ? [new PistonDef(10, 1, 2), new PistonDef(11, 2, 3)] : [new PistonDef(10, 1, 2)],
        nextPartId: 12);

    // Piston 10 and a Camera with the given rays on beam 4.
    private static CreatureDef CameraCreature(int rays) => new(
        [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(2, 0)), new NodeDef(3, new Vector2D(4, 0))],
        [new BeamDef(4, 1, 3)],
        [new SensorDef(5, 4, SensorKind.Camera, rays: rays)],
        [new PistonDef(10, 1, 2)],
        nextPartId: 12);

    private static CreatureDef MovedCreature() => new(
        [new NodeDef(1, new Vector2D(5, 0)), new NodeDef(2, new Vector2D(7, 0))],
        [new BeamDef(3, 1, 2)],
        [new SensorDef(4, 3, SensorKind.Accelerometer)]);

    private static CreationDef CreateCreation(string name, bool withTraining = false)
    {
        var creature = new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(2, 0))],
            [new BeamDef(101, 1, 2)],
            [new SensorDef(201, 101, SensorKind.Accelerometer)]);

        return new CreationDef(
            Guid.NewGuid(),
            name,
            creature,
            withTraining ? TestTraining.State(2, 1, TestTraining.Run) : null);
    }

    /// <summary>Holds every save until <see cref="Gate"/> opens, or fails it.</summary>
    private sealed class GatedRepository : ICreationRepository
    {
        private readonly InMemoryCreationRepository _inner = new();

        public ManualResetEventSlim Gate { get; } = new(initialState: true);

        public bool FailSaves { get; set; }

        public IReadOnlyList<CreationDef> List() => _inner.List();

        public CreationDef? Get(Guid id) => _inner.Get(id);

        public void Save(CreationDef creation)
        {
            Gate.Wait();
            if (FailSaves)
            {
                throw new IOException("disk full");
            }

            _inner.Save(creation);
        }

        public bool Delete(Guid id) => _inner.Delete(id);
    }
}
