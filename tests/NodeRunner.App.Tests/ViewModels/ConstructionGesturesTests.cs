using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.ViewModels;

public class ConstructionGesturesTests
{
    private static readonly Vector2D _empty = new(300, 300);

    [Fact]
    public void Move_TapOnEmptyCanvas_DeselectsWithoutAddingAJoint()
    {
        var (construction, gestures) = TwoJointsAndABeam();
        construction.ReplaceSelection([0]);

        Tap(gestures, _empty);

        construction.Nodes.Count.ShouldBe(2);
        construction.SelectedPartCount.ShouldBe(0);
    }

    [Fact]
    public void Move_TapOnJoint_SelectsOnlyThatJoint()
    {
        var (construction, gestures) = TwoJointsAndABeam();
        construction.ReplaceSelection([0]);

        Tap(gestures, new Vector2D(102, 1));

        construction.SelectedNodeIndices.ShouldBe([1]);
    }

    [Fact]
    public void Move_TapOnBeam_SelectsTheBeam()
    {
        var (construction, gestures) = TwoJointsAndABeam();

        Tap(gestures, new Vector2D(50, 5));

        construction.SingleSelectedBeamIndex.ShouldBe(0);
    }

    [Fact]
    public void Move_DragJoint_MovesItWithoutChangingSelection()
    {
        var (construction, gestures) = TwoJointsAndABeam();
        IReadOnlyCollection<int>? dragStarted = null;
        gestures.NodeDragStarting += (_, nodes) => dragStarted = nodes;

        gestures.Press(new Vector2D(0, 0));
        gestures.Drag(new Vector2D(0, 40));
        gestures.Release(new Vector2D(0, 40));

        construction.Nodes[0].Position.ShouldBe(new Vector2D(0, 40));
        construction.SelectedPartCount.ShouldBe(0);
        dragStarted.ShouldBe([0]);
    }

    [Fact]
    public void Move_SmallWobbleOnJoint_IsATapNotAMove()
    {
        var (construction, gestures) = TwoJointsAndABeam();

        gestures.Press(new Vector2D(0, 0));
        gestures.Drag(new Vector2D(3, 3));
        gestures.Release(new Vector2D(3, 3));

        construction.Nodes[0].Position.ShouldBe(new Vector2D(0, 0));
        construction.SelectedNodeIndices.ShouldBe([0]);
    }

    [Fact]
    public void Move_DragOnEmptyCanvas_ChangesNothing()
    {
        var (construction, gestures) = TwoJointsAndABeam();
        construction.ReplaceSelection([0]);
        var changes = CountChanges(construction);

        gestures.Press(_empty);
        gestures.Drag(new Vector2D(400, 300));
        gestures.Release(new Vector2D(400, 300));

        changes().ShouldBe(0);
        construction.SelectedNodeIndices.ShouldBe([0]);
    }

    [Fact]
    public void Beam_DragBetweenTwoJoints_AddsOneBeam()
    {
        var (construction, gestures) = ThreeLooseJoints(ConstructionTool.Beam);

        gestures.Press(new Vector2D(0, 0));
        gestures.Drag(new Vector2D(60, 0));
        gestures.BeamStartNode.ShouldBe(0);
        gestures.BeamEnd.ShouldBe(new Vector2D(60, 0));
        gestures.Drag(new Vector2D(98, 2));
        gestures.BeamTargetNode.ShouldBe(1);
        gestures.Release(new Vector2D(98, 2));

        construction.Beams.ShouldBe([new BeamDef(0, 1)]);
        gestures.BeamStartNode.ShouldBeNull();
    }

    [Fact]
    public void Beam_ReleaseOnEmptyCanvasOrStartJoint_AddsNothing()
    {
        var (construction, gestures) = ThreeLooseJoints(ConstructionTool.Beam);

        gestures.Press(new Vector2D(0, 0));
        gestures.Drag(_empty);
        gestures.Release(_empty);
        gestures.Press(new Vector2D(0, 0));
        gestures.Drag(new Vector2D(20, 0));
        gestures.Drag(new Vector2D(1, 1));
        gestures.Release(new Vector2D(1, 1));

        construction.Beams.ShouldBeEmpty();
    }

    [Fact]
    public void Beam_TapOneJointThenAnother_AddsNothing()
    {
        var (construction, gestures) = ThreeLooseJoints(ConstructionTool.Beam);

        Tap(gestures, new Vector2D(0, 0));
        Tap(gestures, new Vector2D(100, 0));

        construction.Beams.ShouldBeEmpty();
    }

    [Fact]
    public void Beam_DuplicatePair_AddsNothing()
    {
        var (construction, gestures) = TwoJointsAndABeam();
        construction.ActiveTool = ConstructionTool.Beam;

        gestures.Press(new Vector2D(0, 0));
        gestures.Drag(new Vector2D(100, 0));
        gestures.Release(new Vector2D(100, 0));

        construction.Beams.Count.ShouldBe(1);
    }

    [Fact]
    public void Beam_WhenLocked_DoesNotStart()
    {
        var construction = new ConstructionViewModel();
        construction.Load(
            new CreatureDef([new NodeDef(new Vector2D(0, 0), 18), new NodeDef(new Vector2D(100, 0), 18), new NodeDef(new Vector2D(0, 100), 18)], [new BeamDef(0, 1), new BeamDef(0, 2)], []),
            moveOnly: true);
        construction.ActiveTool = ConstructionTool.Beam;
        var gestures = new ConstructionGestures(construction);

        gestures.Press(new Vector2D(100, 0));
        gestures.BeamStartNode.ShouldBeNull();
        gestures.Drag(new Vector2D(0, 100));
        gestures.BeamEnd.ShouldBeNull();
        gestures.Release(new Vector2D(0, 100));

        construction.Beams.Count.ShouldBe(2);
    }

    [Fact]
    public void Joint_TapOnEmptyCanvas_AddsOneJoint()
    {
        var (construction, gestures) = ThreeLooseJoints(ConstructionTool.Joint);

        Tap(gestures, _empty);

        construction.Nodes.Count.ShouldBe(4);
        construction.Nodes[3].Position.ShouldBe(_empty);
        construction.Nodes[3].Radius.ShouldBe(ConstructionGestures.NewNodeRadius);
    }

    [Fact]
    public void Joint_TapOnBeam_SplitsIt()
    {
        var (construction, gestures) = TwoJointsAndABeam();
        construction.ActiveTool = ConstructionTool.Joint;

        Tap(gestures, new Vector2D(50, 6));

        construction.Nodes[2].Position.ShouldBe(new Vector2D(50, 0));
        construction.Beams.ShouldBe([new BeamDef(0, 2), new BeamDef(2, 1)]);
    }

    [Fact]
    public void Joint_TapOnJointNearABeam_ChangesNothing()
    {
        var (construction, gestures) = TwoJointsAndABeam();
        construction.ActiveTool = ConstructionTool.Joint;
        var changes = CountChanges(construction);

        Tap(gestures, new Vector2D(20, 0));

        changes().ShouldBe(0);
    }

    [Fact]
    public void Joint_Drag_AddsNothing()
    {
        var (construction, gestures) = ThreeLooseJoints(ConstructionTool.Joint);

        gestures.Press(_empty);
        gestures.Drag(new Vector2D(400, 300));
        gestures.Release(new Vector2D(400, 300));

        construction.Nodes.Count.ShouldBe(3);
    }

    [Fact]
    public void Joint_WhenLocked_AddsNothing()
    {
        var construction = new ConstructionViewModel();
        construction.Load(
            new CreatureDef([new NodeDef(new Vector2D(0, 0), 18), new NodeDef(new Vector2D(100, 0), 18)], [new BeamDef(0, 1)], []),
            moveOnly: true);
        construction.ActiveTool = ConstructionTool.Joint;
        var gestures = new ConstructionGestures(construction);

        Tap(gestures, _empty);
        Tap(gestures, new Vector2D(50, 0));

        construction.Nodes.Count.ShouldBe(2);
        construction.Beams.Count.ShouldBe(1);
    }

    [Fact]
    public void ToolChangeMidGesture_CancelsWithoutEditing()
    {
        var (construction, gestures) = ThreeLooseJoints(ConstructionTool.Beam);

        gestures.Press(new Vector2D(0, 0));
        gestures.Drag(new Vector2D(60, 0));
        construction.ActiveTool = ConstructionTool.Joint;
        gestures.Drag(new Vector2D(100, 0));
        gestures.Release(new Vector2D(100, 0));

        construction.Beams.ShouldBeEmpty();
        construction.Nodes.Count.ShouldBe(3);
        gestures.BeamStartNode.ShouldBeNull();
    }

    [Fact]
    public void Cancel_DropsTheBeamPreviewWithoutEditing()
    {
        var (construction, gestures) = ThreeLooseJoints(ConstructionTool.Beam);
        gestures.Press(new Vector2D(0, 0));
        gestures.Drag(new Vector2D(100, 0));
        var redraws = 0;
        gestures.Changed += (_, _) => redraws++;

        gestures.Cancel();
        gestures.Release(new Vector2D(100, 0));

        construction.Beams.ShouldBeEmpty();
        gestures.BeamStartNode.ShouldBeNull();
        redraws.ShouldBe(1);
    }

    [Fact]
    public void Select_BoxDraggedInAnyDirection_ReplacesSelection()
    {
        var (construction, gestures) = ThreeLooseJoints(ConstructionTool.Select);
        construction.ReplaceSelection([2]);

        gestures.Press(new Vector2D(150, -50));
        gestures.Drag(new Vector2D(-50, 50));
        gestures.SelectionBox.ShouldBe((new Vector2D(150, -50), new Vector2D(-50, 50)));
        gestures.Release(new Vector2D(-50, 50));

        construction.SelectedNodeIndices.OrderBy(index => index).ShouldBe([0, 1]);
        gestures.SelectionBox.ShouldBeNull();
    }

    [Fact]
    public void Beam_DragOverAnAlreadyJoinedJoint_DoesNotSnapToIt()
    {
        var (construction, gestures) = ThreeLooseJoints(ConstructionTool.Beam);
        construction.ConnectBeam(0, 1);

        gestures.Press(new Vector2D(0, 0));
        gestures.Drag(new Vector2D(100, 0));

        gestures.BeamTargetNode.ShouldBeNull();
        gestures.BeamEnd.ShouldBe(new Vector2D(100, 0));
    }

    [Fact]
    public void Select_DragASelectedJoint_MovesTheSelectionTogether()
    {
        var (construction, gestures) = ThreeLooseJoints(ConstructionTool.Select);
        construction.ReplaceSelection([0, 1]);

        gestures.Press(new Vector2D(0, 0));
        gestures.Drag(new Vector2D(0, 30));
        gestures.Release(new Vector2D(0, 30));

        construction.Nodes[0].Position.ShouldBe(new Vector2D(0, 30));
        construction.Nodes[1].Position.ShouldBe(new Vector2D(100, 30));
        construction.Nodes[2].Position.ShouldBe(new Vector2D(0, 100));
    }

    [Fact]
    public void Select_DragASelectedJoint_ReportsTheWholeSelectionAsMoving()
    {
        var (construction, gestures) = ThreeLooseJoints(ConstructionTool.Select);
        construction.ReplaceSelection([0, 1]);
        IReadOnlyCollection<int>? moving = null;
        gestures.NodeDragStarting += (_, nodes) => moving = nodes;

        gestures.Press(new Vector2D(0, 0));
        gestures.Drag(new Vector2D(0, 30));

        moving!.OrderBy(index => index).ShouldBe([0, 1]);
    }

    [Fact]
    public void Move_DragWithASelection_ReportsOnlyTheDraggedJointAsMoving()
    {
        var (construction, gestures) = ThreeLooseJoints(ConstructionTool.Move);
        construction.ReplaceSelection([0, 1]);
        IReadOnlyCollection<int>? moving = null;
        gestures.NodeDragStarting += (_, nodes) => moving = nodes;

        gestures.Press(new Vector2D(0, 0));
        gestures.Drag(new Vector2D(0, 30));

        moving.ShouldBe([0]);
        construction.Nodes[1].Position.ShouldBe(new Vector2D(100, 0));
    }

    [Fact]
    public void Core_TapOnJoint_TogglesItsCore()
    {
        var (construction, gestures) = ThreeLooseJoints(ConstructionTool.Core);

        Tap(gestures, new Vector2D(0, 0));

        construction.Cores.ShouldBe([new CoreDef(0)]);
    }

    private static (ConstructionViewModel Construction, ConstructionGestures Gestures) TwoJointsAndABeam()
    {
        var construction = new ConstructionViewModel();
        construction.PlaceNode(new Vector2D(0, 0), 18);
        construction.PlaceNode(new Vector2D(100, 0), 18);
        construction.ConnectBeam(0, 1);
        return (construction, new ConstructionGestures(construction));
    }

    private static (ConstructionViewModel Construction, ConstructionGestures Gestures) ThreeLooseJoints(ConstructionTool tool)
    {
        var construction = new ConstructionViewModel();
        construction.PlaceNode(new Vector2D(0, 0), 18);
        construction.PlaceNode(new Vector2D(100, 0), 18);
        construction.PlaceNode(new Vector2D(0, 100), 18);
        construction.ActiveTool = tool;
        return (construction, new ConstructionGestures(construction));
    }

    private static void Tap(ConstructionGestures gestures, Vector2D position)
    {
        gestures.Press(position);
        gestures.Release(position);
    }

    private static Func<int> CountChanges(ConstructionViewModel construction)
    {
        var changes = 0;
        construction.AnatomyChanged += (_, _) => changes++;
        return () => changes;
    }
}
