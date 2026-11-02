using NodeRunner.App.Repositories;
using NodeRunner.App.Services;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;
using static NodeRunner.App.Tests.ViewModels.BuildFixtures;

namespace NodeRunner.App.Tests.ViewModels;

/// <summary>
/// A sensor or Memory cell moves to another beam and keeps what the network learned about it: its id, so its
/// ports and weights, on locked Creations too.
/// </summary>
public sealed class BuildBeamPartMoveTests
{
    [Fact]
    public void OnALockedCreation_AMovedSensor_IsAnUndoStep_AndKeepsItsPortsAndTrainedWeights()
    {
        var repository = new InMemoryCreationRepository();
        var creation = new CreationDef(Guid.NewGuid(), "Walker", Creature(), TestTraining.StateFor(Creature(), 3));
        repository.Save(creation);
        var build = new BuildViewModel();
        build.LoadCreation(creation);
        var before = build.Snapshot();

        build.MoveBeamPart(21, Beam(11)).ShouldBeTrue();
        var after = build.Snapshot();
        after.Sensors.Single(sensor => sensor.Id == 21).BeamId.ShouldBe(11);
        build.IsLocked.ShouldBeTrue();
        build.Undo();
        build.Snapshot().Sensors.ShouldBe(before.Sensors);
        build.Redo();
        build.Snapshot().Sensors.ShouldBe(after.Sensors);

        build.MoveBeamPart(20, Beam(12)).ShouldBeTrue();
        build.SingleSelectedId(CreatureElementKind.Sensor).ShouldBe(20);
        var updated = new CreationUpdateCoordinator(repository).ApplyEdit(creation.Id, build.Snapshot(), build.OpenedNetwork)!;
        updated.Creature.Sensors.Select(sensor => (sensor.Id, sensor.BeamId)).ShouldBe([(20, 12), (21, 11)]);
        NetworkPorts.Of(updated.Creature).Inputs.ShouldBe(NetworkPorts.Of(creation.Creature).Inputs);
        updated.Training!.Network.Neurons.ShouldBe(creation.Training!.Network.Neurons);
        updated.Training.Network.Connections.ShouldBe(creation.Training.Network.Connections);
    }

    [Fact]
    public void OnALockedCreation_AMovedMemoryCell_KeepsItsPortsAndTrainedWeights()
    {
        // Memory cell 32 on beam 11, and a free beam 31 to move it to.
        var withCell = new CreatureDef([.. Creature().Parts, new BeamDef(31, 1, 4), new MemoryCellDef(32, 11, "Count")], nextPartId: 33);
        var repository = new InMemoryCreationRepository();
        var creation = new CreationDef(Guid.NewGuid(), "Walker", withCell, TestTraining.StateFor(withCell, 3));
        repository.Save(creation);
        var build = new BuildViewModel();
        build.LoadCreation(creation);

        build.MoveBeamPart(32, Beam(31)).ShouldBeTrue();
        build.IsLocked.ShouldBeTrue();
        build.SingleSelectedId(CreatureElementKind.MemoryCell).ShouldBe(32);
        var updated = new CreationUpdateCoordinator(repository).ApplyEdit(creation.Id, build.Snapshot(), build.OpenedNetwork)!;
        updated.Creature.MemoryCells.ShouldBe([new MemoryCellDef(32, 31, "Count")]);
        NetworkPorts.Of(updated.Creature).Inputs.ShouldBe(NetworkPorts.Of(creation.Creature).Inputs);
        NetworkPorts.Of(updated.Creature).Outputs.ShouldBe(NetworkPorts.Of(creation.Creature).Outputs);
        updated.Training!.Network.Neurons.ShouldBe(creation.Training!.Network.Neurons);
        updated.Training.Network.Connections.ShouldBe(creation.Training.Network.Connections);
    }

    [Fact]
    public void ADropThatTakesNoMove_LeavesTheSensor_IsNoStep_AndSaysWhyWhereItCan()
    {
        var build = Loaded(Creature());
        var before = build.Snapshot().Sensors;

        build.MoveBeamPart(20, Beam(12)).ShouldBeFalse();
        build.PlacementNote.ShouldBe(new CanvasNote(CanvasNoteKind.Danger, Beam(12), UiText.Plain("One sensor or Memory cell per beam")));
        build.MoveBeamPart(20, Joint(4)).ShouldBeFalse();
        build.PlacementNote.ShouldBe(new CanvasNote(CanvasNoteKind.Danger, Joint(4), UiText.Plain("Cameras go on a beam")));
        build.MoveBeamPart(20, null).ShouldBeFalse();
        build.PlacementNote.ShouldBeNull();
        build.MoveBeamPart(20, Beam(10)).ShouldBeTrue();
        build.PlacementNote.ShouldBeNull();
        build.Snapshot().Sensors.ShouldBe(before);
        build.CanUndo.ShouldBeFalse();

        build.SelectOnly(CreatureElementKind.Sensor, 20);
        build.DeleteSelectedParts();
        build.BeamPartMovePreview(20, Beam(11)).ShouldBeNull();
        build.MoveBeamPart(20, Beam(11)).ShouldBeFalse();
        build.PlacementNote.ShouldBeNull();
    }

    // A Camera on beam 10, an Accelerometer on beam 12 and nothing on beam 11; Piston 30 gives the
    // network an output.
    private static CreatureDef Creature() => new(
        [
            new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(100, 0)), new NodeDef(3, new Vector2D(100, 100)), new NodeDef(4, new Vector2D(200, 100)),
            new BeamDef(10, 1, 2), new BeamDef(11, 2, 3), new BeamDef(12, 3, 4),
            new SensorDef(20, 10, SensorKind.Camera, aim: 0.2), new SensorDef(21, 12, SensorKind.Accelerometer),
            new PistonDef(30, 1, 3),
        ],
        nextPartId: 31);
}
