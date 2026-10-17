using NodeRunner.App.Lifecycle;
using NodeRunner.App.Repositories;
using NodeRunner.App.Services;
using NodeRunner.App.Tests.ViewModels;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;
using NodeRunner.ML.Networks;

namespace NodeRunner.App.Tests.Services;

public sealed class CreationUpdateCoordinatorTests
{
    // A snapshot taken before a reset (#113), an edit that may change the network, or a delete must not
    // be written over what came after, nor may an older or equal generation queued out of order.
    [Theory]
    [InlineData("nothing", true)]
    [InlineData("reset", false)]
    [InlineData("edit", false)]
    [InlineData("delete", false)]
    [InlineData("generation 10", false)]
    [InlineData("generation 7", false)]
    public void TryPersistTraining_WritesOnlyASnapshotNothingHasOvertaken(string since, bool applied)
    {
        var repository = new InMemoryCreationRepository();
        var coordinator = new CreationUpdateCoordinator(repository);
        var creation = CreateCreation("Alpha", withTraining: true);
        repository.Save(creation);
        var epoch = coordinator.CurrentTrainingEpoch(creation.Id);
        switch (since)
        {
            case "reset":
                coordinator.ResetTraining(creation.Id);
                break;
            case "edit":
                coordinator.ApplyEdit(creation.Id, creation.Creature);
                break;
            case "delete":
                coordinator.Delete(creation.Id).ShouldBeTrue();
                break;
            case "generation 10":
                coordinator.TryPersistTraining(creation.Id, epoch, TestTraining.State(10, 1, TestTraining.Run));
                break;
            case "generation 7":
                coordinator.TryPersistTraining(creation.Id, epoch, TestTraining.State(7, 2, TestTraining.Run));
                break;
        }

        var before = repository.Get(creation.Id);

        coordinator.TryPersistTraining(creation.Id, epoch, TestTraining.State(7, 3, TestTraining.Run)).ShouldBe(applied);

        var after = repository.Get(creation.Id);
        if (applied)
        {
            after!.Training!.Generation.ShouldBe(7);
            after.Training.Best.Distance.ShouldBe(3);
        }
        else
        {
            after.ShouldBe(before);
        }
    }

    [Fact]
    public void Updates_KeepTheTrainSettings_AndAMissingCreationIsNullOrKeyNotFound()
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
        coordinator.ApplyEdit(creation.Id, creation.Creature).ShouldNotBeNull().Training.ShouldBeNull();
        var saved = repository.Get(creation.Id);
        coordinator.DeleteAndCapture(creation.Id).ShouldBe(saved);
        repository.Get(creation.Id).ShouldBeNull();
        coordinator.Delete(creation.Id).ShouldBeFalse();
        coordinator.ApplyEdit(creation.Id, creation.Creature).ShouldBeNull();
        coordinator.UpdateIfPresent(creation.Id, source => source).ShouldBeNull();

        // KeyNotFoundException, not InvalidOperationException, so callers treating a missing Creation as
        // recoverable (#114) cannot also swallow a genuine lifecycle bug.
        Should.Throw<KeyNotFoundException>(() => coordinator.ResetTraining(creation.Id));
        repository.List().ShouldBeEmpty();
    }

    // Moving a joint keeps the network; removing a part drops only its ports.
    [Fact]
    public void ApplyEdit_KeepsTheNetworkThroughGeometry_AndDropsOnlyARemovedPartsPorts()
    {
        var repository = new InMemoryCreationRepository();
        var coordinator = new CreationUpdateCoordinator(repository);
        var creation = Trained(PistonCreature(withSecondPiston: true, nodeX: 0));
        repository.Save(creation);
        var moved = PistonCreature(withSecondPiston: true, nodeX: 3);

        var updated = coordinator.ApplyEdit(creation.Id, moved)!;
        var removed = coordinator.ApplyEdit(creation.Id, PistonCreature(withSecondPiston: false, nodeX: 3))!;

        updated.Creature.ShouldBe(moved);
        updated.Training!.Network.Neurons.ShouldBe(creation.Training!.Network.Neurons);
        updated.Training.Network.Connections.ShouldBe(creation.Training.Network.Connections);
        var network = removed.Training!.Network;
        network.Neurons.ShouldAllBe(neuron => neuron.PartId == 10);
        network.Connections.Count.ShouldBe(4);
        network.Connections.ShouldBe(creation.Training.Network.Connections.Where(gene => network.Connections.Any(other => other.From == gene.From && other.To == gene.To)));
        repository.Get(creation.Id).ShouldBe(removed);
    }

    [Fact]
    public void ApplyEdit_WithTheOpenedNetwork_RefitsIt_AndKeepsTheSavedRecords()
    {
        var repository = new InMemoryCreationRepository();
        var coordinator = new CreationUpdateCoordinator(repository);
        var creation = Trained(PistonCreature(withSecondPiston: true, nodeX: 0));
        repository.Save(creation);
        var opened = creation.Training!.Network;
        coordinator.ApplyEdit(creation.Id, PistonCreature(withSecondPiston: false, nodeX: 0));
        var saved = repository.Get(creation.Id)!;
        var training = saved.Training!;
        repository.Save(saved.WithCreature(saved.Creature, new TrainingStateDef(training.Network, 9, training.Latest, training.Best)));

        var updated = coordinator.ApplyEdit(creation.Id, PistonCreature(withSecondPiston: true, nodeX: 0), opened)!;

        updated.Training!.Generation.ShouldBe(9);
        updated.Training.Network.Neurons.ShouldBe(opened.Neurons, ignoreOrder: true);
        updated.Training.Network.Connections.ShouldBe(opened.Connections, ignoreOrder: true);
    }

    [Fact]
    public void ApplyEdit_Rebuild_KeepsMatchedPortsAndTheTrainingRecords_ThroughARestart()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"node-runner-{Guid.NewGuid():N}");
        try
        {
            var repository = new FileCreationRepository(new TestStorageLocation(directory));
            var creation = Trained(PistonCreature(withSecondPiston: false, nodeX: 0));
            repository.Save(creation);
            var oldPorts = NetworkPorts.Of(creation.Creature);
            var oldGenome = DirectNetwork.Compile(creation.Training!.Network, oldPorts);
            var rebuilt = PistonCreature(withSecondPiston: true, nodeX: 0);
            var newPorts = NetworkPorts.Of(rebuilt);

            var updated = new CreationUpdateCoordinator(repository).ApplyEdit(creation.Id, rebuilt)!;

            var training = updated.Training!;
            training.Generation.ShouldBe(creation.Training.Generation);
            training.Latest.ShouldBe(creation.Training.Latest);
            training.Best.ShouldBe(creation.Training.Best);
            var genome = DirectNetwork.Compile(training.Network, newPorts);

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
            var reopened = new FileCreationRepository(new TestStorageLocation(directory)).Get(creation.Id).ShouldNotBeNull();
            reopened.Training!.Network.Neurons.ShouldBe(training.Network.Neurons);
            reopened.Training.Network.Connections.ShouldBe(training.Network.Connections);
            reopened.Training.Generation.ShouldBe(creation.Training.Generation);
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
    public void ApplyEdit_FiveRaysForThree_KeepsTheWeightsOfTheRaysBothHave()
    {
        // #578: rays keep their keys, so port matching (#516) keeps left1, centre and right1.
        var repository = new InMemoryCreationRepository();
        var coordinator = new CreationUpdateCoordinator(repository);
        var creation = Trained(CameraCreature(rays: 3));
        repository.Save(creation);
        var oldPorts = NetworkPorts.Of(creation.Creature);
        var oldGenome = DirectNetwork.Compile(creation.Training!.Network, oldPorts);
        var rebuilt = CameraCreature(rays: 5);
        var newPorts = NetworkPorts.Of(rebuilt);

        var genome = DirectNetwork.Compile(coordinator.ApplyEdit(creation.Id, rebuilt)!.Training!.Network, newPorts);

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
    public void ApplyEdit_AfterLinksAndJointsChangeOnALockedCreation_KeepsEveryWeight()
    {
        var repository = new InMemoryCreationRepository();
        var coordinator = new CreationUpdateCoordinator(repository);
        var creation = Trained(PistonCreature(withSecondPiston: false, nodeX: 0));
        repository.Save(creation);
        var network = creation.Training!.Network;
        var build = new BuildViewModel();
        build.LoadCreation(creation);
        var updates = new List<CreationDef>();
        void Edit(Action edit)
        {
            edit();
            updates.Add(coordinator.ApplyEdit(creation.Id, build.Snapshot(), build.OpenedNetwork)!);
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
            update.Training!.Network.Neurons.ShouldBe(network.Neurons, ignoreOrder: true);
            update.Training.Network.Connections.ShouldBe(network.Connections, ignoreOrder: true);
        }

        updates[^1].Creature.Nodes.ShouldBe(creation.Creature.Nodes);
        updates[^1].Creature.Beams.ShouldBe(creation.Creature.Beams);
        updates[^1].Creature.Springs.ShouldBeEmpty();
        build.IsLocked.ShouldBeTrue();
    }

    // A link change keeps the motor's id, so its network ports keep their weights and biases (#1177);
    // a locked Creation changes a motor's sides without unlocking, and stays locked and trained (#1179).
    [Theory]
    [InlineData(CreatureElementKind.Servo, true)]
    [InlineData(CreatureElementKind.VelocityMotor, false)]
    public void ApplyEdit_AfterAMotorPicksAnotherLink_KeepsEveryWeight(CreatureElementKind kind, bool unlockAndDeleteItsFixedLink)
    {
        var repository = new InMemoryCreationRepository();
        var coordinator = new CreationUpdateCoordinator(repository);
        var creation = Trained(MotorCreature(kind));
        repository.Save(creation);
        var network = creation.Training!.Network;
        network.Neurons.ShouldContain(neuron => neuron.PartId == 9);
        var build = new BuildViewModel();
        build.LoadCreation(creation);
        if (unlockAndDeleteItsFixedLink)
        {
            build.Unlock();
            build.SelectOnly(CreatureElementKind.Beam, 5);
            build.DeleteSelectedParts();
        }

        build.SelectOnly(kind, 9);
        build.SetHingePartLink(9, fixedRole: unlockAndDeleteItsFixedLink, linkId: 7);

        build.IsLocked.ShouldBe(!unlockAndDeleteItsFixedLink);
        var updated = coordinator.ApplyEdit(creation.Id, build.Snapshot(), build.OpenedNetwork)!;
        updated.Creature.JointMotors.Select(motor => (motor.Id, motor.FixedLinkId, motor.TargetLinkId))
            .ShouldBe([unlockAndDeleteItsFixedLink ? (9, 7, 6) : (9, 5, 7)]);
        CreationLock.IsLocked(updated).ShouldBeTrue();
        updated.Training!.Generation.ShouldBe(creation.Training.Generation);
        updated.Training.Network.Neurons.ShouldBe(network.Neurons, ignoreOrder: true);
        updated.Training.Network.Connections.ShouldBe(network.Connections, ignoreOrder: true);
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

    private static CreationDef Trained(CreatureDef creature) =>
        new(Guid.NewGuid(), "Pair", creature, TestTraining.StateFor(creature, 6, bestDistance: 300, new TrainingRunDef(250, 1, 0, MapIds.Flat)));

    // Three nodes; piston 10 joins nodes 1 and 2, the optional piston 11 joins nodes 2 and 3.
    private static CreatureDef PistonCreature(bool withSecondPiston, double nodeX) => new(
        [new NodeDef(1, new Vector2D(nodeX, 0)), new NodeDef(2, new Vector2D(2, 0)), new NodeDef(3, new Vector2D(4, 0))],
        [new BeamDef(4, 1, 3)],
        [],
        withSecondPiston ? [new PistonDef(10, 1, 2), new PistonDef(11, 2, 3)] : [new PistonDef(10, 1, 2)],
        nextPartId: 12);

    // Motor 9 of kind on joint 2 turns beam 6 against beam 5; beam 7 is a spare link at joint 2 and beam 8 keeps joint 1 attached.
    private static CreatureDef MotorCreature(CreatureElementKind kind) => new(
        [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(100, 0)), new NodeDef(3, new Vector2D(200, 0)), new NodeDef(4, new Vector2D(100, 100))],
        [new BeamDef(5, 1, 2), new BeamDef(6, 2, 3), new BeamDef(7, 2, 4), new BeamDef(8, 1, 4)],
        [],
        kind == CreatureElementKind.Servo ? [new ServoDef(9, 2, 5, 6)] : [],
        [],
        [],
        [],
        [],
        [],
        kind == CreatureElementKind.VelocityMotor ? [new VelocityMotorDef(9, 2, 5, 6)] : [],
        [],
        nextPartId: 10);

    // Piston 10 and a Camera with the given rays on beam 4.
    private static CreatureDef CameraCreature(int rays) => new(
        [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(2, 0)), new NodeDef(3, new Vector2D(4, 0))],
        [new BeamDef(4, 1, 3)],
        [new SensorDef(5, 4, SensorKind.Camera, rays: rays)],
        [new PistonDef(10, 1, 2)],
        nextPartId: 12);

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
