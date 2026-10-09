using NodeRunner.App.Repositories;
using NodeRunner.App.Services;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.ViewModels;

/// <summary>
/// A sensor moves to another beam and keeps what the brain learned about it (#806): its id, so its
/// ports and weights, on locked Creations too.
/// </summary>
public sealed class BuildSensorMoveTests
{
    private static readonly CreatureElementSelection _freeBeam = new(CreatureElementKind.Beam, 11);

    [Fact]
    public void OnALockedCreation_ASensorMoves_AndUndoAndRedoCoverIt()
    {
        var build = new BuildViewModel();
        build.LoadCreation(Trained());
        var before = build.Snapshot();

        build.MoveSensor(21, _freeBeam).ShouldBeTrue();
        var after = build.Snapshot();

        after.Sensors.Single(sensor => sensor.Id == 21).BeamId.ShouldBe(11);
        build.IsLocked.ShouldBeTrue();
        build.Undo();
        build.Snapshot().Sensors.ShouldBe(before.Sensors);
        build.Redo();
        build.Snapshot().Sensors.ShouldBe(after.Sensors);
    }

    [Fact]
    public void AMovedSensor_KeepsItsPortsAndTrainedWeights()
    {
        var repository = new InMemoryCreationRepository();
        var coordinator = new CreationUpdateCoordinator(repository);
        var creation = Trained();
        repository.Save(creation);
        var build = new BuildViewModel();
        build.LoadCreation(creation);

        build.MoveSensor(20, _freeBeam).ShouldBeTrue();
        build.MoveSensor(21, new CreatureElementSelection(CreatureElementKind.Beam, 10)).ShouldBeTrue();
        var updated = coordinator.ApplyEdit(creation.Id, build.Snapshot(), build.OpenedBrain)!;

        updated.Creature.Sensors.Select(sensor => (sensor.Id, sensor.BeamId)).ShouldBe([(20, 11), (21, 10)]);

        BrainPorts.Of(updated.Creature).Inputs.ShouldBe(BrainPorts.Of(creation.Creature).Inputs);
        updated.Training!.Brain.Neurons.ShouldBe(creation.Training!.Brain.Neurons);
        updated.Training.Brain.Connections.ShouldBe(creation.Training.Brain.Connections);
    }

    [Fact]
    public void ADropOnABeamThatHasASensor_LeavesItAndSaysWhyThere()
    {
        var build = Unlocked();
        var before = build.Snapshot();
        var occupied = new CreatureElementSelection(CreatureElementKind.Beam, 12);

        build.MoveSensor(20, occupied).ShouldBeFalse();

        build.Snapshot().Sensors.ShouldBe(before.Sensors);
        build.PlacementNote.ShouldBe(new CanvasNote(CanvasNoteKind.Danger, occupied, UiText.Plain("One sensor per beam")));
        build.CanUndo.ShouldBeFalse();
    }

    [Fact]
    public void ADropOnEmptyCanvas_LeavesItAndSaysNothing()
    {
        var build = Unlocked();
        var before = build.Snapshot();

        build.MoveSensor(20, null).ShouldBeFalse();

        build.Snapshot().Sensors.ShouldBe(before.Sensors);
        build.PlacementNote.ShouldBeNull();
        build.CanUndo.ShouldBeFalse();
    }

    [Fact]
    public void ADropOnAJoint_LeavesItAndSaysWhyThere()
    {
        var build = Unlocked();
        var joint = new CreatureElementSelection(CreatureElementKind.Node, 4);

        build.MoveSensor(20, joint).ShouldBeFalse();

        build.PlacementNote.ShouldBe(new CanvasNote(CanvasNoteKind.Danger, joint, UiText.Plain("Cameras go on a beam")));
    }

    [Fact]
    public void ADropOnItsOwnBeam_IsNoStep()
    {
        var build = Unlocked();

        build.MoveSensor(20, new CreatureElementSelection(CreatureElementKind.Beam, 10)).ShouldBeTrue();

        build.CanUndo.ShouldBeFalse();
        build.PlacementNote.ShouldBeNull();
    }

    [Fact]
    public void AGoodDrop_SelectsTheSensor()
    {
        var build = Unlocked();

        build.MoveSensor(20, _freeBeam).ShouldBeTrue();

        build.SingleSelectedSensorId.ShouldBe(20);
    }

    [Fact]
    public void ASensorGoneBeforeItsDrop_MovesNowhereAndSaysNothing()
    {
        var build = Unlocked();
        build.ReplaceSelection(PartSet.None with { Sensors = new HashSet<int> { 20 } });
        build.DeleteSelectedParts();

        build.SensorMovePreview(20, _freeBeam).ShouldBeNull();
        build.MoveSensor(20, _freeBeam).ShouldBeFalse();
        build.PlacementNote.ShouldBeNull();
    }

    private static BuildViewModel Unlocked()
    {
        var build = new BuildViewModel();
        build.Load(Creature());
        return build;
    }

    private static CreationDef Trained() =>
        new(Guid.NewGuid(), "Walker", Creature(), TestTraining.StateFor(Creature(), 3));

    // A Camera on beam 10, an Accelerometer on beam 12 and nothing on beam 11; Piston 30 gives the
    // brain an output.
    private static CreatureDef Creature() => new(
        [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(100, 0)), new NodeDef(3, new Vector2D(100, 100)), new NodeDef(4, new Vector2D(200, 100))],
        [new BeamDef(10, 1, 2), new BeamDef(11, 2, 3), new BeamDef(12, 3, 4)],
        [new SensorDef(20, 10, SensorKind.Camera, aim: 0.2), new SensorDef(21, 12, SensorKind.Accelerometer)],
        [],
        [new PistonDef(30, 1, 3)],
        [],
        nextPartId: 31);
}
