using NodeRunner.App.Repositories;
using NodeRunner.App.Services;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;
using NodeRunner.ML.Brains;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class BuildUndoTests
{
    [Fact]
    public void Undo_RevertsAnEdit_AndRedoAppliesItAgain()
    {
        var build = new BuildViewModel();
        build.PlaceNode(new Vector2D(0, 0));
        build.CanUndo.ShouldBeTrue();
        build.CanRedo.ShouldBeFalse();

        build.Undo();

        build.Nodes.ShouldBeEmpty();
        build.CanUndo.ShouldBeFalse();
        build.CanRedo.ShouldBeTrue();

        build.Redo();

        build.Nodes.Single().Position.ShouldBe(new Vector2D(0, 0));
        build.CanRedo.ShouldBeFalse();
    }

    [Fact]
    public void ANewEdit_AfterAnUndo_ClearsRedo()
    {
        var build = new BuildViewModel();
        build.PlaceNode(new Vector2D(0, 0));
        build.PlaceNode(new Vector2D(100, 0));
        build.Undo();

        build.MoveNode(1, new Vector2D(0, 50));

        build.CanRedo.ShouldBeFalse();
    }

    [Fact]
    public void SelectionAndToolChanges_AreNotSteps()
    {
        var build = TwoJoints();

        build.ReplaceSelection([1, 2]);
        build.ToggleSelected(new CreatureElementSelection(CreatureElementKind.Node, 1));
        build.ClearSelection();
        build.ActiveTool = BuildTool.Select;
        build.Undo();

        build.Nodes.Count.ShouldBe(1);
    }

    [Fact]
    public void UndoingADelete_SelectsTheRestoredPartAgain_AndRedoClearsIt()
    {
        var build = TwoJoints();
        build.ConnectLink(BuildLink.Beam, 1, 2);
        var beam = build.Beams.Single().Id;
        build.SelectOnly(CreatureElementKind.Beam, beam);
        build.DeleteSelectedParts();
        build.SelectedPartCount.ShouldBe(0);

        build.Undo();
        build.SingleSelectedBeamId.ShouldBe(beam);

        build.Redo();
        build.SelectedPartCount.ShouldBe(0);

        build.Undo();
        build.SingleSelectedBeamId.ShouldBe(beam);
    }

    [Fact]
    public void UndoingADeleteOfSeveralParts_SelectsOnlyThoseParts()
    {
        var build = TwoJoints();
        build.ConnectLink(BuildLink.Beam, 1, 2);
        build.ReplaceSelection([1, 2]);
        build.DeleteSelectedParts();
        build.Beams.ShouldBeEmpty();

        build.Undo();

        build.Selection.Nodes.ShouldBe([1, 2], ignoreOrder: true);
        build.SelectedBeamCount.ShouldBe(0);
        build.Beams.Count.ShouldBe(1);
    }

    [Fact]
    public void UndoingAnEditThatIsNotADelete_KeepsTheSelection()
    {
        var build = TwoJoints();
        build.SelectOnly(CreatureElementKind.Node, 1);
        build.PlaceNode(new Vector2D(0, 100));

        build.Undo();

        build.SingleSelectedNodeId.ShouldBe(1);
    }

    [Fact]
    public void ARefusedEdit_AddsNoStep_AndKeepsRedo()
    {
        var build = TwoJoints();
        build.ConnectLink(BuildLink.Beam, 1, 2);
        build.PlaceNode(new Vector2D(0, 100));
        build.Undo();

        build.ConnectLink(BuildLink.Beam, 1, 2).ShouldBeNull();
        build.MoveNode(1, build.Nodes[0].Position);

        build.CanRedo.ShouldBeTrue();
        build.Undo();
        build.Beams.ShouldBeEmpty();
    }

    [Fact]
    public void AnOpenEdit_IsOneStep_AndOnlyIfTheBodyChanged()
    {
        var build = TwoJoints();

        build.BeginEdit(_slider);
        build.MoveNode(1, new Vector2D(0, 20));
        build.MoveNode(1, new Vector2D(0, 40));
        build.EndEdit(_slider);
        build.BeginEdit(_slider);
        build.MoveNode(2, new Vector2D(100, 60));
        build.MoveNode(2, new Vector2D(100, 0));
        build.EndEdit(_slider);
        build.Undo();

        build.Nodes[0].Position.ShouldBe(new Vector2D(0, 0));
        build.Nodes.Count.ShouldBe(2);
    }

    [Fact]
    public void ACancelledEdit_AddsNoStep_AndTheNextEditIsAStepOfItsOwn()
    {
        var build = TwoJoints();
        build.Undo();

        build.BeginEdit(_slider);
        build.MoveNode(1, new Vector2D(0, 40));
        build.MoveNode(1, new Vector2D(0, 0));
        build.CancelEdit(_slider);

        build.CanRedo.ShouldBeTrue();
        build.CanUndo.ShouldBeTrue();
        AnEditIsAStepOfItsOwn(build);
    }

    [Fact]
    public void AnotherSourcesCancel_KeepsTheSliderEdit()
    {
        var build = TwoJoints();
        var gestures = new BuildGestures(build);

        build.BeginEdit(_slider);
        build.MoveNode(1, new Vector2D(0, 20));
        gestures.Press(new Vector2D(300, 300));
        gestures.Press(new Vector2D(350, 300), pointer: 1);
        gestures.Release(new Vector2D(350, 300), pointer: 1);
        gestures.Release(new Vector2D(300, 300));
        build.MoveNode(1, new Vector2D(0, 40));
        build.EndEdit(_slider);
        build.Undo();

        build.Nodes[0].Position.ShouldBe(new Vector2D(0, 0));
        build.Nodes.Count.ShouldBe(2);
    }

    [Fact]
    public void ATapWhileASliderIsHeld_IsAStepOfItsOwn()
    {
        var build = TwoJoints();
        build.ActiveTool = BuildTool.Joint;
        var gestures = new BuildGestures(build);

        build.BeginEdit(_slider);
        build.MoveNode(1, new Vector2D(0, 20));
        gestures.Press(new Vector2D(300, 300));
        gestures.Release(new Vector2D(300, 300));
        build.MoveNode(1, new Vector2D(0, 40));
        build.EndEdit(_slider);

        build.Nodes.Count.ShouldBe(3);
        build.Undo();
        build.Nodes[0].Position.ShouldBe(new Vector2D(0, 20));
        build.Nodes.Count.ShouldBe(3);
        build.Undo();
        build.Nodes.Count.ShouldBe(2);
    }

    [Fact]
    public void UndoAndRedo_DoNothing_WhileADragIsOpen()
    {
        var build = TwoJoints();
        build.PlaceNode(new Vector2D(0, 100));
        build.Undo();
        var gestures = new BuildGestures(build);

        gestures.Press(new Vector2D(0, 0));
        gestures.Drag(new Vector2D(0, 20));
        build.Undo();
        build.Redo();
        gestures.Drag(new Vector2D(0, 40));
        gestures.Release(new Vector2D(0, 40));

        build.Nodes.Count.ShouldBe(2);
        build.Nodes[0].Position.ShouldBe(new Vector2D(0, 40));
        build.Undo();
        build.Nodes[0].Position.ShouldBe(new Vector2D(0, 0));
        build.CanUndo.ShouldBeTrue();
        build.CanRedo.ShouldBeTrue();
    }

    [Fact]
    public void ANodeDrag_IsOneStep()
    {
        var build = TwoJoints();
        var gestures = new BuildGestures(build);

        gestures.Press(new Vector2D(0, 0));
        gestures.Drag(new Vector2D(0, 20));
        gestures.Drag(new Vector2D(0, 40));
        gestures.Release(new Vector2D(0, 40));
        build.Undo();

        build.Nodes[0].Position.ShouldBe(new Vector2D(0, 0));
        build.Nodes.Count.ShouldBe(2);
    }

    [Fact]
    public void ADragCancelledByASecondFinger_AddsNoStep_AndKeepsRedo()
    {
        var build = TwoJoints();
        build.PlaceNode(new Vector2D(0, 100));
        build.Undo();
        var gestures = new BuildGestures(build);

        gestures.Press(new Vector2D(0, 0));
        gestures.Drag(new Vector2D(0, 40));
        gestures.Press(new Vector2D(200, 200), pointer: 1);
        gestures.Release(new Vector2D(200, 200), pointer: 1);
        gestures.Release(new Vector2D(0, 40));

        build.Nodes[0].Position.ShouldBe(new Vector2D(0, 0));
        build.CanRedo.ShouldBeTrue();
        AnEditIsAStepOfItsOwn(build);
    }

    [Fact]
    public void ASelectionMove_IsOneStep_AndRedoKeepsTheSelection()
    {
        var build = TwoJoints();
        build.ActiveTool = BuildTool.Select;
        var gestures = new BuildGestures(build);
        build.ReplaceSelection([1, 2]);
        var move = gestures.SelectionHandles.Single(handle => handle.Handle == SelectionHandle.Move).Position;

        gestures.Press(move);
        gestures.Drag(new Vector2D(move.X, move.Y + 20));
        gestures.Drag(new Vector2D(move.X, move.Y + 40));
        gestures.Release(new Vector2D(move.X, move.Y + 40));
        build.Undo();

        build.Nodes.Select(node => node.Position).ShouldBe([new Vector2D(0, 0), new Vector2D(100, 0)]);
        build.Redo();
        build.Nodes.Select(node => node.Position).ShouldBe([new Vector2D(0, 40), new Vector2D(100, 40)]);
        build.SelectedNodeIds.OrderBy(id => id).ShouldBe([1, 2]);
    }

    [Fact]
    public void ACancelledSelectionMove_AddsNoStep_AndKeepsRedo()
    {
        var build = TwoJoints();
        build.PlaceNode(new Vector2D(0, 100));
        build.Undo();
        build.ActiveTool = BuildTool.Select;
        var gestures = new BuildGestures(build);
        build.ReplaceSelection([1, 2]);
        var move = gestures.SelectionHandles.Single(handle => handle.Handle == SelectionHandle.Move).Position;

        gestures.Press(move);
        gestures.Drag(new Vector2D(move.X, move.Y + 40));
        gestures.Press(new Vector2D(300, 300), pointer: 1);
        gestures.Release(new Vector2D(300, 300), pointer: 1);
        gestures.Release(new Vector2D(move.X, move.Y + 40));

        build.Nodes[0].Position.ShouldBe(new Vector2D(0, 0));
        build.CanRedo.ShouldBeTrue();
        AnEditIsAStepOfItsOwn(build);
    }

    [Fact]
    public void ALockedCamerasAimDrag_IsOneStep()
    {
        var build = new BuildViewModel();
        build.Load(
            new CreatureDef([new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(100, 0))], [new BeamDef(3, 1, 2)], [new SensorDef(4, 3, SensorKind.Camera)]),
            moveOnly: true);
        var gestures = new BuildGestures(build);
        build.SelectOnly(CreatureElementKind.Sensor, 4);
        var aim = build.Sensors[0].Aim;
        var handle = gestures.SelectionHandles.Single().Position;

        gestures.Press(handle);
        gestures.Drag(new Vector2D(150, -5));
        gestures.Drag(new Vector2D(150, -50));
        gestures.Release(new Vector2D(150, -50));
        var turned = build.Sensors[0].Aim;
        build.Undo();

        build.Sensors[0].Aim.ShouldBe(aim);
        build.Redo();
        build.Sensors[0].Aim.ShouldBe(turned);
        build.CanRedo.ShouldBeFalse();
    }

    [Fact]
    public void ARename_IsOneStep()
    {
        var build = TwoJoints();
        var name = build.PartDisplayName(1);

        build.RenamePart(1, "Hip", "Joint 1");
        build.Undo();

        build.PartDisplayName(1).ShouldBe(name);
        build.Redo();
        build.PartDisplayName(1).ShouldBe(UiText.AsWritten("Hip"));
    }

    [Fact]
    public void APartAddedAfterAnUndo_GetsAFreshId()
    {
        var build = TwoJoints();
        build.PlaceNode(new Vector2D(0, 100));
        build.Undo();

        var id = build.PlaceNode(new Vector2D(0, 100));

        id.ShouldBe(4);
    }

    [Fact]
    public void TheHistory_HoldsAtMostOneHundredSteps()
    {
        var build = new BuildViewModel();
        for (var i = 0; i < BuildHistory.MaxSteps + 5; i++)
        {
            build.PlaceNode(new Vector2D(i, 0));
        }

        while (build.CanUndo)
        {
            build.Undo();
        }

        build.Nodes.Count.ShouldBe(5);
    }

    [Fact]
    public void Undo_KeepsTheSelectedPartsThatStillExist()
    {
        var build = TwoJoints();
        build.ReplaceSelection([1, 2]);

        build.Undo();

        build.SelectedNodeIds.ShouldBe([1]);
    }

    [Fact]
    public void Load_ClearsTheHistory()
    {
        var build = TwoJoints();
        build.Undo();

        build.Load(build.Snapshot());

        build.CanUndo.ShouldBeFalse();
        build.CanRedo.ShouldBeFalse();
    }

    [Fact]
    public void ALockedCreationsMoves_AreUndoable()
    {
        var build = new BuildViewModel();
        var creation = Trained(Creature());
        build.LoadCreation(creation);

        build.MoveNode(1, new Vector2D(0, 40));
        build.Undo();

        build.IsMoveOnly.ShouldBeTrue();
        build.Nodes[0].Position.ShouldBe(new Vector2D(0, 0));
    }

    [Fact]
    public void UndoAndRedo_MarkTheBodyUnsaved()
    {
        var (_, build, autosave) = Open(Trained(Creature()));
        build.MoveNode(1, new Vector2D(0, 40));
        autosave.Save();

        build.Undo();

        autosave.HasUnsavedEdits.ShouldBeTrue();
        autosave.Save();
        build.Redo();
        autosave.HasUnsavedEdits.ShouldBeTrue();
    }

    [Theory]
    [InlineData(CreatureElementKind.Sensor, 20)]
    [InlineData(CreatureElementKind.Piston, 30)]
    [InlineData(CreatureElementKind.Node, 3)]
    [InlineData(CreatureElementKind.Node, 1)]
    public void UndoingADelete_GivesThePartsTrainedBrainBack(CreatureElementKind kind, int id)
    {
        var creation = Trained(Creature());
        var opened = creation.Training!.Brain;
        var (repository, build, autosave) = Open(creation);
        build.Unlock();
        build.ToggleSelected(new CreatureElementSelection(kind, id));
        build.DeleteSelectedParts();
        autosave.Save();
        repository.Get(creation.Id)!.Training!.Brain.Neurons.Count.ShouldBeLessThan(opened.Neurons.Count);

        build.Undo();
        autosave.Save();

        var brain = repository.Get(creation.Id)!.Training!.Brain;
        brain.Neurons.ShouldBe(opened.Neurons, ignoreOrder: true);
        brain.Connections.ShouldBe(opened.Connections, ignoreOrder: true);
        var ports = BrainPorts.Of(build.Snapshot());
        DirectBrain.Compile(brain, ports).ShouldBe(DirectBrain.Compile(opened, ports));
    }

    [Fact]
    public void APartAddedThisVisit_StaysPassive_WhenAnEarlierOneIsDeleted()
    {
        var creation = Trained(Creature());
        var (repository, build, autosave) = Open(creation);
        build.Unlock();
        var first = build.PlacePart(BuildPart.Accelerometer, new CreatureElementSelection(CreatureElementKind.Beam, 11))!.Value;
        var second = build.PlacePart(BuildPart.Accelerometer, new CreatureElementSelection(CreatureElementKind.Beam, 12))!.Value;
        build.SelectOnly(CreatureElementKind.Sensor, first);
        build.DeleteSelectedParts();
        autosave.Save();

        var saved = repository.Get(creation.Id)!;
        var ports = BrainPorts.Of(saved.Creature);
        var genome = DirectBrain.Compile(saved.Training!.Brain, ports);
        var inputs = ports.Inputs.Select((port, index) => (port, index)).Where(entry => entry.port.PartId == second).Select(entry => entry.index).ToList();
        inputs.ShouldNotBeEmpty();
        for (var o = 0; o < ports.Outputs.Count; o++)
        {
            foreach (var i in inputs)
            {
                genome[(o * ports.Inputs.Count) + i].ShouldBe(0);
            }
        }
    }

    [Fact]
    public void SavesInOneVisit_NeverReuseANeuronId()
    {
        var creation = Trained(Creature());
        var (repository, build, autosave) = Open(creation);
        build.Unlock();
        BrainDef SavedBrain()
        {
            autosave.Save();
            return repository.Get(creation.Id)!.Training!.Brain;
        }

        var first = build.PlacePart(BuildPart.Accelerometer, new CreatureElementSelection(CreatureElementKind.Beam, 11))!.Value;
        var withFirst = SavedBrain();
        build.MoveNode(1, new Vector2D(0, 10));
        var firstIds = withFirst.Neurons.Where(neuron => neuron.PartId == first).Select(neuron => neuron.Id).ToList();
        SavedBrain().Neurons.Where(neuron => neuron.PartId == first).Select(neuron => neuron.Id).ShouldBe(firstIds);
        build.SelectOnly(CreatureElementKind.Sensor, first);
        build.DeleteSelectedParts();
        var withoutFirst = SavedBrain();
        var second = build.PlacePart(BuildPart.Accelerometer, new CreatureElementSelection(CreatureElementKind.Beam, 12))!.Value;
        var withSecond = SavedBrain();

        withoutFirst.NextNeuronId.ShouldBeGreaterThanOrEqualTo(withFirst.NextNeuronId);
        withSecond.Neurons.Where(neuron => neuron.PartId == second).Select(neuron => neuron.Id).ShouldAllBe(id => !firstIds.Contains(id));
    }

    private static readonly object _slider = new();

    // Redo is gone, and one Undo takes back only the new edit.
    private static void AnEditIsAStepOfItsOwn(BuildViewModel build)
    {
        var count = build.Nodes.Count;
        build.PlaceNode(new Vector2D(-200, -200));
        build.CanRedo.ShouldBeFalse();
        build.Undo();
        build.Nodes.Count.ShouldBe(count);
        build.Undo();
        build.Nodes.Count.ShouldBeLessThan(count);
    }

    private static BuildViewModel TwoJoints()
    {
        var build = new BuildViewModel();
        build.PlaceNode(new Vector2D(0, 0));
        build.PlaceNode(new Vector2D(100, 0));
        return build;
    }

    // Nodes 1-4; beams 10, 11, 12; an accelerometer 20 on beam 10; a Piston 30 between nodes 3 and 4.
    private static CreatureDef Creature() => new(
        [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(100, 0)), new NodeDef(3, new Vector2D(0, 100)), new NodeDef(4, new Vector2D(100, 100))],
        [new BeamDef(10, 1, 2), new BeamDef(11, 1, 3), new BeamDef(12, 2, 4)],
        [new SensorDef(20, 10, SensorKind.Accelerometer)],
        [new PistonDef(30, 3, 4)],
        nextPartId: 31);

    private static CreationDef Trained(CreatureDef creature) =>
        new(Guid.NewGuid(), "Walker", creature, TestTraining.StateFor(creature, 4));

    private static (InMemoryCreationRepository Repository, BuildViewModel Build, BuildAutosave Autosave) Open(CreationDef creation)
    {
        var repository = new InMemoryCreationRepository();
        repository.Save(creation);
        var build = new BuildViewModel();
        build.LoadCreation(creation);
        var autosave = new BuildAutosave(build, new BuildEditWorkflow(new CreationUpdateCoordinator(repository)), creation.Id, openedAsNew: false);
        return (repository, build, autosave);
    }
}
