using NodeRunner.App.Builders;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;
using static NodeRunner.App.Tests.ViewModels.BuildFixtures;

namespace NodeRunner.App.Tests.ViewModels;

public class BuildGesturesTests
{
    private static readonly Vector2D _empty = new(300, 200);

    // With joints 1 and 2 of ThreeLooseJoints selected at 1×, the frame clears both halos by 8
    // across and meets the 96 minimum down: Move sits at (50, 0), Rotate at (50, -80), Scale at its corner.
    private static readonly double _frameRight = 100 + SelectionMarks.JointHalo(NodeDef.PlainJointRadius) + 8;
    private static readonly Vector2D _moveHandle = new(50, 0);
    private static readonly Vector2D _rotateHandle = new(50, -80);
    private static readonly Vector2D _scaleHandle = new(_frameRight, 48);

    [Theory]
    [InlineData(BuildTool.Parts)]
    [InlineData(BuildTool.Beam)]
    [InlineData(BuildTool.Joint)]
    [InlineData(BuildTool.Select)]
    public void AnyTool_TapsTogglePartsInTheSelection_AndATapOnEmptyCanvasClearsIt(BuildTool tool)
    {
        var (build, gestures) = TwoJointsAndABeam();
        build.ActiveTool = tool;
        build.ReplaceSelection([1]);

        gestures.Tap(new Vector2D(102, 1));
        build.SelectedNodeIds.ShouldBe([1, 2], ignoreOrder: true);
        gestures.Tap(new Vector2D(102, 1));
        build.SelectedNodeIds.ShouldBe([1]);
        gestures.Tap(new Vector2D(50, 6));
        ShouldSelect(build, nodes: [1], beams: [3]);
        gestures.Tap(new Vector2D(0, 0));
        ShouldSelect(build, beams: [3]);
        gestures.Tap(_empty);

        build.SelectedPartCount.ShouldBe(0);
        build.Nodes.Count.ShouldBe(2);
        build.Beams.Count.ShouldBe(1);
        if (tool is BuildTool.Parts or BuildTool.Beam)
        {
            var changes = build.CountAnatomyChanges();
            var redraws = 0;
            gestures.View.Changed += (_, _) => redraws++;

            gestures.DragAcross(new Vector2D(50, 0), new Vector2D(50, 60));

            changes().ShouldBe(0);
            redraws.ShouldBeGreaterThan(0);
            gestures.View.Offset.ShouldBe(new Vector2D(0, 60));
        }
    }

    [Theory]
    [InlineData(BuildTool.Parts)]
    [InlineData(BuildTool.Beam)]
    [InlineData(BuildTool.Joint)]
    [InlineData(BuildTool.Select)]
    public void AnyTool_TheFrameAndItsHandles_MoveTurnAndScaleTheSelection(BuildTool tool)
    {
        var (build, gestures) = ThreeLooseJoints(tool);
        build.ReplaceSelection([1, 2]);
        gestures.SelectionHandles.Select(entry => entry.Handle).ShouldBe([SelectionHandle.Move, SelectionHandle.Rotate, SelectionHandle.Scale]);
        var pan = gestures.View.Offset;

        gestures.DragAcross(new Vector2D(50, 30), new Vector2D(50, 60));
        Positions(build).ShouldBe([new(0, 30), new(100, 30), new(0, 100)]);
        gestures.DragAcross(new Vector2D(0, 30), new Vector2D(0, 0));
        Positions(build).ShouldBe([new(0, 0), new(100, 0), new(0, 100)]);
        build.SelectedNodeIds.ShouldBe([1, 2], ignoreOrder: true);
        gestures.View.Offset.ShouldBe(pan);

        gestures.DragAcross(_rotateHandle, new Vector2D(130, 0));
        build.Nodes[0].Position.X.ShouldBe(50, 1e-9);
        build.Nodes[0].Position.Y.ShouldBe(-50, 1e-9);
        build.Nodes[1].Position.X.ShouldBe(50, 1e-9);
        build.Nodes[1].Position.Y.ShouldBe(50, 1e-9);
        build.Undo();
        build.ReplaceSelection([1, 2]);

        gestures.DragAcross(_scaleHandle, new Vector2D(50 + (2 * (_scaleHandle.X - 50)), 96));
        build.Nodes[0].Position.X.ShouldBe(-50, 1e-9);
        build.Nodes[1].Position.X.ShouldBe(150, 1e-9);
        build.Undo();
        build.ReplaceSelection([1, 2]);

        gestures.Tap(new Vector2D(50, 40));
        build.SelectedPartCount.ShouldBe(0);
        build.Nodes.Count.ShouldBe(3);
    }

    [Theory]
    [InlineData(BuildTool.Parts)]
    [InlineData(BuildTool.Beam)]
    [InlineData(BuildTool.Joint)]
    [InlineData(BuildTool.Select)]
    public void AnyTool_ADragMovesAnUnselectedJointOrServo_ButNotWithBeams_AndOnEmptyCanvasPansOrDrawsABox(BuildTool tool)
    {
        var (build, gestures) = ThreeLooseJoints(tool);
        build.ReplaceSelection([2]);
        var moves = tool != BuildTool.Beam;

        gestures.DragAcross(new Vector2D(0, 0), new Vector2D(0, 40));

        Positions(build).ShouldBe([new(0, moves ? 40 : 0), new(100, 0), new(0, 100)]);
        build.SelectedNodeIds.ShouldBe([moves ? 1 : 2]);
        build.Beams.ShouldBeEmpty();

        build.ReplaceSelection([1, 2]);
        var (positions, pan) = (Positions(build), gestures.View.Offset);
        gestures.Press(_empty);
        gestures.Drag(new Vector2D(330, 320));
        (gestures.SelectionBox is null).ShouldBe(tool != BuildTool.Select);
        gestures.Release(new Vector2D(330, 320));

        Positions(build).ShouldBe(positions);
        build.SelectedPartCount.ShouldBe(tool == BuildTool.Select ? 0 : 2);
        (gestures.View.Offset == pan).ShouldBe(tool == BuildTool.Select);

        var (servoBuild, servoGestures, servo) = ServoOnABeamsEnd();
        servoBuild.ActiveTool = tool;
        servoGestures.DragAcross(new Vector2D(100, 0), new Vector2D(100, 40));
        Positions(servoBuild).ShouldBe([new(0, 0), new(100, moves ? 40 : 0)]);
        servoBuild.Selection.Servos.ShouldBe(moves ? [servo] : []);
    }

    [Fact]
    public void Links_AreDrawnFromAnUnselectedJoint_AsPickedWhenTheDragStarted_AndWithNonePickedADragMovesTheJoint()
    {
        var (build, gestures) = ThreeLooseJoints(BuildTool.Beam);
        build.ReplaceSelection([1]);
        gestures.LinkDrawnFrom(1).ShouldBeNull();
        gestures.LinkDrawnFrom(2).ShouldBe(BuildLink.Beam);

        gestures.Tap(_empty);
        build.SelectedPartCount.ShouldBe(0);
        build.PickedLink.ShouldBe(BuildLink.Beam);
        gestures.Press(new Vector2D(0, 0), 0);
        gestures.Drag(new Vector2D(100, 0), 0);
        build.PickLink(BuildLink.Piston);
        gestures.Release(new Vector2D(100, 0), 0);
        build.Beams.ShouldBe([new BeamDef(4, 1, 2)]);
        build.Pistons.ShouldBeEmpty();

        gestures.Tap(_empty);
        build.PickedLink.ShouldBeNull();
        gestures.LinkDrawnFrom(2).ShouldBeNull();
        gestures.DragAcross(new Vector2D(0, 100), new Vector2D(0, 140));
        gestures.Tap(new Vector2D(100, 0));
        build.Beams.Count.ShouldBe(1);
        build.Nodes[2].Position.ShouldBe(new Vector2D(0, 140));
        build.SelectedNodeIds.ShouldBe([2, 3], ignoreOrder: true);

        build.PickLink(BuildLink.Piston);
        gestures.DragAcross(new Vector2D(0, 0), new Vector2D(0, 140));
        build.Pistons.Select(piston => (piston.NodeA, piston.NodeB)).ShouldBe([(1, 3)]);
        build.SelectedNodeIds.ShouldBe([2, 3], ignoreOrder: true);

        build.PickLink(BuildLink.Spring);
        build.ActiveTool = BuildTool.Joint;
        gestures.LinkDrawnFrom(1).ShouldBeNull();
    }

    [Fact]
    public void TwoFingers_PinchToZoomAndPan_AndTheFingerLeftDownWaitsUntilAllLift()
    {
        var (build, gestures) = ThreeLooseJoints(BuildTool.Joint);
        var changes = build.CountAnatomyChanges();

        gestures.Press(new Vector2D(300, 300), 0);
        gestures.Press(new Vector2D(400, 300), 1);
        gestures.Drag(new Vector2D(250, 300), 0);
        gestures.Drag(new Vector2D(450, 300), 1);
        gestures.Release(new Vector2D(250, 300), 0);
        gestures.Release(new Vector2D(450, 300), 1);

        gestures.View.Zoom.ShouldBe(2, 1e-9);
        gestures.View.ToCanvas(new Vector2D(350, 300)).X.ShouldBe(350, 1e-9);
        gestures.View.ToCanvas(new Vector2D(350, 300)).Y.ShouldBe(300, 1e-9);
        var (zoom, offset) = (gestures.View.Zoom, gestures.View.Offset);
        gestures.Press(new Vector2D(300, 300), 0);
        gestures.Press(new Vector2D(400, 300), 1);
        gestures.Release(new Vector2D(400, 300), 1);
        gestures.Drag(new Vector2D(320, 300), 0);
        gestures.Release(new Vector2D(320, 300), 0);
        gestures.View.Zoom.ShouldBe(zoom);
        gestures.View.Offset.ShouldBe(offset);
        changes().ShouldBe(0);
        gestures.Tap(_empty);
        build.Nodes.Count.ShouldBe(4);

        var panned = new BuildGestures(build);
        build.ActiveTool = BuildTool.Beam;
        panned.Press(new Vector2D(300, 300), 0);
        panned.Press(new Vector2D(400, 300), 1);
        panned.Drag(new Vector2D(300, 340), 0);
        panned.Drag(new Vector2D(400, 340), 1);
        panned.View.Zoom.ShouldBe(1, 1e-9);
        panned.View.Offset.X.ShouldBe(0, 1e-9);
        panned.View.Offset.Y.ShouldBe(40, 1e-9);
    }

    // A move, a beam preview, a press that changed the selection, a box and a turn.
    [Theory]
    [InlineData(BuildTool.Parts, new int[0], 0, 0, 0, 40)]
    [InlineData(BuildTool.Beam, new int[0], 0, 0, 100, 0)]
    [InlineData(BuildTool.Select, new[] { 1 }, 100, 0, 140, 40)]
    [InlineData(BuildTool.Select, new[] { 1 }, -30, 70, 30, 130)]
    [InlineData(BuildTool.Select, new[] { 1, 2 }, 50, -80, 130, 0)]
    public void SecondFinger_PutsBackWhatTheFirstFingerChanged(BuildTool tool, int[] selection, double fromX, double fromY, double toX, double toY)
    {
        var (build, gestures) = ThreeLooseJoints(tool);
        build.ReplaceSelection(selection);
        var (positions, link) = (Positions(build), build.PickedLink);

        gestures.Press(new Vector2D(fromX, fromY), 0);
        gestures.Drag(new Vector2D(toX, toY), 0);
        gestures.Press(new Vector2D(400, 400), 1);

        Positions(build).ShouldBe(positions);
        build.SelectedNodeIds.ShouldBe(selection, ignoreOrder: true);
        build.PickedLink.ShouldBe(link);
        gestures.BeamStartNodeId.ShouldBeNull();
        gestures.Release(new Vector2D(toX, toY), 0);
        gestures.Release(new Vector2D(400, 400), 1);
        Positions(build).ShouldBe(positions);
        build.Beams.ShouldBeEmpty();
        gestures.SelectionBox.ShouldBeNull();
    }

    [Fact]
    public void SecondFinger_PutsBackAReplacedSelectionAndAnAim_AndCancelsASensorMove()
    {
        var (build, gestures) = CarriedParts();
        build.ReplaceSelection(Set(Beam(5), new(CreatureElementKind.Sensor, 6), new(CreatureElementKind.Piston, 7)));
        gestures.Press(new Vector2D(400, 0), 0);
        gestures.Drag(new Vector2D(440, 40), 0);
        ShouldSelect(build, nodes: [4]);
        gestures.Press(new Vector2D(400, 400), 1);
        ShouldSelect(build, beams: [5], sensors: [6], pistons: [7]);

        var (camera, cameraGestures) = BeamWithSensor(100, SensorKind.Camera);
        camera.ActiveTool = BuildTool.Select;
        camera.SelectOnly(CreatureElementKind.Sensor, 4);
        var aim = camera.Sensors[0].Aim;
        cameraGestures.Press(cameraGestures.SelectionHandles.Single().Position, 0);
        cameraGestures.Drag(new Vector2D(0, -100), 0);
        camera.Sensors[0].Aim.ShouldNotBe(aim);
        cameraGestures.Press(new Vector2D(400, 400), 1);
        camera.Sensors[0].Aim.ShouldBe(aim);
        cameraGestures.Release(new Vector2D(0, -100), 0);
        cameraGestures.Release(new Vector2D(400, 400), 1);
        cameraGestures.Press(new Vector2D(300, 300), 0);
        cameraGestures.Press(new Vector2D(400, 400), 1);
        camera.SingleSelectedSensorId.ShouldBe(4);

        var (sensors, sensorGestures) = SensorsOnThreeBeams();
        sensorGestures.Press(new Vector2D(100, 0), 0);
        sensorGestures.Drag(new Vector2D(100, 150), 0);
        sensorGestures.Press(new Vector2D(300, 300), 1);
        sensorGestures.Release(new Vector2D(100, 150), 0);
        sensorGestures.Release(new Vector2D(300, 300), 1);
        sensorGestures.MovingSensorId.ShouldBeNull();
        sensors.Sensors.Single(sensor => sensor.Id == 6).BeamId.ShouldBe(5);
        sensors.CanUndo.ShouldBeFalse();
    }

    [Fact]
    public void Zoomed_TapsHitWhatIsUnderTheFinger_WithAFingerSizedReach_AndJointsLandThere()
    {
        var (build, gestures) = ThreeLooseJoints(BuildTool.Parts);
        gestures.View.ZoomAbout(new Vector2D(0, 0), 2);
        gestures.View.PanBy(new Vector2D(50, 50));
        gestures.Tap(new Vector2D(250, 50));
        build.SelectedNodeIds.ShouldBe([2]);
        build.ClearSelection();
        build.ActiveTool = BuildTool.Joint;
        gestures.Tap(new Vector2D(650, 450));
        build.Nodes[^1].Position.ShouldBe(new Vector2D(300, 200));

        (build, gestures) = TwoJointsAndABeam();
        gestures.View.ZoomAbout(new Vector2D(0, 0), 0.125);
        gestures.DragAcross(new Vector2D(0, 0), new Vector2D(BuildGestures.TapSlop - 2, 0));
        build.Nodes[0].Position.ShouldBe(new Vector2D(0, 0));
        build.SelectedNodeIds.ShouldBe([1]);

        (build, gestures) = TwoJointsAndABeam();
        build.ActiveTool = BuildTool.Joint;
        gestures.View.ZoomAbout(new Vector2D(0, 0), CanvasView.MaxZoom);
        gestures.Tap(gestures.View.ToView(new Vector2D(50, 12)));
        build.Beams.Count.ShouldBe(1);
        build.Nodes.Count.ShouldBe(3);
    }

    // A joint reaches its ring plus the selection gap, and a handle stays finger-sized, at any zoom.
    [Theory]
    [InlineData(0.5)]
    [InlineData(CanvasView.MaxZoom)]
    public void Zoomed_JointsReachTheirRingPlusTheGap_AndHandlesStayFingerSized(double zoom)
    {
        var (build, gestures) = ThreeLooseJoints(BuildTool.Select);
        gestures.View.ZoomAbout(new Vector2D(0, 0), zoom);
        gestures.View.Zoom.ShouldBe(zoom);
        var reach = NodeDef.PlainJointRadius + SelectionMarks.Gap;

        gestures.Tap(gestures.View.ToView(new Vector2D(-(reach + 1), 0)));
        build.SelectedPartCount.ShouldBe(0);
        gestures.Tap(gestures.View.ToView(new Vector2D(-(reach - 1), 0)));
        build.SelectedNodeIds.ShouldBe([1]);

        gestures.View.VisibleArea = new CanvasRect(new Vector2D(0, 0), new Vector2D(1000, 500));
        build.ReplaceSelection([1, 2]);
        var rotate = gestures.View.ToView(Handle(gestures, SelectionHandle.Rotate));
        var edge = new Vector2D(rotate.X + BuildGestures.HandleHitRadius - 1, rotate.Y);
        gestures.DragAcross(edge, new Vector2D(edge.X + 200, edge.Y + 200));
        build.Nodes[0].Position.ShouldNotBe(new Vector2D(0, 0));
        build.SelectedNodeIds.ShouldBe([1, 2], ignoreOrder: true);
    }

    [Fact]
    public void TheView_FitsTheJointsAndTheirMotorArcs_KeepsTheBuildAreasMargin_AndPlacesNothingOutsideIt()
    {
        var (_, gestures) = TwoJointsAndABeam();
        gestures.View.VisibleArea = new CanvasRect(new Vector2D(0, 0), new Vector2D(1000, 500));
        gestures.View.Fit();
        gestures.View.ToView(new Vector2D(50, 0)).ShouldBe(new Vector2D(500, 250));
        gestures.View.ZoomAbout(new Vector2D(0, 0), 0.5);
        gestures.View.Fit();
        gestures.View.Zoom.ShouldBe(1);

        // Joints at x 0 and 100, radius 15: arcs span -30..130 = 160 units into 60% of 100.
        gestures.View.VisibleArea = new CanvasRect(new Vector2D(0, 0), new Vector2D(100, 100));
        gestures.View.Fit();
        gestures.View.Zoom.ShouldBe(60.0 / 160, 1e-9);

        (_, gestures) = TwoJointsAndABeam();

        // Narrower than the bounds' shape, so at the farthest zoom their sides still reach the view's.
        gestures.View.VisibleArea = new CanvasRect(new Vector2D(0, 0), new Vector2D(1000, 600));
        foreach (var factor in new[] { 1.0, 3, 0.25 })
        {
            gestures.View.ZoomAbout(new Vector2D(500, 250), factor);
            gestures.DragAcross(new Vector2D(500, 400), new Vector2D(100000, 400));

            var areaEdge = gestures.View.ToCanvas(new Vector2D(0, 0)).X + BuildViewModel.BuildViewMargin;
            areaEdge.ShouldBe(BuildViewModel.BuildArea.Min.X, 1e-6);
        }

        var build = new BuildViewModel { ActiveTool = BuildTool.Joint };
        gestures = new BuildGestures(build);
        gestures.View.ZoomAbout(new Vector2D(0, 0), 0.001);
        gestures.Tap(gestures.View.ToView(new Vector2D(BuildViewModel.BuildArea.Max.X + 50, 0)));
        build.Nodes.Count.ShouldBe(0);
    }

    [Fact]
    public void Beam_ADragFromJointToJoint_AddsOneBeam_ButTapsDropsAwayAndRefusedPairsAddNone()
    {
        var (build, gestures) = ThreeLooseJoints(BuildTool.Beam);
        gestures.Tap(new Vector2D(0, 0));
        gestures.Tap(new Vector2D(100, 0));
        build.SelectedNodeIds.ShouldBe([1, 2], ignoreOrder: true);
        build.ClearSelection();

        gestures.Press(new Vector2D(0, 0));
        gestures.Drag(_empty);
        gestures.RefusedTargetNodeId.ShouldBeNull();
        gestures.Release(_empty);
        gestures.Press(new Vector2D(0, 0));
        gestures.Drag(new Vector2D(20, 0));
        gestures.Drag(new Vector2D(1, 1));
        gestures.Release(new Vector2D(1, 1));
        build.Beams.ShouldBeEmpty();
        build.Nodes.Count.ShouldBe(3);
        build.PlacementNote.ShouldBeNull();

        gestures.Press(new Vector2D(0, 0));
        gestures.Drag(new Vector2D(60, 0));
        gestures.BeamStartNodeId.ShouldBe(1);
        gestures.BeamEnd.ShouldBe(new Vector2D(60, 0));
        gestures.BeamTargetNodeId.ShouldBeNull();
        gestures.Drag(new Vector2D(98, 2));
        gestures.BeamTargetNodeId.ShouldBe(2);
        gestures.RefusedTargetNodeId.ShouldBeNull();
        gestures.Release(new Vector2D(98, 2));
        build.Beams.ShouldBe([new BeamDef(4, 1, 2)]);
        gestures.BeamStartNodeId.ShouldBeNull();

        gestures.Press(new Vector2D(0, 0));
        gestures.Drag(new Vector2D(100, 0));
        gestures.BeamTargetNodeId.ShouldBeNull();
        gestures.RefusedTargetNodeId.ShouldBe(2);
        gestures.BeamEnd.ShouldBe(new Vector2D(100, 0));
        gestures.Release(new Vector2D(100, 0));
        build.Beams.Count.ShouldBe(1);
        build.PlacementNote.ShouldBe(new CanvasNote(CanvasNoteKind.Danger, Joint(2), CreatureBuilder.BeamJoinsTheseNodesReason));
    }

    [Fact]
    public void Beam_FromASelectedServo_StartsAtItsJoint_AndOverAPistonOutlinesItAndReplacesIt()
    {
        var build = new BuildViewModel();
        build.PlaceNode(new Vector2D(0, 0));
        build.PlaceNode(new Vector2D(100, 0));
        var servo = build.PlacePart(BuildPart.Servo, Joint(2))!.Value;
        build.ActiveTool = BuildTool.Beam;
        var gestures = new BuildGestures(build);

        gestures.DragAcross(new Vector2D(100, 0), new Vector2D(0, 0));

        build.Beams.Select(beam => (beam.NodeA, beam.NodeB)).ShouldBe([(2, 1)]);
        Positions(build).ShouldBe([new(0, 0), new(100, 0)]);
        build.SingleSelectedServoId.ShouldBe(servo);

        (build, gestures) = ThreeLooseJoints(BuildTool.Beam);
        var piston = build.ConnectLink(BuildLink.Piston, 1, 2);
        gestures.Press(new Vector2D(0, 0));
        gestures.Drag(new Vector2D(100, 0));
        gestures.BeamTargetNodeId.ShouldBe(2);
        gestures.ReplacedLinkId.ShouldBe(piston);
        gestures.Release(new Vector2D(100, 0));
        gestures.ReplacedLinkId.ShouldBeNull();
        build.Pistons.ShouldBeEmpty();
        build.Beams.Single().NodeB.ShouldBe(2);
        build.PlacementNote.ShouldBeNull();
    }

    [Fact]
    public void Locked_TheToolsStillDrawBeamsAndAddJoints()
    {
        var build = Loaded(
            new CreatureDef([new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(100, 0)), new NodeDef(3, new Vector2D(0, 100)), new BeamDef(101, 1, 2)], nextPartId: 102),
            locked: true);
        build.ActiveTool = BuildTool.Beam;
        var gestures = new BuildGestures(build);
        var pan = gestures.View.Offset;

        gestures.Press(new Vector2D(100, 0));
        gestures.BeamStartNodeId.ShouldBe(2);
        gestures.Drag(new Vector2D(0, 100));
        gestures.Release(new Vector2D(0, 100));
        build.Beams.Count.ShouldBe(2);

        build.ActiveTool = BuildTool.Joint;
        build.ClearSelection();
        gestures.Tap(_empty);
        build.Nodes.Count.ShouldBe(4);
        build.ClearSelection();
        gestures.Tap(new Vector2D(50, 0));
        build.Selection.Beams.ShouldBe([101]);
        build.Nodes.Count.ShouldBe(4);
        gestures.View.Offset.ShouldBe(pan);
    }

    [Fact]
    public void AToolChangeOrCancel_MidGesture_EndsItWithoutEditing()
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

        build.ActiveTool = BuildTool.Beam;
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

    // CarriedParts: beam 5 (with sensor 6 at 100,0) joins 1–2, Piston 7 joins 2–3 (mid 100,75), beam 8 joins 2–4 (mid 300,0).
    [Fact]
    public void Select_ABox_PreviewsAndCatchesEachPartWhoseCentreIsIn_AndAServoInPlaceOfItsJoint()
    {
        var (build, gestures) = CarriedParts();
        build.ReplaceSelection([3]);
        gestures.Press(new Vector2D(230, 30));
        gestures.Drag(new Vector2D(90, -40));
        gestures.SelectionBox.ShouldBe((new Vector2D(230, 30), new Vector2D(90, -40)));
        gestures.SelectionBoxCatches.Beams.ShouldBe([5]);
        build.SelectedPartCount.ShouldBe(0);
        gestures.Release(new Vector2D(90, -40));
        ShouldSelect(build, nodes: [2], beams: [5], sensors: [6]);
        gestures.SelectionBox.ShouldBeNull();
        gestures.SelectionBoxCatches.Count.ShouldBe(0);
        gestures.DragAcross(new Vector2D(100, 75), new Vector2D(-30, 160));
        ShouldSelect(build, nodes: [3], pistons: [7]);
        gestures.DragAcross(new Vector2D(260, -30), new Vector2D(340, 30));
        ShouldSelect(build, beams: [8]);
        gestures.SelectionHandles.ShouldBeEmpty();

        var (servoBuild, servoGestures, servo) = ServoOnABeamsEnd();
        servoGestures.DragAcross(new Vector2D(-30, -30), new Vector2D(130, 30));
        ShouldSelect(servoBuild, nodes: [1], beams: [3]);
        servoBuild.Selection.Servos.ShouldBe([servo]);
        servoBuild.SelectedNodeCount.ShouldBe(2);

        servoBuild.ReplaceSelection(Set(Joint(1), new(CreatureElementKind.Servo, servo)));
        servoGestures.DragAcross(new Vector2D(100, 0), new Vector2D(100, 30));
        Positions(servoBuild).ShouldBe([new(0, 30), new(100, 30)]);
        servoBuild.Selection.Servos.ShouldBe([servo]);
    }

    [Theory]
    [InlineData(new[] { 1, 2, 3 }, 100, 0, CreatureElementKind.Sensor, 6)]
    [InlineData(new[] { 1, 2, 3 }, 40, 0, CreatureElementKind.Beam, 5)]
    [InlineData(new[] { 1, 3 }, 100, 75, CreatureElementKind.Piston, 7)]
    [InlineData(new[] { 1, 3 }, 300, 0, CreatureElementKind.Beam, 8)]
    public void Select_ATapOnAPartInOrOutOfAGroup_AddsAndRemovesIt(int[] group, double x, double y, CreatureElementKind kind, int id)
    {
        var (build, gestures) = CarriedParts();
        build.ReplaceSelection(group);

        gestures.Tap(new Vector2D(x, y));
        build.Selection.SetOf(kind).ShouldBe([id]);
        build.SelectedNodeIds.ShouldBe(group, ignoreOrder: true);
        build.SelectedPartCount.ShouldBe(group.Length + 1);

        gestures.Tap(new Vector2D(x, y));
        ShouldSelect(build, nodes: group);
    }

    // Inside the group a drag from a part moves it; outside, Select draws a box and the other tools pan.
    // A sensor outside the group moves alone, like a joint (#806).
    [Theory]
    [InlineData(BuildTool.Select, new[] { 1, 2, 3 }, 100, 0)]
    [InlineData(BuildTool.Select, new[] { 1, 3 }, 300, 0)]
    [InlineData(BuildTool.Parts, new[] { 1, 3 }, 100, 75)]
    public void ADragFromAPartInAGroup_MovesIt_AndOutsideOneDrawsABoxOrPans(BuildTool tool, int[] group, double x, double y)
    {
        var (build, gestures) = CarriedParts();
        build.ActiveTool = tool;
        build.ReplaceSelection(group);
        var (positions, pan) = (Positions(build), gestures.View.Offset);

        gestures.DragAcross(new Vector2D(x, y), new Vector2D(x + 30, y + 20));

        if (group.Length == 3)
        {
            Positions(build).ShouldBe([new(30, 20), new(230, 20), new(30, 170), new(400, 0)]);
            gestures.View.Offset.ShouldBe(pan);
            return;
        }

        Positions(build).ShouldBe(positions);
        if (tool == BuildTool.Select)
        {
            ShouldSelect(build, beams: [8]);
            gestures.View.Offset.ShouldBe(pan);
        }
        else
        {
            ShouldSelect(build, nodes: group);
            gestures.View.Offset.ShouldNotBe(pan);
        }
    }

    [Fact]
    public void Select_TheFrameAndItsHandles_SitClearOfTheGroup_AndPassTapsToThePartsUnderThem()
    {
        var (build, gestures) = ThreeLooseJoints(BuildTool.Select);
        gestures.View.VisibleArea = new CanvasRect(new Vector2D(-300, -60), new Vector2D(300, 300));
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
        gestures.RotateStem.ShouldBe((new Vector2D(50, -48), _rotateHandle));
        gestures.FrameCornerSquares.ShouldBe(
            [new Vector2D(100 - _frameRight, -48), new Vector2D(_frameRight, -48), new Vector2D(100 - _frameRight, 48)]);

        gestures.Tap(_rotateHandle);
        ShouldSelect(build, nodes: [1, 2]);
        var joint = build.PlaceNode(_moveHandle);
        build.ReplaceSelection([1, 2]);
        gestures.Tap(_moveHandle);
        build.SelectedNodeIds.ShouldBe([1, 2, joint], ignoreOrder: true);
        gestures.Tap(_moveHandle);
        build.SelectedNodeIds.ShouldBe([1, 2], ignoreOrder: true);
        gestures.DragAcross(_moveHandle, new Vector2D(_moveHandle.X, 40));
        build.Nodes[0].Position.ShouldBe(new Vector2D(0, 40));
        build.Nodes.Single(node => node.Id == joint).Position.ShouldBe(_moveHandle);
        build.SelectedNodeIds.ShouldBe([1, 2], ignoreOrder: true);

        (build, gestures) = ThreeLooseJoints(BuildTool.Select);
        var beam = build.ConnectLink(BuildLink.Beam, 1, 2)!.Value;
        build.ReplaceSelection([1, 2]);
        gestures.Tap(_moveHandle);
        build.Selection.Beams.ShouldBe([beam]);
        build.SelectedNodeIds.ShouldBe([1, 2], ignoreOrder: true);
    }

    [Fact]
    public void Select_TheRotateHandle_TurnsTheFrameWithTheGroup_UntilANewSelection()
    {
        var (build, gestures) = ThreeLooseJoints(BuildTool.Select);
        build.ReplaceSelection([1, 2]);

        gestures.Press(_rotateHandle);
        gestures.Drag(new Vector2D(130, 0));
        Handle(gestures, SelectionHandle.Move).ShouldBe(_moveHandle);
        gestures.Release(new Vector2D(130, 0));
        gestures.SelectionFrameAngle.ShouldBe(Math.PI / 2, 1e-9);
        gestures.SelectionFrame.ShouldNotBeNull().Width.ShouldBe((2 * _frameRight) - 100, 1e-9);
        Handle(gestures, SelectionHandle.Rotate).X.ShouldBe(130, 1e-9);
        Handle(gestures, SelectionHandle.Rotate).Y.ShouldBe(0, 1e-9);

        // Inside the turned frame, though outside where the upright one was.
        gestures.DragAcross(new Vector2D(95, 70), new Vector2D(125, 70));
        build.Nodes[0].Position.X.ShouldBe(80, 1e-9);
        gestures.SelectionFrameAngle.ShouldBe(Math.PI / 2, 1e-9);
        Handle(gestures, SelectionHandle.Rotate).X.ShouldBe(160, 1e-9);
        Handle(gestures, SelectionHandle.Rotate).Y.ShouldBe(0, 1e-9);

        build.ReplaceSelection([1, 2]);
        gestures.SelectionFrameAngle.ShouldBe(0);
        Handle(gestures, SelectionHandle.Move).X.ShouldBe(80, 1e-9);
        Handle(gestures, SelectionHandle.Rotate).X.ShouldBe(80, 1e-9);
    }

    [Fact]
    public void Select_ScalingNeverReflects_KeepsATurnedFrameOnTheGroup_AndARefusedTurnLeavesTheFrame()
    {
        var (build, gestures) = ThreeLooseJoints(BuildTool.Select);
        build.ReplaceSelection([1, 2]);
        gestures.Press(_scaleHandle);
        gestures.Drag(new Vector2D(-200, -200));
        build.Nodes[0].Position.X.ShouldBeLessThan(build.Nodes[1].Position.X);

        // Turned by π/4 the triangle's frame centre stays at (50, 50), while the middle of
        // its joints' bounds moves to about (50, 15): only the frame centre keeps the frame on the group.
        (build, gestures) = ThreeLooseJoints(BuildTool.Select);
        build.ReplaceSelection([1, 2, 3]);
        var centre = Handle(gestures, SelectionHandle.Move);
        var reach = centre.Y - Handle(gestures, SelectionHandle.Rotate).Y;
        gestures.DragAcross(Handle(gestures, SelectionHandle.Rotate), new Vector2D(centre.X + (reach / Math.Sqrt(2)), centre.Y - (reach / Math.Sqrt(2))));
        Math.Abs(gestures.SelectionFrameAngle).ShouldBe(Math.PI / 4, 1e-9);
        var width = gestures.SelectionFrame.ShouldNotBeNull().Width;
        var scale = Handle(gestures, SelectionHandle.Scale);
        gestures.Press(scale);
        gestures.Drag(new Vector2D(centre.X + ((scale.X - centre.X) * 1.5), centre.Y + ((scale.Y - centre.Y) * 1.5)));
        gestures.SelectionFrame.ShouldNotBeNull().Width.ShouldBeGreaterThan(width);
        Handle(gestures, SelectionHandle.Move).X.ShouldBe(centre.X, 1e-9);
        Handle(gestures, SelectionHandle.Move).Y.ShouldBe(centre.Y, 1e-9);

        build = new BuildViewModel();
        build.PlaceNode(new Vector2D(BuildViewModel.BuildArea.Max.X - 18, -100));
        build.PlaceNode(new Vector2D(BuildViewModel.BuildArea.Max.X - 18, 100));
        build.ActiveTool = BuildTool.Select;
        gestures = new BuildGestures(build);
        build.ReplaceSelection([1, 2]);
        var rotate = Handle(gestures, SelectionHandle.Rotate);
        gestures.Press(rotate);
        gestures.Drag(new Vector2D(rotate.X - 200, 0));
        gestures.SelectionFrameAngle.ShouldBe(0);
        Handle(gestures, SelectionHandle.Rotate).ShouldBe(rotate);
    }

    [Fact]
    public void Taps_OnASensorPicture_SelectTheSensor_ButAJointDiscOrAServosJointWins()
    {
        var (build, gestures) = BeamWithSensor(100);
        gestures.Tap(new Vector2D(50, 9));
        build.SingleSelectedSensorId.ShouldBe(4);
        build.SingleSelectedBeamId.ShouldBeNull();
        build.ClearSelection();
        build.ActiveTool = BuildTool.Joint;
        gestures.Tap(new Vector2D(50, 0));
        build.SingleSelectedSensorId.ShouldBe(4);
        build.Nodes.Count.ShouldBe(2);

        // A short beam: the picture sits inside both joints' touch reach but off their rings.
        (build, gestures) = BeamWithSensor(50);
        gestures.Tap(new Vector2D(22, 0));
        build.SingleSelectedSensorId.ShouldBe(4);
        (build, gestures) = BeamWithSensor(30);
        gestures.Tap(new Vector2D(10, 0));
        build.SelectedNodeIds.ShouldBe([1]);

        build = TwoBeams(new ServoDef(6, 2, 4, 5));
        new BuildGestures(build).Tap(new Vector2D(100, 0));
        build.SingleSelectedServoId.ShouldBe(6);
        build.SingleSelectedNodeId.ShouldBeNull();
    }

    [Fact]
    public void Camera_WhenSelected_ShowsAnAimHandleJustPastItsPicture_AtAnyZoomAndWhenLocked()
    {
        var reach = 2 * ((SensorPicture.CameraSize / Math.Sqrt(2)) + 8 + BuildGestures.HandleHitRadius);
        var (build, gestures) = BeamWithSensor(300, SensorKind.Camera);
        gestures.Tap(new Vector2D(150, 0));
        var handle = gestures.SelectionHandles.ShouldHaveSingleItem();
        handle.Handle.ShouldBe(SelectionHandle.Aim);
        handle.Position.X.ShouldBe(150 + reach, 1e-9);
        handle.Position.Y.ShouldBe(0, 1e-9);

        (build, gestures) = BeamWithSensor(100, SensorKind.Camera);
        build.SelectOnly(CreatureElementKind.Sensor, 4);
        build.SetParameter(PartParameterId.Aim, 0);
        gestures.SelectionHandles.Single().Position.ShouldBe(new Vector2D(50 + reach, 0));
        var pictureReach = SensorPicture.CameraSize / Math.Sqrt(2);
        var gaps = 8 + BuildGestures.HandleHitRadius;
        for (var step = 0; step < 10; step++)
        {
            gestures.View.ZoomAbout(new Vector2D(0, 0), 1.1);

            // In canvas units: the picture part stays put, the on-screen gaps shrink as the view zooms in.
            gestures.SelectionHandles.Single().Position.X.ShouldBe(50 + (2 * (pictureReach + (gaps / gestures.View.Zoom))), 1e-9);
        }

        (build, gestures) = BeamWithSensor(100, SensorKind.Accelerometer);
        build.SelectOnly(CreatureElementKind.Sensor, 4);
        gestures.SelectionHandles.ShouldBeEmpty();

        build = Loaded(new CreatureDef([new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(100, 0)), new BeamDef(3, 1, 2), new SensorDef(4, 3, SensorKind.Camera)], nextPartId: 5), locked: true);
        gestures = new BuildGestures(build);
        build.SelectOnly(CreatureElementKind.Sensor, 4);
        build.AimableCameraId.ShouldBe(4);
        gestures.SelectionHandles.Select(entry => entry.Handle).ShouldBe([SelectionHandle.Aim]);
    }

    [Theory]
    [InlineData(BuildTool.Joint)]
    [InlineData(BuildTool.Beam)]
    public void Camera_DraggingTheAimHandle_TurnsItRelativeToItsBeam_AndATapOnTheHandleChangesNothing(BuildTool tool)
    {
        var (build, gestures) = BeamWithSensor(100, SensorKind.Camera);
        build.SelectOnly(CreatureElementKind.Sensor, 4);
        build.ActiveTool = tool;

        gestures.DragAcross(gestures.SelectionHandles.Single().Position, new Vector2D(50 + 100, -5));
        build.Sensors[0].Aim!.Value.ShouldBe(Math.Atan2(-5, 100), 1e-9);
        build.SingleSelectedSensorId.ShouldBe(4);
        build.Nodes[0].Position.ShouldBe(new Vector2D(0, 0));
        var aim = build.Sensors[0].Aim;
        gestures.Tap(gestures.SelectionHandles.Single().Position);
        build.SingleSelectedSensorId.ShouldBe(4);
        build.Sensors[0].Aim.ShouldBe(aim);
        build.Nodes.Count.ShouldBe(2);
        build.Beams.Count.ShouldBe(1);

        build.MoveNode(2, new Vector2D(0, 100));
        gestures.Press(gestures.SelectionHandles.Single().Position);
        gestures.Drag(new Vector2D(200, 50));
        build.Sensors[0].Aim!.Value.ShouldBe(-Math.PI / 2, 1e-9);
    }

    [Fact]
    public void ShowSelectedCameraRays_EasesOutUntilTheCameraAndEveryRayEndAreACellInside_OnlyWhileTheRaysShow()
    {
        var (build, camera, accelerometer) = DownwardBeamWithCameraAndAccelerometer();
        var gestures = FittedTo800By400(build);
        build.ToggleSelected(new(CreatureElementKind.Sensor, camera));
        build.SetParameter(PartParameterId.CameraRange, 1000);
        build.ToggleSelected(new(CreatureElementKind.Sensor, accelerometer));
        var (zoom, offset) = (gestures.View.Zoom, gestures.View.Offset);
        gestures.ShowSelectedCameraRays();
        gestures.View.Step(10);
        gestures.View.Zoom.ShouldBe(zoom);
        gestures.View.Offset.ShouldBe(offset);

        build.ToggleSelected(new(CreatureElementKind.Sensor, accelerometer));
        gestures.ShowSelectedCameraRays();
        gestures.View.Step(10);

        gestures.View.Zoom.ShouldBeLessThan(zoom);
        var (origin, ends) = build.ShownCameraRays().ShouldHaveSingleItem();
        ends.Max(end => end.Y).ShouldBeGreaterThan(BuildViewModel.BuildViewBounds.Max.Y);
        var cell = BuildViewModel.BuildViewMargin * gestures.View.Zoom;
        foreach (var point in ends.Append(origin).Select(gestures.View.ToView))
        {
            point.X.ShouldBeInRange(cell - 1e-6, 800 - cell + 1e-6);
            point.Y.ShouldBeInRange(cell - 1e-6, 400 - cell + 1e-6);
        }
    }

    [Fact]
    public void Step_HoldsTheViewWhileAFingerIsDownOrTheRaysShrink_AndEasesBackInsideOnceTheyHide()
    {
        var (build, camera, _) = DownwardBeamWithCameraAndAccelerometer();
        var gestures = FittedTo800By400(build);
        build.ToggleSelected(new(CreatureElementKind.Sensor, camera));
        build.SetParameter(PartParameterId.CameraRange, 1000);
        gestures.ShowSelectedCameraRays();
        var (zoom, offset) = (gestures.View.Zoom, gestures.View.Offset);
        gestures.Press(new Vector2D(790, 10));
        gestures.Step(10);
        gestures.View.Zoom.ShouldBe(zoom);
        gestures.View.Offset.ShouldBe(offset);
        gestures.Cancel();
        gestures.Release(new Vector2D(790, 10));
        gestures.Step(10);
        gestures.View.Zoom.ShouldBeLessThan(zoom);

        (zoom, offset) = (gestures.View.Zoom, gestures.View.Offset);
        build.SetParameter(PartParameterId.CameraRange, 300);
        gestures.ShowSelectedCameraRays();
        gestures.Step(10);
        gestures.View.Zoom.ShouldBe(zoom);
        gestures.View.Offset.ShouldBe(offset);
        build.ClearSelection();
        gestures.Step(10);

        // All of BuildViewBounds (13 × 7 m) in an 800 × 400 view: its height fits, centred.
        var bounds = BuildViewModel.BuildViewBounds;
        gestures.View.Zoom.ShouldBe(400 / bounds.Height, 1e-9);
        gestures.View.ToCanvas(new Vector2D(400, 200)).ShouldBe(bounds.Center);
    }

    [Theory]
    [InlineData(BuildTool.Joint, false)]
    [InlineData(BuildTool.Select, true)]
    public void DraggingASensorToAFreeBeam_MovesItThereAsOneStep_InAnyToolAndWhenLocked(BuildTool tool, bool locked)
    {
        var (build, gestures) = SensorsOnThreeBeams(locked);
        build.ActiveTool = tool;

        gestures.DragAcross(new Vector2D(100, 0), new Vector2D(100, 150));

        build.Sensors.Single(sensor => sensor.Id == 6).BeamId.ShouldBe(7);
        build.SingleSelectedSensorId.ShouldBe(6);
        Positions(build).ShouldBe([new(0, 0), new(200, 0), new(0, 150), new(200, 150)]);
        build.Undo();
        build.Sensors.Single(sensor => sensor.Id == 6).BeamId.ShouldBe(5);
    }

    [Fact]
    public void DraggingASensor_PreviewsItOnlyOnABeamThatTakesIt_AndTheLineFollowsTheFinger()
    {
        var (_, gestures) = SensorsOnThreeBeams();
        gestures.Press(new Vector2D(100, 0));
        gestures.Drag(new Vector2D(100, 150));
        gestures.MovingSensorId.ShouldBe(6);
        gestures.MovedSensorPreview.ShouldBe(new SensorDef(6, 7, SensorKind.Accelerometer));

        // The Camera's beam is taken, and its own beam is where it already is.
        gestures.Drag(new Vector2D(200, 75));
        gestures.SensorDropTarget.ShouldBe(Beam(8));
        gestures.MovedSensorPreview.ShouldBeNull();
        gestures.Drag(new Vector2D(100, 0));
        gestures.MovedSensorPreview.ShouldBeNull();

        (_, gestures) = SensorsOnThreeBeams();
        gestures.View.ZoomAbout(new Vector2D(0, 0), 2);
        gestures.View.PanBy(new Vector2D(50, 50));
        gestures.Press(new Vector2D(250, 50));
        gestures.Drag(new Vector2D(250, 450));
        var redraws = 0;
        gestures.Changed += (_, _) => redraws++;

        // Both over empty canvas: the drop target stays, but the line still follows the finger.
        gestures.Drag(new Vector2D(450, 450));
        gestures.SensorDragEnd.ShouldBe(new Vector2D(200, 200));
        redraws.ShouldBe(1);
        gestures.Release(new Vector2D(450, 450));
        gestures.SensorDragEnd.ShouldBeNull();
    }

    [Fact]
    public void ASensorDroppedOnATakenBeamOrEmptyCanvas_Stays_WithANoteOnlyOnTheBeam()
    {
        var (build, gestures) = SensorsOnThreeBeams();

        gestures.DragAcross(new Vector2D(100, 0), new Vector2D(200, 75));
        build.Sensors.Single(sensor => sensor.Id == 6).BeamId.ShouldBe(5);
        build.PlacementNote!.Target.ShouldBe(Beam(8));
        build.CanUndo.ShouldBeFalse();
        gestures.MovingSensorId.ShouldBeNull();

        gestures.DragAcross(new Vector2D(100, 0), new Vector2D(100, 300));
        build.Sensors.Single(sensor => sensor.Id == 6).BeamId.ShouldBe(5);
        build.PlacementNote.ShouldBeNull();
    }

    [Theory]
    [InlineData(BuildLink.Piston)]
    [InlineData(BuildLink.Spring)]
    public void ASensorDroppedOnALink_StaysWithTheNoteThere_ButWhereTheLinkCrossesAFreeBeamGoesOnTheBeam(BuildLink kind)
    {
        // Free beam 7 rises over the link crossing it at (100, 150) while the sensor moves (#1107).
        var (build, gestures) = SensorsOnThreeBeams();
        var top = build.PlaceNode(new Vector2D(100, 100));
        var bottom = build.PlaceNode(new Vector2D(100, 200));
        var link = new CreatureElementSelection(kind == BuildLink.Piston ? CreatureElementKind.Piston : CreatureElementKind.Spring, build.ConnectLink(kind, top, bottom)!.Value);
        var changes = build.CountAnatomyChanges();

        gestures.Press(new Vector2D(100, 0));
        gestures.Drag(new Vector2D(100, 130));
        gestures.SensorDropTarget.ShouldBe(link);
        gestures.MovedSensorPreview.ShouldBeNull();
        gestures.Release(new Vector2D(100, 130));
        build.Sensors.Single(sensor => sensor.Id == 6).BeamId.ShouldBe(5);
        build.PlacementNote.ShouldBe(new CanvasNote(CanvasNoteKind.Danger, link, BuildViewModel.GoesOnABeamReason(SensorKind.Accelerometer)));
        changes().ShouldBe(0);

        gestures.Press(new Vector2D(100, 0));
        gestures.Drag(new Vector2D(100, 150));
        gestures.SensorDropTarget.ShouldBe(Beam(7));
        gestures.Release(new Vector2D(100, 150));
        build.Sensors.Single(sensor => sensor.Id == 6).BeamId.ShouldBe(7);
        build.PlacementNote.ShouldBeNull();
    }

    [Fact]
    public void DeletingTheSensorOrTheHoveredBeamMidDrag_EndsItWithoutAMove()
    {
        var (build, gestures) = SensorsOnThreeBeams();
        build.SelectOnly(CreatureElementKind.Sensor, 6);
        gestures.Press(new Vector2D(100, 0));
        gestures.Drag(new Vector2D(100, 150));
        build.PlacingTargetsOf(null, 6).Beams.ShouldContain(7);
        build.DeleteSelectedParts();
        gestures.MovedSensorPreview.ShouldBeNull();
        build.PlacingTargetsOf(null, 6).ShouldBeSameAs(PlacingTargets.None);
        gestures.Release(new Vector2D(100, 150));
        build.Sensors.Select(sensor => sensor.Id).ShouldBe([9]);
        gestures.MovingSensorId.ShouldBeNull();
        build.Undo();
        build.Sensors.Select(sensor => sensor.Id).ShouldBe([6, 9]);

        (build, gestures) = SensorsOnThreeBeams();
        build.ReplaceSelection(Set(Beam(7)));
        gestures.Press(new Vector2D(100, 0));
        gestures.Drag(new Vector2D(100, 150));
        build.DeleteSelectedParts();
        gestures.MovedSensorPreview.ShouldBeNull();
        gestures.Release(new Vector2D(100, 150));
        build.Sensors.Single(sensor => sensor.Id == 6).BeamId.ShouldBe(5);
        gestures.MovingSensorId.ShouldBeNull();
    }

    private static Vector2D[] Positions(BuildViewModel build) => [.. build.Nodes.Select(node => node.Position)];

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

    private static BuildGestures FittedTo800By400(BuildViewModel build)
    {
        var gestures = new BuildGestures(build);
        gestures.View.VisibleArea = new CanvasRect(new Vector2D(0, 0), new Vector2D(800, 400));
        gestures.View.Fit();
        return gestures;
    }

    /// <summary>Joints 1 (0,0) and 2 (<paramref name="length"/>,0), beam 3 between them and sensor 4 on it.</summary>
    private static (BuildViewModel Build, BuildGestures Gestures) BeamWithSensor(double length, SensorKind kind = SensorKind.Accelerometer)
    {
        var builder = new CreatureBuilder();
        builder.AddNode(new Vector2D(0, 0));
        builder.AddNode(new Vector2D(length, 0));
        builder.AddSensor(builder.AddBeam(1, 2), kind, out _, out _);
        var build = new BuildViewModel(builder);
        return (build, new BuildGestures(build));
    }

    /// <summary>
    /// Joints 1 (0,0), 2 (200,0), 3 (0,150) and 4 (200,150); an Accelerometer 6 on beam 5 (1–2),
    /// nothing on beam 7 (3–4) and a Camera 9 on beam 8 (2–4).
    /// </summary>
    private static (BuildViewModel Build, BuildGestures Gestures) SensorsOnThreeBeams(bool locked = false)
    {
        var builder = new CreatureBuilder();
        builder.AddNode(new Vector2D(0, 0));
        builder.AddNode(new Vector2D(200, 0));
        builder.AddNode(new Vector2D(0, 150));
        builder.AddNode(new Vector2D(200, 150));
        builder.AddSensor(builder.AddBeam(1, 2), SensorKind.Accelerometer, out _, out _);
        builder.AddBeam(3, 4);
        builder.AddSensor(builder.AddBeam(2, 4), SensorKind.Camera, out _, out _);
        var build = Loaded(builder.Build(), locked);
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

    /// <summary>Joints 1 (0,0) and 2 (100,0) and beam 3 between them, placed one by one; Parts is active.</summary>
    private static (BuildViewModel Build, BuildGestures Gestures) TwoJointsAndABeam()
    {
        var build = new BuildViewModel();
        build.PlaceNode(new Vector2D(0, 0));
        build.PlaceNode(new Vector2D(100, 0));
        build.ConnectLink(BuildLink.Beam, 1, 2);
        return (build, new BuildGestures(build));
    }

    /// <summary>TwoJointsAndABeam with a Servo on joint 2; Select is active.</summary>
    private static (BuildViewModel Build, BuildGestures Gestures, int Servo) ServoOnABeamsEnd()
    {
        var (build, gestures) = TwoJointsAndABeam();
        var servo = build.PlacePart(BuildPart.Servo, Joint(2))!.Value;
        build.ActiveTool = BuildTool.Select;
        build.ClearSelection();
        return (build, gestures, servo);
    }
}
