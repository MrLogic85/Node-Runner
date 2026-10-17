using NodeRunner.App.Repositories;
using NodeRunner.App.Services;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;
using NodeRunner.ML.Networks;
using static NodeRunner.App.Tests.ViewModels.BuildFixtures;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class BuildUndoTests
{
    private static readonly object _slider = new();

    [Fact]
    public void EachEdit_IsAStep_ThatUndoRevertsAndRedoApplies_UntilANewEditOrALoad()
    {
        var build = new BuildViewModel();
        build.PlaceNode(new Vector2D(0, 0));
        (build.CanUndo, build.CanRedo).ShouldBe((true, false));
        build.Undo();
        build.Nodes.ShouldBeEmpty();
        (build.CanUndo, build.CanRedo).ShouldBe((false, true));
        build.Redo();
        build.Nodes.Single().Position.ShouldBe(new Vector2D(0, 0));
        build.CanRedo.ShouldBeFalse();

        build.PlaceNode(new Vector2D(100, 0));
        build.ConnectLink(BuildLink.Beam, 1, 2);
        build.PlaceNode(new Vector2D(0, 100));
        build.Undo();
        build.ConnectLink(BuildLink.Beam, 1, 2).ShouldBeNull();
        build.MoveNode(1, build.Nodes[0].Position);
        build.ReplaceSelection([1, 2]);
        build.ToggleSelected(Joint(1));
        build.ActiveTool = BuildTool.Select;
        build.CanRedo.ShouldBeTrue();
        build.PlaceNode(new Vector2D(0, 100)).ShouldBe(5);
        build.CanRedo.ShouldBeFalse();
        build.Undo();

        var name = build.PartDisplayName(1);
        build.RenamePart(1, "Hip", "Joint 1");
        build.Undo();
        build.PartDisplayName(1).ShouldBe(name);
        build.Redo();
        build.PartDisplayName(1).ShouldBe(UiText.AsWritten("Hip"));

        build.ReplaceSelection([1, 2]);
        build.PlaceNode(new Vector2D(0, 100));
        build.Undo();
        build.SelectedNodeIds.ShouldBe([1, 2], ignoreOrder: true);
        build.Undo();
        build.Undo();
        build.Beams.ShouldBeEmpty();
        build.Undo();
        build.SelectedNodeIds.ShouldBe([1]);

        build.Load(build.Snapshot());
        (build.CanUndo, build.CanRedo).ShouldBe((false, false));
        for (var i = 0; i < BuildHistory.MaxSteps + 5; i++)
        {
            build.PlaceNode(new Vector2D(i, 50));
        }

        while (build.CanUndo)
        {
            build.Undo();
        }

        build.Nodes.Count.ShouldBe(6);
    }

    [Fact]
    public void UndoingADelete_SelectsOnlyTheDeletedParts_AndRedoClearsThem()
    {
        var build = TwoJoints();
        var beam = build.ConnectLink(BuildLink.Beam, 1, 2)!.Value;
        build.SelectOnly(CreatureElementKind.Beam, beam);
        build.DeleteSelectedParts();
        build.SelectedPartCount.ShouldBe(0);
        build.Undo();
        build.SingleSelectedBeamId.ShouldBe(beam);
        build.Redo();
        build.SelectedPartCount.ShouldBe(0);
        build.Undo();

        build.ReplaceSelection([1, 2]);
        build.DeleteSelectedParts();
        build.Beams.ShouldBeEmpty();
        build.Undo();
        build.Selection.Parts.ShouldBe([Joint(1), Joint(2)], ignoreOrder: true);
        build.Beams.Count.ShouldBe(1);
    }

    [Fact]
    public void AnOpenEdit_IsOneStep_OnlyIfTheBodyChanged_AndATapMeanwhileIsAStepOfItsOwn()
    {
        var build = TwoJoints();
        build.ActiveTool = BuildTool.Joint;
        var gestures = new BuildGestures(build);

        build.BeginEdit(_slider);
        build.MoveNode(1, new Vector2D(0, 20));
        gestures.Tap(new Vector2D(300, 300));
        gestures.Press(new Vector2D(500, 300));
        gestures.Press(new Vector2D(550, 300), pointer: 1);
        gestures.Release(new Vector2D(550, 300), pointer: 1);
        gestures.Release(new Vector2D(500, 300));
        build.MoveNode(1, new Vector2D(0, 40));
        build.EndEdit(_slider);
        build.BeginEdit(_slider);
        build.MoveNode(2, new Vector2D(100, 60));
        build.MoveNode(2, new Vector2D(100, 0));
        build.EndEdit(_slider);

        build.Nodes.Count.ShouldBe(3);
        build.Undo();
        build.Nodes[0].Position.ShouldBe(new Vector2D(0, 20));
        build.Nodes.Count.ShouldBe(3);
        build.Undo();
        build.Nodes.Count.ShouldBe(2);
        build.Undo();
        build.Nodes[0].Position.ShouldBe(new Vector2D(0, 0));

        build.BeginEdit(_slider);
        build.MoveNode(1, new Vector2D(0, 40));
        build.MoveNode(1, new Vector2D(0, 0));
        build.CancelEdit(_slider);
        (build.CanUndo, build.CanRedo).ShouldBe((true, true));
        AnEditIsAStepOfItsOwn(build);
    }

    [Fact]
    public void ADrag_IsOneStep_UndoWaitsForIt_AndASecondFingerCancelsItWithoutAStep()
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
        (build.CanUndo, build.CanRedo).ShouldBe((true, true));

        gestures.Press(new Vector2D(0, 0));
        gestures.Drag(new Vector2D(0, 40));
        gestures.Press(new Vector2D(200, 200), pointer: 1);
        gestures.Release(new Vector2D(200, 200), pointer: 1);
        gestures.Release(new Vector2D(0, 40));
        build.Nodes[0].Position.ShouldBe(new Vector2D(0, 0));
        build.CanRedo.ShouldBeTrue();

        build.ActiveTool = BuildTool.Select;
        build.ReplaceSelection([1, 2]);
        var move = gestures.SelectionHandles.Single(handle => handle.Handle == SelectionHandle.Move).Position;
        gestures.Press(move);
        gestures.Drag(new Vector2D(move.X, move.Y + 40));
        gestures.Press(new Vector2D(300, 300), pointer: 1);
        gestures.Release(new Vector2D(300, 300), pointer: 1);
        gestures.Release(new Vector2D(move.X, move.Y + 40));
        build.Nodes[0].Position.ShouldBe(new Vector2D(0, 0));
        build.CanRedo.ShouldBeTrue();

        gestures.DragAcross(move, new Vector2D(move.X, move.Y + 40));
        build.CanRedo.ShouldBeFalse();
        build.Undo();
        build.Nodes.Select(node => node.Position).ShouldBe([new Vector2D(0, 0), new Vector2D(100, 0)]);
        build.Redo();
        build.Nodes.Select(node => node.Position).ShouldBe([new Vector2D(0, 40), new Vector2D(100, 40)]);
        build.SelectedNodeIds.ShouldBe([1, 2], ignoreOrder: true);
    }

    [Fact]
    public void OnALockedCreation_MovesAndACamerasAimDrag_AreSteps_ThatMarkTheBodyUnsaved()
    {
        var creature = new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(100, 0)), new BeamDef(3, 1, 2), new SensorDef(4, 3, SensorKind.Camera), new PistonDef(5, 1, 2)],
            nextPartId: 6);
        var (_, build, autosave) = Open(Trained(creature));
        var gestures = new BuildGestures(build);
        build.SelectOnly(CreatureElementKind.Sensor, 4);
        var aim = build.Sensors[0].Aim;
        gestures.Press(gestures.SelectionHandles.Single().Position);
        gestures.Drag(new Vector2D(150, -5));
        gestures.Drag(new Vector2D(150, -50));
        gestures.Release(new Vector2D(150, -50));
        var turned = build.Sensors[0].Aim;
        build.Undo();
        build.Sensors[0].Aim.ShouldBe(aim);
        build.Redo();
        build.Sensors[0].Aim.ShouldBe(turned);
        build.CanRedo.ShouldBeFalse();

        build.MoveNode(1, new Vector2D(0, 40));
        autosave.Save();
        build.Undo();
        build.Nodes[0].Position.ShouldBe(new Vector2D(0, 0));
        build.IsLocked.ShouldBeTrue();
        autosave.HasUnsavedEdits.ShouldBeTrue();
        autosave.Save();
        build.Redo();
        autosave.HasUnsavedEdits.ShouldBeTrue();
    }

    [Theory]
    [InlineData(CreatureElementKind.Node, 1)]
    [InlineData(CreatureElementKind.Node, 3)]
    public void UndoingADelete_GivesThePartsTrainedNetworkBack(CreatureElementKind kind, int id)
    {
        var creation = Trained(Creature());
        var opened = creation.Training!.Network;
        var (repository, build, autosave) = Open(creation);
        build.Unlock();
        build.ToggleSelected(new CreatureElementSelection(kind, id));
        build.DeleteSelectedParts();
        autosave.Save();
        repository.Get(creation.Id)!.Training!.Network.Neurons.Count.ShouldBeLessThan(opened.Neurons.Count);

        build.Undo();
        autosave.Save();

        var network = repository.Get(creation.Id)!.Training!.Network;
        network.Neurons.ShouldBe(opened.Neurons, ignoreOrder: true);
        network.Connections.ShouldBe(opened.Connections, ignoreOrder: true);
        var ports = NetworkPorts.Of(build.Snapshot());
        DirectNetwork.Compile(network, ports).ShouldBe(DirectNetwork.Compile(opened, ports));
    }

    [Fact]
    public void PartsAddedInOneVisit_NeverReuseANeuronId_AndStayPassiveWhenAnEarlierOneIsDeleted()
    {
        var creation = Trained(Creature());
        var (repository, build, autosave) = Open(creation);
        build.Unlock();
        NeuralNetworkDef SavedNetwork()
        {
            autosave.Save();
            return repository.Get(creation.Id)!.Training!.Network;
        }

        var first = build.PlacePart(BuildPart.Accelerometer, Beam(11))!.Value;
        var withFirst = SavedNetwork();
        build.MoveNode(1, new Vector2D(0, 10));
        var firstIds = withFirst.Neurons.Where(neuron => neuron.PartId == first).Select(neuron => neuron.Id).ToList();
        SavedNetwork().Neurons.Where(neuron => neuron.PartId == first).Select(neuron => neuron.Id).ShouldBe(firstIds);
        var second = build.PlacePart(BuildPart.Accelerometer, Beam(12))!.Value;
        build.SelectOnly(CreatureElementKind.Sensor, first);
        build.DeleteSelectedParts();
        var withSecond = SavedNetwork();

        withSecond.NextNeuronId.ShouldBeGreaterThanOrEqualTo(withFirst.NextNeuronId);
        withSecond.Neurons.Where(neuron => neuron.PartId == second).Select(neuron => neuron.Id).ShouldAllBe(id => !firstIds.Contains(id));
        var ports = NetworkPorts.Of(repository.Get(creation.Id)!.Creature);
        var genome = DirectNetwork.Compile(withSecond, ports);
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
        [
            new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(100, 0)), new NodeDef(3, new Vector2D(0, 100)), new NodeDef(4, new Vector2D(100, 100)),
            new BeamDef(10, 1, 2), new BeamDef(11, 1, 3), new BeamDef(12, 2, 4), new SensorDef(20, 10, SensorKind.Accelerometer), new PistonDef(30, 3, 4),
        ],
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
