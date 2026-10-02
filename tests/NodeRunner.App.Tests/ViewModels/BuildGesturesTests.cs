using NodeRunner.App.Builders;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.ViewModels;

public class BuildGesturesTests
{
    private static readonly Vector2D _empty = new(300, 300);

    [Fact]
    public void Move_TapOnEmptyCanvas_DeselectsWithoutAddingAJoint()
    {
        var (build, gestures) = TwoJointsAndABeam();
        build.ReplaceSelection([1]);

        Tap(gestures, _empty);

        build.Nodes.Count.ShouldBe(2);
        build.SelectedPartCount.ShouldBe(0);
    }

    [Fact]
    public void Move_TapOnJoint_SelectsOnlyThatJoint()
    {
        var (build, gestures) = TwoJointsAndABeam();
        build.ReplaceSelection([1]);

        Tap(gestures, new Vector2D(102, 1));

        build.SelectedNodeIds.ShouldBe([2]);
    }

    [Fact]
    public void Move_TapOnBeam_SelectsTheBeam()
    {
        var (build, gestures) = TwoJointsAndABeam();

        Tap(gestures, new Vector2D(50, 5));

        build.SingleSelectedBeamId.ShouldBe(3);
    }

    [Fact]
    public void Move_DragJoint_MovesItWithoutChangingSelection()
    {
        var (build, gestures) = TwoJointsAndABeam();
        IReadOnlyCollection<int>? dragStarted = null;
        gestures.NodeDragStarting += (_, nodes) => dragStarted = nodes;

        gestures.Press(new Vector2D(0, 0));
        gestures.Drag(new Vector2D(0, 40));
        gestures.Release(new Vector2D(0, 40));

        build.Nodes[0].Position.ShouldBe(new Vector2D(0, 40));
        build.SelectedPartCount.ShouldBe(0);
        dragStarted.ShouldBe([1]);
    }

    [Fact]
    public void Move_SmallWobbleOnJoint_IsATapNotAMove()
    {
        var (build, gestures) = TwoJointsAndABeam();

        gestures.Press(new Vector2D(0, 0));
        gestures.Drag(new Vector2D(3, 3));
        gestures.Release(new Vector2D(3, 3));

        build.Nodes[0].Position.ShouldBe(new Vector2D(0, 0));
        build.SelectedNodeIds.ShouldBe([1]);
    }

    [Fact]
    public void Move_DragOnEmptyCanvas_PansTheViewWithoutEditing()
    {
        var (build, gestures) = TwoJointsAndABeam();
        build.ReplaceSelection([1]);
        var changes = CountChanges(build);

        gestures.Press(_empty);
        gestures.Drag(new Vector2D(350, 300));
        gestures.Drag(new Vector2D(400, 280));
        gestures.Release(new Vector2D(400, 280));

        changes().ShouldBe(0);
        build.SelectedNodeIds.ShouldBe([1]);
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
        var (build, gestures) = TwoJointsAndABeam();
        var changes = CountChanges(build);

        gestures.Press(new Vector2D(50, 0));
        gestures.Drag(new Vector2D(50, 60));
        gestures.Release(new Vector2D(50, 60));

        changes().ShouldBe(0);
        gestures.View.Offset.ShouldBe(new Vector2D(0, 60));
    }

    [Fact]
    public void Pinch_ZoomsAboutTheFingersAndPansWithThem()
    {
        var (build, gestures) = ThreeLooseJoints(BuildTool.Joint);
        var changes = CountChanges(build);

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
        var (_, gestures) = ThreeLooseJoints(BuildTool.Beam);

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
        var (build, gestures) = ThreeLooseJoints(BuildTool.Move);

        gestures.Press(new Vector2D(0, 0), 0);
        gestures.Drag(new Vector2D(0, 40), 0);
        gestures.Press(new Vector2D(200, 200), 1);

        build.Nodes[0].Position.ShouldBe(new Vector2D(0, 0));
    }

    [Fact]
    public void SecondFinger_PutsBackASelectionTheFirstFingerAlreadyMoved()
    {
        var (build, gestures) = ThreeLooseJoints(BuildTool.Select);
        build.ReplaceSelection([1, 2]);

        gestures.Press(new Vector2D(0, 0), 0);
        gestures.Drag(new Vector2D(0, 40), 0);
        gestures.Press(new Vector2D(200, 200), 1);

        build.Nodes[0].Position.ShouldBe(new Vector2D(0, 0));
        build.Nodes[1].Position.ShouldBe(new Vector2D(100, 0));
    }

    [Fact]
    public void SecondFinger_DropsTheBeamPreviewAndNothingIsJoinedOnRelease()
    {
        var (build, gestures) = ThreeLooseJoints(BuildTool.Beam);

        gestures.Press(new Vector2D(0, 0), 0);
        gestures.Drag(new Vector2D(100, 0), 0);
        gestures.Press(new Vector2D(200, 200), 1);
        gestures.BeamStartNodeId.ShouldBeNull();
        gestures.Release(new Vector2D(100, 0), 0);
        gestures.Release(new Vector2D(200, 200), 1);

        build.Beams.ShouldBeEmpty();
    }

    [Fact]
    public void AfterAPinch_TheFingerLeftDownDoesNothingUntilAllLift()
    {
        var (build, gestures) = ThreeLooseJoints(BuildTool.Joint);

        gestures.Press(new Vector2D(300, 300), 0);
        gestures.Press(new Vector2D(400, 300), 1);
        gestures.Release(new Vector2D(400, 300), 1);
        gestures.Drag(new Vector2D(320, 300), 0);
        gestures.Release(new Vector2D(320, 300), 0);
        build.Nodes.Count.ShouldBe(3);
        gestures.View.Offset.ShouldBe(new Vector2D(0, 0));

        Tap(gestures, _empty);

        build.Nodes.Count.ShouldBe(4);
    }

    [Fact]
    public void ZoomedIn_TapsLandOnTheCreatureUnderTheFinger()
    {
        var (build, gestures) = ThreeLooseJoints(BuildTool.Move);
        gestures.View.ZoomAbout(new Vector2D(0, 0), 2);
        gestures.View.PanBy(new Vector2D(50, 50));

        Tap(gestures, new Vector2D(250, 50));

        build.SelectedNodeIds.ShouldBe([2]);
    }

    [Fact]
    public void ZoomedIn_JointIsAddedAtTheCanvasPointUnderTheFinger()
    {
        var (build, gestures) = ThreeLooseJoints(BuildTool.Joint);
        gestures.View.ZoomAbout(new Vector2D(0, 0), 2);

        Tap(gestures, new Vector2D(600, 600));

        build.Nodes[^1].Position.ShouldBe(new Vector2D(300, 300));
    }

    [Fact]
    public void ZoomedIn_HitZoneStaysFingerSizedButCoversTheWholeJoint()
    {
        var (build, gestures) = ThreeLooseJoints(BuildTool.Joint);
        gestures.View.ZoomAbout(new Vector2D(0, 0), CanvasView.MaxZoom);

        Tap(gestures, gestures.View.ToView(new Vector2D(-(BuildGestures.NewNodeRadius - 2), 0)));
        build.Nodes.Count.ShouldBe(3);

        Tap(gestures, gestures.View.ToView(new Vector2D(-25, 0)));
        build.Nodes.Count.ShouldBe(4);
    }

    [Fact]
    public void ZoomedIn_JointTapBesideABeamPlacesAFreeJoint()
    {
        var (build, gestures) = TwoJointsAndABeam();
        build.ActiveTool = BuildTool.Joint;
        gestures.View.ZoomAbout(new Vector2D(0, 0), CanvasView.MaxZoom);

        Tap(gestures, gestures.View.ToView(new Vector2D(50, 12)));

        build.Beams.Count.ShouldBe(1);
        build.Nodes.Count.ShouldBe(3);
    }

    [Fact]
    public void SecondFinger_PutsBackTheSelectionASelectPressChanged()
    {
        var (build, gestures) = ThreeLooseJoints(BuildTool.Select);
        build.ReplaceSelection([1, 2]);

        gestures.Press(_empty, 0);
        build.SelectedNodeIds.ShouldBeEmpty();
        gestures.Press(new Vector2D(400, 400), 1);

        build.SelectedNodeIds.OrderBy(id => id).ShouldBe([1, 2]);
    }

    [Fact]
    public void SecondFinger_PutsBackASelectedBeamASelectPressReplaced()
    {
        var (build, gestures) = TwoJointsAndABeam();
        build.ActiveTool = BuildTool.Select;
        build.SelectBeam(build.Beams[0].Id);

        gestures.Press(new Vector2D(0, 0), 0);
        gestures.Press(new Vector2D(400, 400), 1);

        build.SingleSelectedBeamId.ShouldBe(3);
        build.SelectedNodeIds.ShouldBeEmpty();
    }

    [Fact]
    public void Cancel_PutsBackAJointTheDragAlreadyMoved()
    {
        var (build, gestures) = ThreeLooseJoints(BuildTool.Move);

        gestures.Press(new Vector2D(0, 0));
        gestures.Drag(new Vector2D(0, 40));
        gestures.Cancel();
        gestures.Release(new Vector2D(0, 40));

        build.Nodes[0].Position.ShouldBe(new Vector2D(0, 0));
    }

    [Fact]
    public void ZoomedOut_HitRadiusStaysFingerSized()
    {
        var (build, gestures) = ThreeLooseJoints(BuildTool.Move);
        gestures.View.ZoomAbout(new Vector2D(0, 0), 0.5);

        Tap(gestures, new Vector2D(-(BuildGestures.NodeHitRadius - 2), 0));

        build.SelectedNodeIds.ShouldBe([1]);
    }

    [Fact]
    public void ZoomedOut_SmallWobbleOnJoint_IsStillATap()
    {
        var (build, gestures) = TwoJointsAndABeam();
        gestures.View.ZoomAbout(new Vector2D(0, 0), 0.125);

        gestures.Press(new Vector2D(0, 0));
        gestures.Drag(new Vector2D(BuildGestures.TapSlop - 2, 0));
        gestures.Release(new Vector2D(BuildGestures.TapSlop - 2, 0));

        build.Nodes[0].Position.ShouldBe(new Vector2D(0, 0));
        build.SelectedNodeIds.ShouldBe([1]);
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
    public void View_ShowsTheMarginPastTheBuildArea_AtEveryZoom()
    {
        var (_, gestures) = TwoJointsAndABeam();
        gestures.View.VisibleArea = new CanvasRect(new Vector2D(0, 0), new Vector2D(1000, 500));
        foreach (var factor in new[] { 1.0, 3, 0.25 })
        {
            gestures.View.ZoomAbout(new Vector2D(500, 250), factor);

            gestures.Press(new Vector2D(500, 400));
            gestures.Drag(new Vector2D(100000, 400));
            gestures.Release(new Vector2D(100000, 400));

            var areaEdge = gestures.View.ToCanvas(new Vector2D(0, 0)).X + BuildViewModel.BuildViewMargin;
            areaEdge.ShouldBe(BuildViewModel.BuildArea.Min.X, 1e-6);
        }
    }

    [Fact]
    public void Joint_TapOutsideTheBuildArea_PlacesNothing()
    {
        var build = new BuildViewModel { ActiveTool = BuildTool.Joint };
        var gestures = new BuildGestures(build);
        gestures.View.ZoomAbout(new Vector2D(0, 0), 0.001);

        Tap(gestures, gestures.View.ToView(new Vector2D(BuildViewModel.BuildArea.Max.X + 50, 0)));

        build.Nodes.Count.ShouldBe(0);
    }

    [Fact]
    public void Beam_DragBetweenTwoJoints_AddsOneBeam()
    {
        var (build, gestures) = ThreeLooseJoints(BuildTool.Beam);

        gestures.Press(new Vector2D(0, 0));
        gestures.Drag(new Vector2D(60, 0));
        gestures.BeamStartNodeId.ShouldBe(1);
        gestures.BeamEnd.ShouldBe(new Vector2D(60, 0));
        gestures.Drag(new Vector2D(98, 2));
        gestures.BeamTargetNodeId.ShouldBe(2);
        gestures.Release(new Vector2D(98, 2));

        build.Beams.ShouldBe([new BeamDef(4, 1, 2)]);
        gestures.BeamStartNodeId.ShouldBeNull();
    }

    [Fact]
    public void Beam_ReleaseOnEmptyCanvasOrStartJoint_AddsNothing()
    {
        var (build, gestures) = ThreeLooseJoints(BuildTool.Beam);

        gestures.Press(new Vector2D(0, 0));
        gestures.Drag(_empty);
        gestures.Release(_empty);
        gestures.Press(new Vector2D(0, 0));
        gestures.Drag(new Vector2D(20, 0));
        gestures.Drag(new Vector2D(1, 1));
        gestures.Release(new Vector2D(1, 1));

        build.Beams.ShouldBeEmpty();
    }

    [Fact]
    public void Beam_TapOneJointThenAnother_AddsNothing()
    {
        var (build, gestures) = ThreeLooseJoints(BuildTool.Beam);

        Tap(gestures, new Vector2D(0, 0));
        Tap(gestures, new Vector2D(100, 0));

        build.Beams.ShouldBeEmpty();
    }

    [Fact]
    public void Beam_DuplicatePair_AddsNothing()
    {
        var (build, gestures) = TwoJointsAndABeam();
        build.ActiveTool = BuildTool.Beam;

        gestures.Press(new Vector2D(0, 0));
        gestures.Drag(new Vector2D(100, 0));
        gestures.Release(new Vector2D(100, 0));

        build.Beams.Count.ShouldBe(1);
    }

    [Fact]
    public void Beam_WhenLocked_DoesNotStart()
    {
        var build = new BuildViewModel();
        build.Load(
            new CreatureDef([new NodeDef(1, new Vector2D(0, 0), 18), new NodeDef(2, new Vector2D(100, 0), 18), new NodeDef(3, new Vector2D(0, 100), 18)], [new BeamDef(101, 1, 2), new BeamDef(102, 1, 3)], []),
            moveOnly: true);
        build.ActiveTool = BuildTool.Beam;
        var gestures = new BuildGestures(build);

        gestures.Press(new Vector2D(100, 0));
        gestures.BeamStartNodeId.ShouldBeNull();
        gestures.Drag(new Vector2D(0, 100));
        gestures.BeamEnd.ShouldBeNull();
        gestures.Release(new Vector2D(0, 100));

        build.Beams.Count.ShouldBe(2);
    }

    [Fact]
    public void Joint_TapOnEmptyCanvas_AddsOneJoint()
    {
        var (build, gestures) = ThreeLooseJoints(BuildTool.Joint);

        Tap(gestures, _empty);

        build.Nodes.Count.ShouldBe(4);
        build.Nodes[3].Position.ShouldBe(_empty);
        build.Nodes[3].Radius.ShouldBe(BuildGestures.NewNodeRadius);
    }

    [Fact]
    public void Joint_TapOnBeam_SplitsIt()
    {
        var (build, gestures) = TwoJointsAndABeam();
        build.ActiveTool = BuildTool.Joint;

        Tap(gestures, new Vector2D(50, 6));

        build.Nodes[2].Position.ShouldBe(new Vector2D(50, 0));
        build.Beams.ShouldBe([new BeamDef(5, 1, 4), new BeamDef(6, 4, 2)]);
    }

    [Fact]
    public void Joint_TapOnJointNearABeam_ChangesNothing()
    {
        var (build, gestures) = TwoJointsAndABeam();
        build.ActiveTool = BuildTool.Joint;
        var changes = CountChanges(build);

        Tap(gestures, new Vector2D(20, 0));

        changes().ShouldBe(0);
    }

    [Fact]
    public void Joint_Drag_AddsNothing()
    {
        var (build, gestures) = ThreeLooseJoints(BuildTool.Joint);

        gestures.Press(_empty);
        gestures.Drag(new Vector2D(400, 300));
        gestures.Release(new Vector2D(400, 300));

        build.Nodes.Count.ShouldBe(3);
    }

    [Fact]
    public void Joint_WhenLocked_AddsNothing()
    {
        var build = new BuildViewModel();
        build.Load(
            new CreatureDef([new NodeDef(1, new Vector2D(0, 0), 18), new NodeDef(2, new Vector2D(100, 0), 18)], [new BeamDef(101, 1, 2)], []),
            moveOnly: true);
        build.ActiveTool = BuildTool.Joint;
        var gestures = new BuildGestures(build);

        Tap(gestures, _empty);
        Tap(gestures, new Vector2D(50, 0));

        build.Nodes.Count.ShouldBe(2);
        build.Beams.Count.ShouldBe(1);
    }

    [Fact]
    public void ToolChangeMidGesture_CancelsWithoutEditing()
    {
        var (build, gestures) = ThreeLooseJoints(BuildTool.Beam);

        gestures.Press(new Vector2D(0, 0));
        gestures.Drag(new Vector2D(60, 0));
        build.ActiveTool = BuildTool.Joint;
        gestures.Drag(new Vector2D(100, 0));
        gestures.Release(new Vector2D(100, 0));

        build.Beams.ShouldBeEmpty();
        build.Nodes.Count.ShouldBe(3);
        gestures.BeamStartNodeId.ShouldBeNull();
    }

    [Fact]
    public void Cancel_DropsTheBeamPreviewWithoutEditing()
    {
        var (build, gestures) = ThreeLooseJoints(BuildTool.Beam);
        gestures.Press(new Vector2D(0, 0));
        gestures.Drag(new Vector2D(100, 0));
        var redraws = 0;
        gestures.Changed += (_, _) => redraws++;

        gestures.Cancel();
        gestures.Release(new Vector2D(100, 0));

        build.Beams.ShouldBeEmpty();
        gestures.BeamStartNodeId.ShouldBeNull();
        redraws.ShouldBe(1);
    }

    [Fact]
    public void Select_BoxDraggedInAnyDirection_ReplacesSelection()
    {
        var (build, gestures) = ThreeLooseJoints(BuildTool.Select);
        build.ReplaceSelection([3]);

        gestures.Press(new Vector2D(150, -50));
        gestures.Drag(new Vector2D(-50, 50));
        gestures.SelectionBox.ShouldBe((new Vector2D(150, -50), new Vector2D(-50, 50)));
        gestures.Release(new Vector2D(-50, 50));

        build.SelectedNodeIds.OrderBy(id => id).ShouldBe([1, 2]);
        gestures.SelectionBox.ShouldBeNull();
    }

    [Fact]
    public void Beam_DragOverAnAlreadyJoinedJoint_DoesNotSnapToIt()
    {
        var (build, gestures) = ThreeLooseJoints(BuildTool.Beam);
        build.ConnectBeam(1, 2);

        gestures.Press(new Vector2D(0, 0));
        gestures.Drag(new Vector2D(100, 0));

        gestures.BeamTargetNodeId.ShouldBeNull();
        gestures.BeamEnd.ShouldBe(new Vector2D(100, 0));
    }

    [Fact]
    public void Select_DragASelectedJoint_MovesTheSelectionTogether()
    {
        var (build, gestures) = ThreeLooseJoints(BuildTool.Select);
        build.ReplaceSelection([1, 2]);

        gestures.Press(new Vector2D(0, 0));
        gestures.Drag(new Vector2D(0, 30));
        gestures.Release(new Vector2D(0, 30));

        build.Nodes[0].Position.ShouldBe(new Vector2D(0, 30));
        build.Nodes[1].Position.ShouldBe(new Vector2D(100, 30));
        build.Nodes[2].Position.ShouldBe(new Vector2D(0, 100));
    }

    [Fact]
    public void Select_DragASelectedJoint_ReportsTheWholeSelectionAsMoving()
    {
        var (build, gestures) = ThreeLooseJoints(BuildTool.Select);
        build.ReplaceSelection([1, 2]);
        IReadOnlyCollection<int>? moving = null;
        gestures.NodeDragStarting += (_, nodes) => moving = nodes;

        gestures.Press(new Vector2D(0, 0));
        gestures.Drag(new Vector2D(0, 30));

        moving!.OrderBy(id => id).ShouldBe([1, 2]);
    }

    [Fact]
    public void Move_DragWithASelection_ReportsOnlyTheDraggedJointAsMoving()
    {
        var (build, gestures) = ThreeLooseJoints(BuildTool.Move);
        build.ReplaceSelection([1, 2]);
        IReadOnlyCollection<int>? moving = null;
        gestures.NodeDragStarting += (_, nodes) => moving = nodes;

        gestures.Press(new Vector2D(0, 0));
        gestures.Drag(new Vector2D(0, 30));

        moving.ShouldBe([1]);
        build.Nodes[1].Position.ShouldBe(new Vector2D(100, 0));
    }



    // With joints 0 and 1 of ThreeLooseJoints selected at 1×, the frame clears both halos by 8
    // across and meets the 96 minimum down: Move sits at (50, 0), Rotate at (50, -80), Scale at its corner.
    private static readonly double _frameRight = 100 + (18 * BuildGestures.SelectedHaloScale) + 8;
    private static readonly Vector2D _moveHandle = new(50, 0);
    private static readonly Vector2D _rotateHandle = new(50, -80);
    private static readonly Vector2D _scaleHandle = new(_frameRight, 48);

    [Fact]
    public void Select_TapOnAnUnselectedJoint_AddsIt()
    {
        var (build, gestures) = ThreeLooseJoints(BuildTool.Select);
        build.ReplaceSelection([1]);

        Tap(gestures, new Vector2D(0, 100));

        build.SelectedNodeIds.OrderBy(id => id).ShouldBe([1, 3]);
    }

    [Fact]
    public void Select_TapOnASelectedJoint_RemovesIt()
    {
        var (build, gestures) = ThreeLooseJoints(BuildTool.Select);
        build.ReplaceSelection([1, 2]);

        Tap(gestures, new Vector2D(0, 0));

        build.SelectedNodeIds.ShouldBe([2]);
    }

    [Fact]
    public void Select_TapOnABeam_ClearsTheSelection()
    {
        var (build, gestures) = TwoJointsAndABeam();
        build.ActiveTool = BuildTool.Select;
        build.ReplaceSelection([1]);

        Tap(gestures, new Vector2D(50, 0));

        build.SelectedPartCount.ShouldBe(0);
    }

    [Fact]
    public void Select_TwoJoints_ShowAFrameWithThreeHandles()
    {
        var (build, gestures) = ThreeLooseJoints(BuildTool.Select);
        build.ReplaceSelection([1]);
        gestures.SelectionFrame.ShouldBeNull();

        build.ReplaceSelection([1, 2]);

        var frame = gestures.SelectionFrame.ShouldNotBeNull();
        frame.Min.X.ShouldBe(100 - _frameRight, 1e-9);
        frame.Min.Y.ShouldBe(-48);
        frame.Max.X.ShouldBe(_frameRight, 1e-9);
        frame.Max.Y.ShouldBe(48);
        gestures.SelectionHandles.ShouldBe([
            (SelectionHandle.Move, _moveHandle),
            (SelectionHandle.Rotate, _rotateHandle),
            (SelectionHandle.Scale, _scaleHandle)]);
    }

    [Fact]
    public void Select_WhenLocked_StillOffersAllThreeHandles()
    {
        var build = new BuildViewModel();
        build.Load(
            new CreatureDef([new NodeDef(1, new Vector2D(0, 0), 18), new NodeDef(2, new Vector2D(100, 0), 18)], [new BeamDef(101, 1, 2)], []),
            moveOnly: true);
        build.ActiveTool = BuildTool.Select;
        build.ReplaceSelection([1, 2]);
        var gestures = new BuildGestures(build);

        gestures.SelectionHandles.Select(entry => entry.Handle).ShouldBe([SelectionHandle.Move, SelectionHandle.Rotate, SelectionHandle.Scale]);
    }

    [Fact]
    public void Select_DragInsideTheFrame_MovesTheSelection()
    {
        var (build, gestures) = ThreeLooseJoints(BuildTool.Select);
        build.ReplaceSelection([1, 2]);

        gestures.Press(new Vector2D(50, 30));
        gestures.Drag(new Vector2D(50, 60));
        gestures.Release(new Vector2D(50, 60));

        build.Nodes[0].Position.ShouldBe(new Vector2D(0, 30));
        build.Nodes[1].Position.ShouldBe(new Vector2D(100, 30));
        build.Nodes[2].Position.ShouldBe(new Vector2D(0, 100));
        build.SelectedNodeIds.OrderBy(id => id).ShouldBe([1, 2]);
    }

    [Fact]
    public void Select_RotateHandle_TurnsTheSelectionAboutItsCentre()
    {
        var (build, gestures) = ThreeLooseJoints(BuildTool.Select);
        build.ReplaceSelection([1, 2]);

        gestures.Press(_rotateHandle);
        gestures.Drag(new Vector2D(130, 0));
        gestures.Release(new Vector2D(130, 0));

        build.Nodes[0].Position.X.ShouldBe(50, 1e-9);
        build.Nodes[0].Position.Y.ShouldBe(-50, 1e-9);
        build.Nodes[1].Position.X.ShouldBe(50, 1e-9);
        build.Nodes[1].Position.Y.ShouldBe(50, 1e-9);
    }

    [Fact]
    public void Select_ScaleHandle_SpreadsTheSelectionAlongItsDiagonal()
    {
        var (build, gestures) = ThreeLooseJoints(BuildTool.Select);
        build.ReplaceSelection([1, 2]);

        gestures.Press(_scaleHandle);
        gestures.Drag(new Vector2D(50 + (2 * (_scaleHandle.X - 50)), 96));

        build.Nodes[0].Position.X.ShouldBe(-50, 1e-9);
        build.Nodes[1].Position.X.ShouldBe(150, 1e-9);
    }

    [Fact]
    public void Select_TapOnAJointUnderTheMoveHandle_StillAddsAndRemovesIt()
    {
        var (build, gestures) = ThreeLooseJoints(BuildTool.Select);
        build.PlaceNode(_moveHandle, 18);
        build.ReplaceSelection([1, 2]);

        Tap(gestures, _moveHandle);
        build.SelectedNodeIds.ShouldBe([1, 2, 4], ignoreOrder: true);

        Tap(gestures, _moveHandle);
        build.SelectedNodeIds.ShouldBe([1, 2], ignoreOrder: true);
    }

    [Fact]
    public void Select_DragFromTheMoveHandleOverAnUnselectedJoint_MovesOnlyTheSelection()
    {
        var (build, gestures) = ThreeLooseJoints(BuildTool.Select);
        build.PlaceNode(_moveHandle, 18);
        build.ReplaceSelection([1, 2]);

        gestures.Press(_moveHandle);
        gestures.Drag(new Vector2D(_moveHandle.X, 40));
        gestures.Release(new Vector2D(_moveHandle.X, 40));

        build.Nodes[0].Position.ShouldBe(new Vector2D(0, 40));
        build.Nodes[3].Position.ShouldBe(_moveHandle);
        build.SelectedNodeIds.ShouldBe([1, 2], ignoreOrder: true);
    }

    [Fact]
    public void Select_ScaleHandleDraggedPastTheCentre_NeverReflects()
    {
        var (build, gestures) = ThreeLooseJoints(BuildTool.Select);
        build.ReplaceSelection([1, 2]);

        gestures.Press(_scaleHandle);
        gestures.Drag(new Vector2D(-200, -200));

        build.Nodes[0].Position.X.ShouldBeLessThan(build.Nodes[1].Position.X);
    }

    [Fact]
    public void SecondFinger_PutsBackASelectionTheRotateHandleTurned()
    {
        var (build, gestures) = ThreeLooseJoints(BuildTool.Select);
        build.ReplaceSelection([1, 2]);

        gestures.Press(_rotateHandle, 0);
        gestures.Drag(new Vector2D(130, 0), 0);
        gestures.Press(new Vector2D(400, 400), 1);

        build.Nodes[0].Position.ShouldBe(new Vector2D(0, 0));
        build.Nodes[1].Position.ShouldBe(new Vector2D(100, 0));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(1)]
    [InlineData(0.01)]
    public void Select_HandlesStayFingerSized_AtAnyZoom(double zoom)
    {
        var (build, gestures) = ThreeLooseJoints(BuildTool.Select);
        gestures.View.VisibleArea = new CanvasRect(new Vector2D(0, 0), new Vector2D(1000, 500));
        gestures.View.ZoomAbout(new Vector2D(0, 0), zoom);
        build.ReplaceSelection([1, 2]);
        var rotate = gestures.View.ToView(gestures.SelectionHandles.Single(entry => entry.Handle == SelectionHandle.Rotate).Position);

        var edge = new Vector2D(rotate.X + BuildGestures.HandleHitRadius - 1, rotate.Y);
        gestures.Press(edge);
        gestures.Drag(new Vector2D(edge.X + 200, edge.Y + 200));

        build.Nodes[0].Position.ShouldNotBe(new Vector2D(0, 0));
        build.SelectedNodeIds.OrderBy(id => id).ShouldBe([1, 2]);
    }

    [Fact]
    public void Move_TapOnASensorPicture_SelectsTheSensorNotTheBeam()
    {
        var (build, gestures) = BeamWithSensor(100, SensorKind.Accelerometer);

        Tap(gestures, new Vector2D(50, 9));

        build.SingleSelectedSensorId.ShouldBe(4);
        build.SingleSelectedBeamId.ShouldBeNull();
    }

    [Fact]
    public void Move_TapOnAPictureWithinAJointsReach_SelectsTheSensor()
    {
        // A short beam: the picture sits inside both joints' touch reach but off their discs.
        var (build, gestures) = BeamWithSensor(50, SensorKind.Accelerometer);

        Tap(gestures, new Vector2D(22, 0));

        build.SingleSelectedSensorId.ShouldBe(4);
    }

    [Fact]
    public void Move_TapOnAJointDisc_StillWinsOverASensor()
    {
        var (build, gestures) = BeamWithSensor(30, SensorKind.Accelerometer);

        Tap(gestures, new Vector2D(10, 0));

        build.SelectedNodeIds.ShouldBe([1]);
    }

    [Fact]
    public void Joint_TapOnASensorPicture_DoesNotSplitTheBeam()
    {
        var (build, gestures) = BeamWithSensor(100, SensorKind.Accelerometer);
        build.ActiveTool = BuildTool.Joint;
        var changes = CountChanges(build);

        Tap(gestures, new Vector2D(50, 0));

        changes().ShouldBe(0);
        build.Beams.Count.ShouldBe(1);
    }

    [Fact]
    public void SecondFinger_PutsBackASelectedSensorASelectPressReplaced()
    {
        var (build, gestures) = BeamWithSensor(100, SensorKind.Accelerometer);
        build.ActiveTool = BuildTool.Select;
        build.SelectSensor(4);

        gestures.Press(new Vector2D(300, 300), 0);
        gestures.Press(new Vector2D(400, 400), 1);

        build.SingleSelectedSensorId.ShouldBe(4);
    }

    [Fact]
    public void Camera_WhenSelected_ShowsAnAimHandleOutAlongItsAim()
    {
        var (build, gestures) = BeamWithSensor(300, SensorKind.Camera);

        Tap(gestures, new Vector2D(150, 0));

        var handle = gestures.SelectionHandles.ShouldHaveSingleItem();
        handle.Handle.ShouldBe(SelectionHandle.Aim);
        var reach = (SensorPicture.CameraSize / Math.Sqrt(2)) + 8 + BuildGestures.HandleHitRadius;
        handle.Position.X.ShouldBe(150 + reach, 1e-9);
        handle.Position.Y.ShouldBe(0, 1e-9);
    }

    [Fact]
    public void Camera_AimHandle_StaysJustPastThePictureEvenOverAJoint()
    {
        var (build, gestures) = BeamWithSensor(100, SensorKind.Camera);
        build.SelectSensor(4);
        build.SetCameraAim(4, 0);

        var handle = gestures.SelectionHandles.Single().Position;

        var reach = (SensorPicture.CameraSize / Math.Sqrt(2)) + 8 + BuildGestures.HandleHitRadius;
        handle.X.ShouldBe(50 + reach, 1e-9);
        handle.Y.ShouldBe(0, 1e-9);
    }

    [Fact]
    public void Camera_AimHandle_FollowsTheZoomSmoothly()
    {
        var (build, gestures) = BeamWithSensor(100, SensorKind.Camera);
        build.SelectSensor(4);
        build.SetCameraAim(4, 0);
        var pictureReach = SensorPicture.CameraSize / Math.Sqrt(2);
        var gaps = 8 + BuildGestures.HandleHitRadius;

        for (var step = 0; step < 10; step++)
        {
            gestures.View.ZoomAbout(new Vector2D(0, 0), 1.1);
            var zoom = gestures.View.Zoom;

            // In canvas units: the picture part stays put, the on-screen gaps shrink as the view zooms in.
            gestures.SelectionHandles.Single().Position.X.ShouldBe(50 + pictureReach + (gaps / zoom), 1e-9);
        }
    }

    [Fact]
    public void Accelerometer_WhenSelected_HasNoAimHandle()
    {
        var (build, gestures) = BeamWithSensor(100, SensorKind.Accelerometer);

        build.SelectSensor(4);

        gestures.SelectionHandles.ShouldBeEmpty();
    }

    [Fact]
    public void Camera_WhenLocked_StillHasItsAimHandle()
    {
        var build = new BuildViewModel();
        build.Load(
            new CreatureDef([new NodeDef(1, new Vector2D(0, 0), 18), new NodeDef(2, new Vector2D(100, 0), 18)], [new BeamDef(3, 1, 2)], [new SensorDef(4, 3, SensorKind.Camera)]),
            moveOnly: true);
        var gestures = new BuildGestures(build);

        build.SelectSensor(4);

        build.AimableCameraId.ShouldBe(4);
        gestures.SelectionHandles.Select(handle => handle.Handle).ShouldBe([SelectionHandle.Aim]);
    }

    [Theory]
    [InlineData(BuildTool.Move)]
    [InlineData(BuildTool.Joint)]
    [InlineData(BuildTool.Beam)]
    [InlineData(BuildTool.Select)]
    public void Camera_DraggingTheAimHandle_TurnsItSmoothlyInAnyTool(BuildTool tool)
    {
        var (build, gestures) = BeamWithSensor(100, SensorKind.Camera);
        build.SelectSensor(4);
        build.ActiveTool = tool;
        var handle = gestures.SelectionHandles.Single().Position;

        gestures.Press(handle);
        gestures.Drag(new Vector2D(50 + 100, -5));
        gestures.Release(new Vector2D(50 + 100, -5));

        build.Sensors[0].Aim!.Value.ShouldBe(Math.Atan2(-5, 100), 1e-9);
        build.SingleSelectedSensorId.ShouldBe(4);
        build.Nodes[0].Position.ShouldBe(new Vector2D(0, 0));
        build.Nodes.Count.ShouldBe(2);
    }

    [Fact]
    public void Camera_AimOnATurnedBeam_IsRelativeToTheBeam()
    {
        var (build, gestures) = BeamWithSensor(100, SensorKind.Camera);
        build.MoveNode(2, new Vector2D(0, 100));
        build.SelectSensor(4);
        var handle = gestures.SelectionHandles.Single().Position;

        gestures.Press(handle);
        gestures.Drag(new Vector2D(200, 50));

        build.Sensors[0].Aim!.Value.ShouldBe(-Math.PI / 2, 1e-9);
    }

    [Fact]
    public void Camera_TapOnTheAimHandle_KeepsTheCameraSelected()
    {
        var (build, gestures) = BeamWithSensor(100, SensorKind.Camera);
        build.SelectSensor(4);
        var aim = build.Sensors[0].Aim;

        Tap(gestures, gestures.SelectionHandles.Single().Position);

        build.SingleSelectedSensorId.ShouldBe(4);
        build.Sensors[0].Aim.ShouldBe(aim);
    }

    [Fact]
    public void Joint_TapOnTheAimHandle_AddsNoJoint()
    {
        var (build, gestures) = BeamWithSensor(100, SensorKind.Camera);
        build.SelectSensor(4);
        build.ActiveTool = BuildTool.Joint;

        Tap(gestures, gestures.SelectionHandles.Single().Position);

        build.Nodes.Count.ShouldBe(2);
    }

    [Fact]
    public void SecondFinger_PutsBackTheAimAnAimDragTurned()
    {
        var (build, gestures) = BeamWithSensor(100, SensorKind.Camera);
        build.SelectSensor(4);
        var aim = build.Sensors[0].Aim;
        gestures.Press(gestures.SelectionHandles.Single().Position, 0);
        gestures.Drag(new Vector2D(0, -100), 0);
        build.Sensors[0].Aim.ShouldNotBe(aim);

        gestures.Press(new Vector2D(400, 400), 1);

        build.Sensors[0].Aim.ShouldBe(aim);
    }

    private static (BuildViewModel Build, BuildGestures Gestures) BeamWithSensor(double length, SensorKind kind = SensorKind.Accelerometer)
    {
        var builder = new CreatureBuilder();
        builder.AddNode(new Vector2D(0, 0), 18);
        builder.AddNode(new Vector2D(length, 0), 18);
        var beam = builder.AddBeam(1, 2);
        builder.AddSensor(beam, kind, out _, out _);

        var build = new BuildViewModel(builder);
        return (build, new BuildGestures(build));
    }

    private static (BuildViewModel Build, BuildGestures Gestures) TwoJointsAndABeam()
    {
        var build = new BuildViewModel();
        build.PlaceNode(new Vector2D(0, 0), 18);
        build.PlaceNode(new Vector2D(100, 0), 18);
        build.ConnectBeam(1, 2);
        return (build, new BuildGestures(build));
    }

    private static (BuildViewModel Build, BuildGestures Gestures) ThreeLooseJoints(BuildTool tool)
    {
        var build = new BuildViewModel();
        build.PlaceNode(new Vector2D(0, 0), 18);
        build.PlaceNode(new Vector2D(100, 0), 18);
        build.PlaceNode(new Vector2D(0, 100), 18);
        build.ActiveTool = tool;
        return (build, new BuildGestures(build));
    }

    private static void Tap(BuildGestures gestures, Vector2D position)
    {
        gestures.Press(position);
        gestures.Release(position);
    }

    private static Func<int> CountChanges(BuildViewModel build)
    {
        var changes = 0;
        build.AnatomyChanged += (_, _) => changes++;
        return () => changes;
    }
}
