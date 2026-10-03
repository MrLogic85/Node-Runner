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

        Tap(gestures, gestures.View.ToView(new Vector2D(-(NodeDef.PlainJointRadius - 2), 0)));
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
        build.ReplaceSelection([1]);

        gestures.Press(new Vector2D(100, 0), 0);
        gestures.Drag(new Vector2D(140, 40), 0);
        build.SelectedNodeIds.ShouldBe([2]);
        gestures.Press(new Vector2D(400, 400), 1);

        build.SelectedNodeIds.ShouldBe([1]);
    }

    [Fact]
    public void SecondFinger_PutsBackTheBeamSensorAndPistonAJointDragReplaced()
    {
        var (build, gestures) = CarriedParts();
        build.ActiveTool = BuildTool.Select;
        build.ReplaceSelection(PartSet.None with
        {
            Beams = new HashSet<int> { 5 },
            Sensors = new HashSet<int> { 6 },
            Pistons = new HashSet<int> { 7 },
        });

        gestures.Press(new Vector2D(400, 0), 0);
        gestures.Drag(new Vector2D(440, 40), 0);
        ShouldSelect(build, nodes: [4]);
        gestures.Press(new Vector2D(400, 400), 1);

        ShouldSelect(build, beams: [5], sensors: [6], pistons: [7]);
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

        // Joints at x 0 and 100, radius 15: arcs span -30..130 = 160 units into 60% of 100.
        gestures.View.Zoom.ShouldBe(60.0 / 160, 1e-9);
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
            new CreatureDef([new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(100, 0)), new NodeDef(3, new Vector2D(0, 100))], [new BeamDef(101, 1, 2), new BeamDef(102, 1, 3)], []),
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
            new CreatureDef([new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(100, 0))], [new BeamDef(101, 1, 2)], []),
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
    private static readonly double _frameRight = 100 + (NodeDef.PlainJointRadius * BuildGestures.SelectedHaloScale) + 8;
    private static readonly Vector2D _moveHandle = new(50, 0);
    private static readonly Vector2D _rotateHandle = new(50, -80);
    private static readonly Vector2D _scaleHandle = new(_frameRight, 48);

    [Fact]
    public void Select_TapsOnTwoJoints_SelectBoth()
    {
        var (build, gestures) = ThreeLooseJoints(BuildTool.Select);

        Tap(gestures, new Vector2D(0, 0));
        Tap(gestures, new Vector2D(0, 100));

        build.SelectedNodeIds.ShouldBe([1, 3], ignoreOrder: true);
    }

    [Fact]
    public void Select_PressOnAnUnselectedJoint_ChangesNothingUntilItDrags()
    {
        var (build, gestures) = ThreeLooseJoints(BuildTool.Select);
        build.ReplaceSelection([1]);

        gestures.Press(new Vector2D(0, 100));

        build.SelectedNodeIds.ShouldBe([1]);
    }

    [Fact]
    public void Select_TapOnAnUnselectedJoint_InAGroup_AddsIt()
    {
        var (build, gestures) = ThreeLooseJoints(BuildTool.Select);
        build.ReplaceSelection([1, 2]);

        Tap(gestures, new Vector2D(0, 100));

        build.SelectedNodeIds.OrderBy(id => id).ShouldBe([1, 2, 3]);
    }

    [Fact]
    public void Select_DragFromAnotherJoint_WithOneSelected_MovesOnlyThatJoint()
    {
        var (build, gestures) = ThreeLooseJoints(BuildTool.Select);
        build.ReplaceSelection([1]);

        gestures.Press(new Vector2D(0, 100));
        gestures.Drag(new Vector2D(40, 140));
        gestures.Release(new Vector2D(40, 140));

        build.SelectedNodeIds.ShouldBe([3]);
        build.Nodes[2].Position.ShouldBe(new Vector2D(40, 140));
        build.Nodes[0].Position.ShouldBe(new Vector2D(0, 0));
    }

    [Theory]
    [InlineData(300, 300)]
    [InlineData(0, 100)]
    public void Select_DragOutsideAGroup_PansAndKeepsIt(double x, double y)
    {
        var (build, gestures) = ThreeLooseJoints(BuildTool.Select);
        build.ReplaceSelection([1, 2]);
        var pan = gestures.View.Offset;

        gestures.Press(new Vector2D(x, y));
        gestures.Drag(new Vector2D(x + 30, y + 20));
        gestures.Release(new Vector2D(x + 30, y + 20));

        build.SelectedNodeIds.OrderBy(id => id).ShouldBe([1, 2]);
        build.Nodes.Select(node => node.Position).ShouldBe([new Vector2D(0, 0), new Vector2D(100, 0), new Vector2D(0, 100)]);
        gestures.View.Offset.ShouldNotBe(pan);
    }

    // CarriedParts: beam 5 (with sensor 6 at 100,0) joins 1–2, Piston 7 joins 2–3 (mid 100,75), beam 8 joins 2–4 (mid 300,0).
    [Theory]
    [InlineData(300, 0)]
    [InlineData(100, 0)]
    [InlineData(100, 75)]
    public void Select_DragFromAPartOutsideAGroup_Pans(double x, double y)
    {
        var (build, gestures) = CarriedParts();
        build.ReplaceSelection([1, 3]);
        var positions = build.Nodes.Select(node => node.Position).ToList();
        var pan = gestures.View.Offset;

        gestures.Press(new Vector2D(x, y));
        gestures.Drag(new Vector2D(x + 30, y + 20));
        gestures.Release(new Vector2D(x + 30, y + 20));

        build.SelectedNodeIds.OrderBy(id => id).ShouldBe([1, 3]);
        build.Nodes.Select(node => node.Position).ShouldBe(positions);
        gestures.View.Offset.ShouldNotBe(pan);
    }

    [Theory]
    [InlineData(40, 0)]
    [InlineData(100, 0)]
    [InlineData(100, 75)]
    public void Select_DragFromAPartInsideAGroup_MovesIt(double x, double y)
    {
        var (build, gestures) = CarriedParts();
        build.ReplaceSelection([1, 2, 3]);
        var pan = gestures.View.Offset;

        gestures.Press(new Vector2D(x, y));
        gestures.Drag(new Vector2D(x + 30, y + 20));
        gestures.Release(new Vector2D(x + 30, y + 20));

        build.Nodes.Select(node => node.Position).ShouldBe(
            [new Vector2D(30, 20), new Vector2D(230, 20), new Vector2D(30, 170), new Vector2D(400, 0)]);
        gestures.View.Offset.ShouldBe(pan);
    }

    [Fact]
    public void Select_ABoxFromASensor_CatchesEachPartWhoseCentreIsIn()
    {
        var (build, gestures) = CarriedParts();

        gestures.Press(new Vector2D(100, 0));
        gestures.Drag(new Vector2D(230, -30));
        gestures.SelectionBox.ShouldNotBeNull();
        gestures.Release(new Vector2D(230, -30));

        // Joint 2, and beam 5 and its sensor 6, whose centre is the box's corner; not beam 8 or Piston 7.
        ShouldSelect(build, nodes: [2], beams: [5], sensors: [6]);
    }

    [Fact]
    public void Select_ABoxFromAPiston_CatchesItByItsMidpoint()
    {
        var (build, gestures) = CarriedParts();

        gestures.Press(new Vector2D(100, 75));
        gestures.Drag(new Vector2D(-30, 160));
        gestures.Release(new Vector2D(-30, 160));

        ShouldSelect(build, nodes: [3], pistons: [7]);
    }

    [Fact]
    public void Select_ABoxRoundABeamsMiddle_SelectsOnlyTheBeam()
    {
        var (build, gestures) = CarriedParts();

        gestures.Press(new Vector2D(260, -30));
        gestures.Drag(new Vector2D(340, 30));
        gestures.Release(new Vector2D(340, 30));

        ShouldSelect(build, beams: [8]);
        gestures.SelectionHandles.ShouldBeEmpty();
    }

    [Fact]
    public void SecondFinger_DuringABox_PutsBackTheSelectionAndDropsTheBox()
    {
        var (build, gestures) = ThreeLooseJoints(BuildTool.Select);
        build.ReplaceSelection([1]);

        gestures.Press(new Vector2D(-30, 70), 0);
        gestures.Drag(new Vector2D(30, 130), 0);
        build.SelectedPartCount.ShouldBe(0);
        gestures.Press(new Vector2D(400, 400), 1);
        gestures.Release(new Vector2D(30, 130), 0);
        gestures.Release(new Vector2D(400, 400), 1);

        build.SelectedNodeIds.ShouldBe([1]);
        gestures.SelectionBox.ShouldBeNull();
    }

    [Fact]
    public void Select_WhenLocked_TapsAddAndDragsOutsideTheGroupPan()
    {
        var build = new BuildViewModel();
        build.Load(
            new CreatureDef(
                [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(100, 0)), new NodeDef(3, new Vector2D(0, 100))],
                [new BeamDef(101, 1, 2)],
                []),
            moveOnly: true);
        build.ActiveTool = BuildTool.Select;
        build.ReplaceSelection([1, 3]);
        var gestures = new BuildGestures(build);
        var pan = gestures.View.Offset;

        Tap(gestures, new Vector2D(100, 0));
        build.SelectedNodeIds.OrderBy(id => id).ShouldBe([1, 2, 3]);

        gestures.Press(_empty);
        gestures.Drag(new Vector2D(330, 320));
        gestures.Release(new Vector2D(330, 320));
        gestures.View.Offset.ShouldNotBe(pan);
    }

    [Fact]
    public void Select_BoxCatchingOneJoint_SelectsOnlyIt()
    {
        var (build, gestures) = ThreeLooseJoints(BuildTool.Select);
        build.ReplaceSelection([2]);

        gestures.Press(new Vector2D(-30, 70));
        gestures.Drag(new Vector2D(30, 130));
        gestures.Release(new Vector2D(30, 130));

        build.SelectedNodeIds.ShouldBe([3]);
    }

    [Fact]
    public void Select_BoxCatchingNoJoint_SelectsNothing()
    {
        var (build, gestures) = ThreeLooseJoints(BuildTool.Select);
        build.ReplaceSelection([2]);

        gestures.Press(new Vector2D(200, 200));
        gestures.Drag(new Vector2D(260, 260));
        gestures.Release(new Vector2D(260, 260));

        build.SelectedPartCount.ShouldBe(0);
    }

    [Theory]
    [InlineData(100, 0, CreatureElementKind.Sensor, 6)]
    [InlineData(100, 75, CreatureElementKind.Piston, 7)]
    [InlineData(40, 0, CreatureElementKind.Beam, 5)]
    public void Select_TapOnAPartInsideAGroup_AddsAndRemovesIt(double x, double y, CreatureElementKind kind, int id)
    {
        var (build, gestures) = CarriedParts();
        build.ReplaceSelection([1, 2, 3]);

        Tap(gestures, new Vector2D(x, y));

        build.Selection.SetOf(kind).ShouldBe([id]);
        build.SelectedNodeIds.ShouldBe([1, 2, 3], ignoreOrder: true);
        build.SelectedPartCount.ShouldBe(4);

        Tap(gestures, new Vector2D(x, y));
        ShouldSelect(build, nodes: [1, 2, 3]);
    }

    [Theory]
    [InlineData(300, 0, CreatureElementKind.Beam, 8)]
    [InlineData(100, 0, CreatureElementKind.Sensor, 6)]
    [InlineData(100, 75, CreatureElementKind.Piston, 7)]
    public void Select_TapOnAPartOutsideAGroup_AddsIt(double x, double y, CreatureElementKind kind, int id)
    {
        var (build, gestures) = CarriedParts();
        build.ReplaceSelection([1, 3]);

        Tap(gestures, new Vector2D(x, y));

        build.Selection.SetOf(kind).ShouldBe([id]);
        build.SelectedNodeIds.ShouldBe([1, 3], ignoreOrder: true);
        build.SelectedPartCount.ShouldBe(3);
    }

    [Fact]
    public void Select_TapOnAPiston_WithNoGroup_SelectsIt()
    {
        var build = new BuildViewModel();
        build.PlaceNode(new Vector2D(0, 0));
        build.PlaceNode(new Vector2D(200, 0));
        var piston = build.ConnectPiston(1, 2);
        var gestures = new BuildGestures(build);
        build.ActiveTool = BuildTool.Select;

        Tap(gestures, new Vector2D(100, 0));

        build.SingleSelectedPistonId.ShouldBe(piston);
    }

    [Fact]
    public void Select_TapOnEmptyCanvas_InAGroup_ClearsIt()
    {
        var (build, gestures) = ThreeLooseJoints(BuildTool.Select);
        build.ReplaceSelection([1, 2]);

        Tap(gestures, _empty);

        build.SelectedPartCount.ShouldBe(0);
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
    public void Select_TapOnTheOneSelectedJoint_ClearsIt()
    {
        var (build, gestures) = ThreeLooseJoints(BuildTool.Select);
        build.ReplaceSelection([1]);

        Tap(gestures, new Vector2D(0, 0));

        build.SelectedPartCount.ShouldBe(0);
    }

    [Fact]
    public void Select_TapOnABeam_WithAJointSelected_AddsIt()
    {
        var (build, gestures) = TwoJointsAndABeam();
        build.ActiveTool = BuildTool.Select;
        build.ReplaceSelection([1]);

        Tap(gestures, new Vector2D(50, 0));

        build.Selection.Beams.ShouldBe([build.Beams[0].Id]);
        build.SelectedNodeIds.ShouldBe([1]);
    }

    [Fact]
    public void Select_TapOnASensor_WithNoGroup_SelectsIt()
    {
        var (build, gestures) = BeamWithSensor(200);
        build.ActiveTool = BuildTool.Select;

        Tap(gestures, new Vector2D(100, 0));

        build.SingleSelectedSensorId.ShouldBe(build.Sensors[0].Id);
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
            new CreatureDef([new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(100, 0))], [new BeamDef(101, 1, 2)], []),
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
        build.PlaceNode(_moveHandle);
        build.ReplaceSelection([1, 2]);

        Tap(gestures, _moveHandle);
        build.SelectedNodeIds.ShouldBe([1, 2, 4], ignoreOrder: true);

        Tap(gestures, _moveHandle);
        build.SelectedNodeIds.ShouldBe([1, 2], ignoreOrder: true);
    }

    [Fact]
    public void Select_TapOnAHandleOverEmptyCanvas_KeepsTheGroup()
    {
        var (build, gestures) = ThreeLooseJoints(BuildTool.Select);
        build.ReplaceSelection([1, 2]);

        Tap(gestures, _rotateHandle);

        ShouldSelect(build, nodes: [1, 2]);
    }

    [Fact]
    public void Select_TapOnABeamUnderTheMoveHandle_AddsIt()
    {
        var (build, gestures) = ThreeLooseJoints(BuildTool.Select);
        build.ConnectBeam(1, 2);
        build.ReplaceSelection([1, 2]);

        Tap(gestures, _moveHandle);

        build.Selection.Beams.ShouldBe([build.Beams[0].Id]);
    }

    [Fact]
    public void Select_DragFromTheMoveHandleOverAnUnselectedJoint_MovesOnlyTheSelection()
    {
        var (build, gestures) = ThreeLooseJoints(BuildTool.Select);
        build.PlaceNode(_moveHandle);
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
    public void Select_ARefusedTurn_LeavesTheFrameWhereItWas()
    {
        var build = new BuildViewModel();
        build.PlaceNode(new Vector2D(BuildViewModel.BuildArea.Max.X - 18, -100));
        build.PlaceNode(new Vector2D(BuildViewModel.BuildArea.Max.X - 18, 100));
        build.ActiveTool = BuildTool.Select;
        var gestures = new BuildGestures(build);
        build.ReplaceSelection([1, 2]);
        var rotate = Handle(gestures, SelectionHandle.Rotate);

        gestures.Press(rotate);
        gestures.Drag(new Vector2D(rotate.X - 200, 0));

        gestures.SelectionFrameAngle.ShouldBe(0);
        Handle(gestures, SelectionHandle.Rotate).ShouldBe(rotate);
    }

    [Fact]
    public void Select_DraggingRotate_TurnsTheFrameWithTheGroupAndKeepsItTurned()
    {
        var (build, gestures) = ThreeLooseJoints(BuildTool.Select);
        build.ReplaceSelection([1, 2]);

        gestures.Press(_rotateHandle);
        gestures.Drag(new Vector2D(130, 0));

        gestures.SelectionFrameAngle.ShouldBe(Math.PI / 2, 1e-9);
        gestures.SelectionFrame.ShouldNotBeNull().Width.ShouldBe(2 * _frameRight - 100, 1e-9);
        Handle(gestures, SelectionHandle.Rotate).X.ShouldBe(130, 1e-9);
        Handle(gestures, SelectionHandle.Rotate).Y.ShouldBe(0, 1e-9);
        Handle(gestures, SelectionHandle.Move).ShouldBe(_moveHandle);

        gestures.Release(new Vector2D(130, 0));

        gestures.SelectionFrameAngle.ShouldBe(Math.PI / 2, 1e-9);
        gestures.SelectionFrame.ShouldNotBeNull().Width.ShouldBe(2 * _frameRight - 100, 1e-9);
        Handle(gestures, SelectionHandle.Rotate).X.ShouldBe(130, 1e-9);
        Handle(gestures, SelectionHandle.Rotate).Y.ShouldBe(0, 1e-9);
    }

    [Fact]
    public void Select_ScalingATurnedFrame_KeepsItsCentreUnderTheMoveHandle()
    {
        // Turned by π/4 the triangle's frame centre stays at (50, 50), while the middle of
        // its joints' bounds moves to about (50, 15): only the frame centre keeps the frame on the group.
        var (build, gestures) = ThreeLooseJoints(BuildTool.Select);
        build.ReplaceSelection([1, 2, 3]);
        var centre = Handle(gestures, SelectionHandle.Move);
        var reach = centre.Y - Handle(gestures, SelectionHandle.Rotate).Y;
        var turned = new Vector2D(centre.X + (reach / Math.Sqrt(2)), centre.Y - (reach / Math.Sqrt(2)));
        gestures.Press(Handle(gestures, SelectionHandle.Rotate));
        gestures.Drag(turned);
        gestures.Release(turned);
        Math.Abs(gestures.SelectionFrameAngle).ShouldBe(Math.PI / 4, 1e-9);
        var width = gestures.SelectionFrame.ShouldNotBeNull().Width;

        var scale = Handle(gestures, SelectionHandle.Scale);
        gestures.Press(scale);
        gestures.Drag(new Vector2D(centre.X + ((scale.X - centre.X) * 1.5), centre.Y + ((scale.Y - centre.Y) * 1.5)));

        gestures.SelectionFrame.ShouldNotBeNull().Width.ShouldBeGreaterThan(width);
        Handle(gestures, SelectionHandle.Move).X.ShouldBe(centre.X, 1e-9);
        Handle(gestures, SelectionHandle.Move).Y.ShouldBe(centre.Y, 1e-9);
    }

    [Fact]
    public void Select_ATurnedFrame_FollowsTheGroupWhenItMoves()
    {
        var (build, gestures) = ThreeLooseJoints(BuildTool.Select);
        build.ReplaceSelection([1, 2]);
        gestures.Press(_rotateHandle);
        gestures.Drag(new Vector2D(130, 0));
        gestures.Release(new Vector2D(130, 0));

        // Inside the turned frame, though outside where the upright one was.
        gestures.Press(new Vector2D(95, 70));
        gestures.Drag(new Vector2D(125, 70));
        gestures.Release(new Vector2D(125, 70));

        build.Nodes[0].Position.X.ShouldBe(80, 1e-9);
        gestures.SelectionFrameAngle.ShouldBe(Math.PI / 2, 1e-9);
        Handle(gestures, SelectionHandle.Rotate).X.ShouldBe(160, 1e-9);
        Handle(gestures, SelectionHandle.Rotate).Y.ShouldBe(0, 1e-9);
    }

    [Fact]
    public void Select_ANewSelection_GetsAnUprightFrame()
    {
        var (build, gestures) = ThreeLooseJoints(BuildTool.Select);
        build.ReplaceSelection([1, 2]);
        gestures.Press(_rotateHandle);
        gestures.Drag(new Vector2D(130, 0));
        gestures.Release(new Vector2D(130, 0));

        build.ReplaceSelection([1, 2]);

        gestures.SelectionFrameAngle.ShouldBe(0);
        Handle(gestures, SelectionHandle.Move).ShouldBe(_moveHandle);
        Handle(gestures, SelectionHandle.Rotate).X.ShouldBe(_moveHandle.X, 1e-9);
    }

    [Fact]
    public void Select_WithNoRoomAboveTheFrame_KeepsRotateAboveIt()
    {
        var (build, gestures) = ThreeLooseJoints(BuildTool.Select);
        gestures.View.VisibleArea = new CanvasRect(new Vector2D(-300, -60), new Vector2D(300, 300));

        build.ReplaceSelection([1, 2]);

        Handle(gestures, SelectionHandle.Rotate).ShouldBe(_rotateHandle);
        gestures.RotateStem.ShouldBe((new Vector2D(50, -48), _rotateHandle));
        gestures.FrameCornerSquares.ShouldBe(
            [new Vector2D(100 - _frameRight, -48), new Vector2D(_frameRight, -48), new Vector2D(100 - _frameRight, 48)]);
    }

    [Fact]
    public void Select_DraggingABox_PreviewsTheJointsItWouldCatch()
    {
        var (build, gestures) = ThreeLooseJoints(BuildTool.Select);

        gestures.Press(new Vector2D(-50, -50));
        gestures.Drag(new Vector2D(150, 50));

        gestures.SelectionBoxCatches.Nodes.ShouldBe([1, 2], ignoreOrder: true);
        build.SelectedNodeIds.ShouldBeEmpty();

        gestures.Release(new Vector2D(150, 50));

        gestures.SelectionBoxCatches.Count.ShouldBe(0);
        build.SelectedNodeIds.ShouldBe([1, 2], ignoreOrder: true);
    }

    private static void ShouldSelect(BuildViewModel build, int[]? nodes = null, int[]? beams = null, int[]? sensors = null, int[]? pistons = null)
    {
        var selection = build.Selection;
        selection.Nodes.ShouldBe(nodes ?? [], ignoreOrder: true);
        selection.Beams.ShouldBe(beams ?? [], ignoreOrder: true);
        selection.Sensors.ShouldBe(sensors ?? [], ignoreOrder: true);
        selection.Pistons.ShouldBe(pistons ?? [], ignoreOrder: true);
    }

    private static Vector2D Handle(BuildGestures gestures, SelectionHandle handle) =>
        gestures.SelectionHandles.Single(entry => entry.Handle == handle).Position;

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
        // A short beam: the picture sits inside both joints' touch reach but off their rings.
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
        var reach = 2 * ((SensorPicture.CameraSize / Math.Sqrt(2)) + 8 + BuildGestures.HandleHitRadius);
        handle.Position.X.ShouldBe(150 + reach, 1e-9);
        handle.Position.Y.ShouldBe(0, 1e-9);
    }

    [Fact]
    public void Camera_AimHandle_StaysJustPastThePictureEvenOverAJoint()
    {
        var (build, gestures) = BeamWithSensor(100, SensorKind.Camera);
        build.SelectSensor(4);
        build.SetParameter(PartParameterId.Aim, 0);

        var handle = gestures.SelectionHandles.Single().Position;

        var reach = 2 * ((SensorPicture.CameraSize / Math.Sqrt(2)) + 8 + BuildGestures.HandleHitRadius);
        handle.X.ShouldBe(50 + reach, 1e-9);
        handle.Y.ShouldBe(0, 1e-9);
    }

    [Fact]
    public void Camera_AimHandle_FollowsTheZoomSmoothly()
    {
        var (build, gestures) = BeamWithSensor(100, SensorKind.Camera);
        build.SelectSensor(4);
        build.SetParameter(PartParameterId.Aim, 0);
        var pictureReach = SensorPicture.CameraSize / Math.Sqrt(2);
        var gaps = 8 + BuildGestures.HandleHitRadius;

        for (var step = 0; step < 10; step++)
        {
            gestures.View.ZoomAbout(new Vector2D(0, 0), 1.1);
            var zoom = gestures.View.Zoom;

            // In canvas units: the picture part stays put, the on-screen gaps shrink as the view zooms in.
            gestures.SelectionHandles.Single().Position.X.ShouldBe(50 + (2 * (pictureReach + (gaps / zoom))), 1e-9);
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
            new CreatureDef([new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(100, 0))], [new BeamDef(3, 1, 2)], [new SensorDef(4, 3, SensorKind.Camera)]),
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
        builder.AddNode(new Vector2D(0, 0));
        builder.AddNode(new Vector2D(length, 0));
        var beam = builder.AddBeam(1, 2);
        builder.AddSensor(beam, kind, out _, out _);

        var build = new BuildViewModel(builder);
        return (build, new BuildGestures(build));
    }

    private static (BuildViewModel Build, BuildGestures Gestures) CarriedParts()
    {
        var builder = new CreatureBuilder();
        builder.AddNode(new Vector2D(0, 0));
        builder.AddNode(new Vector2D(200, 0));
        builder.AddNode(new Vector2D(0, 150));
        builder.AddNode(new Vector2D(400, 0));
        builder.AddSensor(builder.AddBeam(1, 2), SensorKind.Accelerometer, out _, out _);
        builder.AddPiston(2, 3);
        builder.AddBeam(2, 4);

        var build = new BuildViewModel(builder) { ActiveTool = BuildTool.Select };
        return (build, new BuildGestures(build));
    }

    private static (BuildViewModel Build, BuildGestures Gestures) TwoJointsAndABeam()
    {
        var build = new BuildViewModel();
        build.PlaceNode(new Vector2D(0, 0));
        build.PlaceNode(new Vector2D(100, 0));
        build.ConnectBeam(1, 2);
        return (build, new BuildGestures(build));
    }

    private static (BuildViewModel Build, BuildGestures Gestures) ThreeLooseJoints(BuildTool tool)
    {
        var build = new BuildViewModel();
        build.PlaceNode(new Vector2D(0, 0));
        build.PlaceNode(new Vector2D(100, 0));
        build.PlaceNode(new Vector2D(0, 100));
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
