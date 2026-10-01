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
    public void Move_DragOnEmptyCanvas_PansTheViewWithoutEditing()
    {
        var (construction, gestures) = TwoJointsAndABeam();
        construction.ReplaceSelection([0]);
        var changes = CountChanges(construction);

        gestures.Press(_empty);
        gestures.Drag(new Vector2D(350, 300));
        gestures.Drag(new Vector2D(400, 280));
        gestures.Release(new Vector2D(400, 280));

        changes().ShouldBe(0);
        construction.SelectedNodeIndices.ShouldBe([0]);
        gestures.View.Offset.ShouldBe(new Vector2D(100, -20));
    }

    [Fact]
    public void Move_DragOnEmptyCanvas_TellsTheCanvasToRedraw()
    {
        var (_, gestures) = TwoJointsAndABeam();
        var changes = 0;
        gestures.View.Changed += (_, _) => changes++;

        gestures.Press(_empty);
        gestures.Drag(new Vector2D(_empty.X + 40, _empty.Y));

        changes.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Move_DragOnABeam_PansWithoutEditing()
    {
        var (construction, gestures) = TwoJointsAndABeam();
        var changes = CountChanges(construction);

        gestures.Press(new Vector2D(50, 0));
        gestures.Drag(new Vector2D(50, 60));
        gestures.Release(new Vector2D(50, 60));

        changes().ShouldBe(0);
        gestures.View.Offset.ShouldBe(new Vector2D(0, 60));
    }

    [Fact]
    public void Pinch_ZoomsAboutTheFingersAndPansWithThem()
    {
        var (construction, gestures) = ThreeLooseJoints(ConstructionTool.Joint);
        var changes = CountChanges(construction);

        gestures.Press(new Vector2D(300, 300), 0);
        gestures.Press(new Vector2D(400, 300), 1);
        gestures.Drag(new Vector2D(250, 300), 0);
        gestures.Drag(new Vector2D(450, 300), 1);
        gestures.Release(new Vector2D(250, 300), 0);
        gestures.Release(new Vector2D(450, 300), 1);

        gestures.View.Zoom.ShouldBe(2, 1e-9);
        gestures.View.ToCanvas(new Vector2D(350, 300)).X.ShouldBe(350, 1e-9);
        gestures.View.ToCanvas(new Vector2D(350, 300)).Y.ShouldBe(300, 1e-9);
        changes().ShouldBe(0);
    }

    [Fact]
    public void TwoFingerDrag_PansWhateverTheTool()
    {
        var (_, gestures) = ThreeLooseJoints(ConstructionTool.Beam);

        gestures.Press(new Vector2D(300, 300), 0);
        gestures.Press(new Vector2D(400, 300), 1);
        gestures.Drag(new Vector2D(300, 340), 0);
        gestures.Drag(new Vector2D(400, 340), 1);

        gestures.View.Zoom.ShouldBe(1, 1e-9);
        gestures.View.Offset.X.ShouldBe(0, 1e-9);
        gestures.View.Offset.Y.ShouldBe(40, 1e-9);
    }

    [Fact]
    public void SecondFinger_PutsBackAJointTheFirstFingerAlreadyMoved()
    {
        var (construction, gestures) = ThreeLooseJoints(ConstructionTool.Move);

        gestures.Press(new Vector2D(0, 0), 0);
        gestures.Drag(new Vector2D(0, 40), 0);
        gestures.Press(new Vector2D(200, 200), 1);

        construction.Nodes[0].Position.ShouldBe(new Vector2D(0, 0));
    }

    [Fact]
    public void SecondFinger_PutsBackASelectionTheFirstFingerAlreadyMoved()
    {
        var (construction, gestures) = ThreeLooseJoints(ConstructionTool.Select);
        construction.ReplaceSelection([0, 1]);

        gestures.Press(new Vector2D(0, 0), 0);
        gestures.Drag(new Vector2D(0, 40), 0);
        gestures.Press(new Vector2D(200, 200), 1);

        construction.Nodes[0].Position.ShouldBe(new Vector2D(0, 0));
        construction.Nodes[1].Position.ShouldBe(new Vector2D(100, 0));
    }

    [Fact]
    public void SecondFinger_DropsTheBeamPreviewAndNothingIsJoinedOnRelease()
    {
        var (construction, gestures) = ThreeLooseJoints(ConstructionTool.Beam);

        gestures.Press(new Vector2D(0, 0), 0);
        gestures.Drag(new Vector2D(100, 0), 0);
        gestures.Press(new Vector2D(200, 200), 1);
        gestures.BeamStartNode.ShouldBeNull();
        gestures.Release(new Vector2D(100, 0), 0);
        gestures.Release(new Vector2D(200, 200), 1);

        construction.Beams.ShouldBeEmpty();
    }

    [Fact]
    public void AfterAPinch_TheFingerLeftDownDoesNothingUntilAllLift()
    {
        var (construction, gestures) = ThreeLooseJoints(ConstructionTool.Joint);

        gestures.Press(new Vector2D(300, 300), 0);
        gestures.Press(new Vector2D(400, 300), 1);
        gestures.Release(new Vector2D(400, 300), 1);
        gestures.Drag(new Vector2D(320, 300), 0);
        gestures.Release(new Vector2D(320, 300), 0);
        construction.Nodes.Count.ShouldBe(3);
        gestures.View.Offset.ShouldBe(new Vector2D(0, 0));

        Tap(gestures, _empty);

        construction.Nodes.Count.ShouldBe(4);
    }

    [Fact]
    public void ZoomedIn_TapsLandOnTheCreatureUnderTheFinger()
    {
        var (construction, gestures) = ThreeLooseJoints(ConstructionTool.Move);
        gestures.View.ZoomAbout(new Vector2D(0, 0), 2);
        gestures.View.PanBy(new Vector2D(50, 50));

        Tap(gestures, new Vector2D(250, 50));

        construction.SelectedNodeIndices.ShouldBe([1]);
    }

    [Fact]
    public void ZoomedIn_JointIsAddedAtTheCanvasPointUnderTheFinger()
    {
        var (construction, gestures) = ThreeLooseJoints(ConstructionTool.Joint);
        gestures.View.ZoomAbout(new Vector2D(0, 0), 2);

        Tap(gestures, new Vector2D(600, 600));

        construction.Nodes[^1].Position.ShouldBe(new Vector2D(300, 300));
    }

    [Fact]
    public void ZoomedIn_HitZoneStaysFingerSizedButCoversTheWholeJoint()
    {
        var (construction, gestures) = ThreeLooseJoints(ConstructionTool.Joint);
        gestures.View.ZoomAbout(new Vector2D(0, 0), CanvasView.MaxZoom);

        Tap(gestures, gestures.View.ToView(new Vector2D(-(ConstructionGestures.NewNodeRadius - 2), 0)));
        construction.Nodes.Count.ShouldBe(3);

        Tap(gestures, gestures.View.ToView(new Vector2D(-25, 0)));
        construction.Nodes.Count.ShouldBe(4);
    }

    [Fact]
    public void ZoomedIn_JointTapBesideABeamPlacesAFreeJoint()
    {
        var (construction, gestures) = TwoJointsAndABeam();
        construction.ActiveTool = ConstructionTool.Joint;
        gestures.View.ZoomAbout(new Vector2D(0, 0), CanvasView.MaxZoom);

        Tap(gestures, gestures.View.ToView(new Vector2D(50, 12)));

        construction.Beams.Count.ShouldBe(1);
        construction.Nodes.Count.ShouldBe(3);
    }

    [Fact]
    public void SecondFinger_PutsBackTheSelectionASelectPressChanged()
    {
        var (construction, gestures) = ThreeLooseJoints(ConstructionTool.Select);
        construction.ReplaceSelection([0, 1]);

        gestures.Press(_empty, 0);
        construction.SelectedNodeIndices.ShouldBeEmpty();
        gestures.Press(new Vector2D(400, 400), 1);

        construction.SelectedNodeIndices.OrderBy(index => index).ShouldBe([0, 1]);
    }

    [Fact]
    public void SecondFinger_PutsBackASelectedBeamASelectPressReplaced()
    {
        var (construction, gestures) = TwoJointsAndABeam();
        construction.ActiveTool = ConstructionTool.Select;
        construction.SelectBeam(0);

        gestures.Press(new Vector2D(0, 0), 0);
        gestures.Press(new Vector2D(400, 400), 1);

        construction.SingleSelectedBeamIndex.ShouldBe(0);
        construction.SelectedNodeIndices.ShouldBeEmpty();
    }

    [Fact]
    public void Cancel_PutsBackAJointTheDragAlreadyMoved()
    {
        var (construction, gestures) = ThreeLooseJoints(ConstructionTool.Move);

        gestures.Press(new Vector2D(0, 0));
        gestures.Drag(new Vector2D(0, 40));
        gestures.Cancel();
        gestures.Release(new Vector2D(0, 40));

        construction.Nodes[0].Position.ShouldBe(new Vector2D(0, 0));
    }

    [Fact]
    public void ZoomedOut_HitRadiusStaysFingerSized()
    {
        var (construction, gestures) = ThreeLooseJoints(ConstructionTool.Move);
        gestures.View.ZoomAbout(new Vector2D(0, 0), 0.5);

        Tap(gestures, new Vector2D(-(ConstructionGestures.NodeHitRadius - 2), 0));

        construction.SelectedNodeIndices.ShouldBe([0]);
    }

    [Fact]
    public void ZoomedOut_SmallWobbleOnJoint_IsStillATap()
    {
        var (construction, gestures) = TwoJointsAndABeam();
        gestures.View.ZoomAbout(new Vector2D(0, 0), 0.125);

        gestures.Press(new Vector2D(0, 0));
        gestures.Drag(new Vector2D(ConstructionGestures.TapSlop - 2, 0));
        gestures.Release(new Vector2D(ConstructionGestures.TapSlop - 2, 0));

        construction.Nodes[0].Position.ShouldBe(new Vector2D(0, 0));
        construction.SelectedNodeIndices.ShouldBe([0]);
    }

    [Fact]
    public void Fit_FramesEveryJointWithRoomForItsMotorArc()
    {
        var (_, gestures) = TwoJointsAndABeam();
        gestures.View.VisibleArea = new CanvasRect(new Vector2D(0, 0), new Vector2D(1000, 500));

        gestures.View.Fit();

        gestures.View.ToView(new Vector2D(50, 0)).ShouldBe(new Vector2D(500, 250));
        gestures.View.ZoomAbout(new Vector2D(0, 0), 0.5);
        gestures.View.Fit();
        gestures.View.Zoom.ShouldBe(1);
    }

    [Fact]
    public void Fit_ZoomsOutToShowTheMotorArcs()
    {
        var (_, gestures) = TwoJointsAndABeam();
        gestures.View.VisibleArea = new CanvasRect(new Vector2D(0, 0), new Vector2D(100, 100));

        gestures.View.Fit();

        // Joints at x 0 and 100, radius 18: arcs span -36..136 = 172 units into 60% of 100.
        gestures.View.Zoom.ShouldBe(60.0 / 172, 1e-9);
    }

    [Fact]
    public void View_StaysWithinTheBuildArea()
    {
        var (_, gestures) = TwoJointsAndABeam();
        gestures.View.VisibleArea = new CanvasRect(new Vector2D(0, 0), new Vector2D(1000, 500));

        gestures.Press(new Vector2D(500, 400));
        gestures.Drag(new Vector2D(100000, 400));
        gestures.Release(new Vector2D(100000, 400));

        gestures.View.ToView(ConstructionViewModel.BuildArea.Min).X.ShouldBe(CanvasView.EdgeMargin, 1e-9);
    }

    [Fact]
    public void Joint_TapOutsideTheBuildArea_PlacesNothing()
    {
        var construction = new ConstructionViewModel { ActiveTool = ConstructionTool.Joint };
        var gestures = new ConstructionGestures(construction);
        gestures.View.ZoomAbout(new Vector2D(0, 0), 0.001);

        Tap(gestures, gestures.View.ToView(new Vector2D(ConstructionViewModel.BuildArea.Max.X + 50, 0)));

        construction.Nodes.Count.ShouldBe(0);
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

    [Fact]
    public void Core_PressThatBecomesAPinch_AddsNoCore()
    {
        var (construction, gestures) = ThreeLooseJoints(ConstructionTool.Core);

        gestures.Press(new Vector2D(0, 0), 0);
        gestures.Press(new Vector2D(200, 200), 1);
        gestures.Release(new Vector2D(0, 0), 0);
        gestures.Release(new Vector2D(200, 200), 1);

        construction.Cores.ShouldBeEmpty();
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
