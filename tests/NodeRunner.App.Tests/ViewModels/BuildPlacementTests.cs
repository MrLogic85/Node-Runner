using NodeRunner.App.Builders;
using NodeRunner.App.Lifecycle;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.ViewModels;

/// <summary>Dropping a part from the Parts tray onto the drawing (#376).</summary>
public sealed class BuildPlacementTests
{
    private static readonly CreatureElementSelection _firstBeam = new(CreatureElementKind.Beam, 4);
    private static readonly CreatureElementSelection _secondBeam = new(CreatureElementKind.Beam, 5);
    private static readonly CreatureElementSelection _firstJoint = new(CreatureElementKind.Node, 1);
    private static readonly CreatureElementSelection _middleJoint = new(CreatureElementKind.Node, 2);

    [Fact]
    public void PlacePart_OnAFreeBeam_AddsTheSensorThereWithAFreshId()
    {
        var build = TwoBeams();
        var freshId = build.Snapshot().NextPartId;
        var changes = CountChanges(build);

        var id = build.PlacePart(BuildPart.Accelerometer, _firstBeam);

        id.ShouldBe(freshId);
        build.Sensors.Select(sensor => (sensor.Id, sensor.BeamId, sensor.Kind)).ShouldBe([(freshId, 4, SensorKind.Accelerometer)]);
        changes().ShouldBe(1);
        build.PlacementNote.ShouldBeNull();
    }

    [Fact]
    public void PlacePart_PlacesOnAnyNumberOfBeams()
    {
        var build = TwoBeams();

        build.PlacePart(BuildPart.Accelerometer, _firstBeam).ShouldNotBeNull();
        build.PlacePart(BuildPart.Accelerometer, _secondBeam).ShouldNotBeNull();

        build.Sensors.Select(sensor => sensor.BeamId).ShouldBe([4, 5]);
        build.Sensors.Select(sensor => sensor.Id).ShouldBeUnique();
    }

    [Fact]
    public void PlacePart_OnEmptyCanvas_ChangesNothing_AndSaysNothing()
    {
        var build = TwoBeams();
        var changes = CountChanges(build);

        build.PlacePart(BuildPart.Accelerometer, null).ShouldBeNull();

        build.Sensors.ShouldBeEmpty();
        changes().ShouldBe(0);
        build.PlacementNote.ShouldBeNull();
        build.CanvasNotes().ShouldBeEmpty();
    }

    [Fact]
    public void PlacePart_OnAJoint_ChangesNothing_AndNotesWhyAtTheJoint()
    {
        var build = TwoBeams();
        var changes = CountChanges(build);

        build.PlacePart(BuildPart.Accelerometer, _firstJoint).ShouldBeNull();

        build.Sensors.ShouldBeEmpty();
        changes().ShouldBe(0);
        build.PlacementNote.ShouldBe(new CanvasNote(CanvasNoteKind.Danger, _firstJoint, UiText.Plain("Accelerometers go on a beam")));
    }

    [Fact]
    public void PlacePart_OnABeamWithASensor_ChangesNothing_AndNotesWhyAtTheBeam()
    {
        var build = TwoBeams();
        build.PlacePart(BuildPart.Accelerometer, _firstBeam);
        var changes = CountChanges(build);

        build.PlacePart(BuildPart.Accelerometer, _firstBeam).ShouldBeNull();

        build.Sensors.Count.ShouldBe(1);
        changes().ShouldBe(0);
        build.PlacementNote.ShouldBe(new CanvasNote(CanvasNoteKind.Danger, _firstBeam, UiText.Plain("One sensor per beam")));
    }

    [Fact]
    public void PlacePart_Camera_OnAFreeBeam_LooksLevelAndForward()
    {
        var build = TwoBeams();

        var id = build.PlacePart(BuildPart.Camera, _firstBeam);

        id.ShouldNotBeNull();
        build.Sensors.Select(sensor => (sensor.Id, sensor.BeamId, sensor.Kind, sensor.Aim)).ShouldBe([(id.Value, 4, SensorKind.Camera, (double?)0)]);
    }

    [Theory]
    [InlineData(BuildPart.Battery)]
    [InlineData(BuildPart.TouchSensor)]
    public void PlacePart_ComingLater_IsRefused(BuildPart part)
    {
        var build = TwoBeams();

        build.CanPlacePart(part, _firstBeam, out var reason).ShouldBeFalse();
        reason.ShouldBe(PartTray.ComingLater);
        build.PlacePart(part, _firstBeam).ShouldBeNull();

        build.Sensors.ShouldBeEmpty();
    }

    [Fact]
    public void PlacePart_OnALockedCreation_ChangesNothing_WithoutANote()
    {
        var build = new BuildViewModel();
        build.Load(TwoBeams().Snapshot(), locked: true);

        build.PlacePart(BuildPart.Accelerometer, _firstBeam).ShouldBeNull();

        build.Sensors.ShouldBeEmpty();
        build.PlacementNote.ShouldBeNull();
    }

    [Fact]
    public void CanPlacePart_SaysWhichBeamsTakeASensor()
    {
        var build = TwoBeams();
        build.PlacePart(BuildPart.Accelerometer, _firstBeam);

        build.CanPlacePart(BuildPart.Accelerometer, _firstBeam, out var taken).ShouldBeFalse();
        taken.ShouldBe(UiText.Plain("One sensor per beam"));
        build.CanPlacePart(BuildPart.Accelerometer, _secondBeam, out var free).ShouldBeTrue();
        free.ShouldBeNull();
    }

    // #1107: the canvas marks these takes and refusals on the parts themselves.
    [Fact]
    public void PlacingTargetsOf_SplitsWhatTakesThePartFromWhatRefusesIt()
    {
        var build = TwoBeams();
        var sensor = build.PlacePart(BuildPart.Accelerometer, _firstBeam)!.Value;
        build.PlacePart(BuildPart.Servo, _middleJoint).ShouldNotBeNull();

        var camera = build.PlacingTargetsOf(BuildPart.Camera, null);
        camera.Beams.ShouldBe([_secondBeam.Id]);
        camera.RefusedBeams.ShouldBe([_firstBeam.Id]);

        build.PlacePart(BuildPart.Camera, _secondBeam).ShouldNotBeNull();
        var moving = build.PlacingTargetsOf(null, sensor);
        moving.Beams.ShouldBe([_firstBeam.Id]);
        moving.RefusedBeams.ShouldBe([_secondBeam.Id]);
        moving.MovingSensor.ShouldBe(sensor);

        build.PlacePart(BuildPart.Wheel, new CreatureElementSelection(CreatureElementKind.Node, 3)).ShouldNotBeNull();
        var servo = build.PlacingTargetsOf(BuildPart.Servo, null);
        servo.Joints.ShouldBe([_firstJoint.Id]);
        servo.RefusedJoints.ShouldBe([_middleJoint.Id, 3], ignoreOrder: true);
        servo.JointRing.ShouldBe(ServoDef.JointRadius);
        var wheel = build.PlacingTargetsOf(BuildPart.Wheel, null);
        wheel.Joints.ShouldBe([_firstJoint.Id]);
        wheel.JointRing.ShouldBe(WheelDef.DefaultRadius);
    }

    [Fact]
    public void PlacementNote_ComesFirst_AndGoesWhenDismissed_OrOnTheNextDrop()
    {
        var build = TwoBeams();
        build.PlacePart(BuildPart.Accelerometer, _firstJoint);

        build.CanvasNotes()[0].Text.ShouldBe(UiText.Plain("Accelerometers go on a beam"));
        build.DismissPlacementNote();
        build.CanvasNotes().ShouldBeEmpty();

        build.PlacePart(BuildPart.Accelerometer, _firstJoint);
        build.PlacePart(BuildPart.Accelerometer, null);
        build.PlacementNote.ShouldBeNull();
    }

    [Fact]
    public void PlacementNote_IsNotListed_OnceItsPartIsGone()
    {
        var build = TwoBeams();
        build.PlacePart(BuildPart.Accelerometer, _firstJoint);

        build.ToggleSelected(new(CreatureElementKind.Node, 1));
        build.DeleteSelectedParts();

        build.CanvasNotes().ShouldBeEmpty();
    }

    // #1107: the part drawn on top, which covers every joint's reach, then a beam's reach. A beam
    // that would take the sensor rises alone over its joints, so it is hit there too.
    [Fact]
    public void DropTargetAt_TakesThePartDrawnOnTop_ThenABeamWithinReach()
    {
        var build = TwoBeams();
        build.PlacePart(BuildPart.Accelerometer, _secondBeam);
        var gestures = new BuildGestures(build);

        gestures.DropTargetAt(new Vector2D(5, 0), BuildPart.Accelerometer).ShouldBe(_firstJoint);
        gestures.DropTargetAt(new Vector2D(5, 9), BuildPart.Accelerometer).ShouldBe(_firstJoint);
        gestures.DropTargetAt(new Vector2D(150, 9), BuildPart.Accelerometer).ShouldBe(_secondBeam);
        gestures.DropTargetAt(new Vector2D(30, 10), BuildPart.Accelerometer).ShouldBe(_firstBeam);
        gestures.DropTargetAt(new Vector2D(300, 300), BuildPart.Accelerometer).ShouldBeNull();

        // Zoomed in, the beam's finger-sized reach is shorter than the joint's gap.
        gestures.View.ZoomAbout(new Vector2D(0, 0), 2);
        gestures.DropTargetAt(gestures.View.ToView(new Vector2D(-17, 0)), BuildPart.Accelerometer).ShouldBe(_firstJoint);
    }

    // A Piston or Spring takes no tray part, but is a target so a drop on it says why (#1033).
    [Theory]
    [InlineData(BuildLink.Piston)]
    [InlineData(BuildLink.Spring)]
    public void DropTargetAt_FindsAPistonOrSpring_OverABeamItCrosses_ButNotInAJointsReach(BuildLink kind)
    {
        var (build, link) = BeamCrossedBy(kind);
        var gestures = new BuildGestures(build);

        gestures.DropTargetAt(new Vector2D(50, 0), placing: null).ShouldBe(link);
        gestures.DropTargetAt(new Vector2D(50, 30), BuildPart.Accelerometer).ShouldBe(link);

        // A beam that would take the sensor rises over the link it crosses (#1107).
        gestures.DropTargetAt(new Vector2D(50, 0), BuildPart.Accelerometer).ShouldBe(new CreatureElementSelection(CreatureElementKind.Beam, 5));
        gestures.DropTargetAt(new Vector2D(20, 0), BuildPart.Accelerometer).ShouldBe(new CreatureElementSelection(CreatureElementKind.Beam, 5));
        gestures.DropTargetAt(new Vector2D(50, -33), BuildPart.Accelerometer).ShouldBe(new CreatureElementSelection(CreatureElementKind.Node, 3));
    }

    // A Servo being placed rings every joint, and the ring is where a finger aims (#1055).
    [Fact]
    public void DropTargetAt_ForAServo_LandsOnAJointAnywhereInsideItsRing_BeforeABeam()
    {
        var gestures = new BuildGestures(TwoBeams());
        var ring = SelectionMarks.JointHalo(ServoDef.JointRadius);

        gestures.DropTargetAt(new Vector2D(20, 0), BuildPart.Servo).ShouldBe(_firstJoint);
        gestures.DropTargetAt(new Vector2D(20, 0), BuildPart.Accelerometer).ShouldBe(_firstBeam);
        gestures.DropTargetAt(new Vector2D(0, ring), BuildPart.Servo).ShouldBe(_firstJoint);
        gestures.DropTargetAt(new Vector2D(0, ring + 1), BuildPart.Servo).ShouldBeNull();
    }

    [Fact]
    public void EverySensorKind_NamesItselfInItsGoesOnABeamReason()
    {
        // The reason names the part (#1053), so a new kind needs its own sentence.
        foreach (var kind in Enum.GetValues<SensorKind>())
        {
            BuildViewModel.GoesOnABeamReason(kind).Message.ShouldStartWith(kind.ToString());
        }
    }

    [Theory]
    [InlineData(BuildPart.Accelerometer, BuildLink.Piston, "Accelerometers go on a beam")]
    [InlineData(BuildPart.Accelerometer, BuildLink.Spring, "Accelerometers go on a beam")]
    [InlineData(BuildPart.Camera, BuildLink.Piston, "Cameras go on a beam")]
    [InlineData(BuildPart.Camera, BuildLink.Spring, "Cameras go on a beam")]
    [InlineData(BuildPart.Servo, BuildLink.Piston, "Servos go on a joint")]
    [InlineData(BuildPart.Servo, BuildLink.Spring, "Servos go on a joint")]
    public void APartTappedOrDroppedOnAPistonOrSpring_ChangesNothing_AndNotesWhyThere(BuildPart part, BuildLink kind, string reason)
    {
        var (build, link) = BeamCrossedBy(kind);
        build.ActiveTool = BuildTool.Parts;
        var gestures = new BuildGestures(build);
        var refused = new CanvasNote(CanvasNoteKind.Danger, link, UiText.Plain(reason));
        // Off the beam it crosses, which a sensor's target halo or a selected joint raises over it
        // (#1107), and off the Servo's target rings.
        var onLink = new Vector2D(50, 15);
        build.PickPart(part);
        var tapChanges = CountChanges(build);

        gestures.Press(onLink);
        gestures.Release(onLink);

        tapChanges().ShouldBe(0);
        build.PlacementNote.ShouldBe(refused);
        build.PickedPart.ShouldBe(part);
        build.SelectedPartCount.ShouldBe(0);

        build.ReplaceSelection([1]);
        var dropChanges = CountChanges(build);
        gestures.DropPart(part, onLink).ShouldBeNull();

        dropChanges().ShouldBe(0);
        build.PlacementNote.ShouldBe(refused);
        build.SelectedNodeIds.ShouldBe([1]);
        build.Sensors.ShouldBeEmpty();
        build.Servos.ShouldBeEmpty();
    }

    [Fact]
    public void DropPart_PlacesOnTheBeamUnderThePointer()
    {
        var build = TwoBeams();
        var gestures = new BuildGestures(build);

        gestures.DropPart(BuildPart.Accelerometer, new Vector2D(40, 0)).ShouldNotBeNull();

        build.Sensors.Single().BeamId.ShouldBe(4);
    }

    [Fact]
    public void Press_DismissesThePlacementNote()
    {
        var build = TwoBeams();
        var gestures = new BuildGestures(build);
        gestures.DropPart(BuildPart.Accelerometer, new Vector2D(0, 0));
        build.PlacementNote.ShouldNotBeNull();

        gestures.Press(new Vector2D(300, 300));

        build.PlacementNote.ShouldBeNull();
    }

    [Fact]
    public void PartTray_KnowsWhichPartsAreSensors_AndWhichCanBeDragged()
    {
        PartTray.SensorKindOf(BuildPart.Accelerometer).ShouldBe(SensorKind.Accelerometer);
        PartTray.SensorKindOf(BuildPart.Camera).ShouldBe(SensorKind.Camera);
        PartTray.SensorKindOf(BuildPart.Servo).ShouldBeNull();
        PartTray.IsAvailable(BuildPart.Accelerometer).ShouldBeTrue();
        PartTray.IsAvailable(BuildPart.Battery).ShouldBeFalse();
    }

    [Fact]
    public void PlacePart_Servo_OnAJointWithTwoBeams_AddsAndSelectsIt()
    {
        var build = TwoBeams();
        var freshId = build.Snapshot().NextPartId;

        var id = build.PlacePart(BuildPart.Servo, _middleJoint);

        id.ShouldBe(freshId);
        build.Servos.Select(servo => (servo.Id, servo.NodeId, servo.FixedLinkId, servo.TargetLinkId))
            .ShouldBe([(freshId, 2, 4, 5)]);
        build.SingleSelectedServoId.ShouldBe(freshId);
        build.NodeRadius(2).ShouldBe(ServoDef.JointRadius);
    }

    [Theory]
    [InlineData("beam-piston", 4, 5)]
    [InlineData("beam-spring", 4, 5)]
    [InlineData("piston-spring", 4, 5)]
    [InlineData("piston-before-beam", 3, 4)]
    [InlineData("spring-before-beam", 3, 4)]
    public void PlacePart_Servo_OnAJointWithAnyTwoLinks_AddsDefaultLowestIds(string setup, int fixedLink, int targetLink)
    {
        var build = setup switch
        {
            "beam-piston" => BeamAndPiston(),
            "beam-spring" => BeamAndSpring(),
            "piston-spring" => PistonAndSpring(),
            "piston-before-beam" => PistonWithLowerIdThanBeam(),
            "spring-before-beam" => SpringWithLowerIdThanBeam(),
            _ => throw new ArgumentOutOfRangeException(nameof(setup)),
        };
        var freshId = build.Snapshot().NextPartId;

        var id = build.PlacePart(BuildPart.Servo, _middleJoint);

        id.ShouldBe(freshId);
        build.Servos.Select(servo => (servo.Id, servo.NodeId, servo.FixedLinkId, servo.TargetLinkId))
            .ShouldBe([(freshId, 2, fixedLink, targetLink)]);
    }

    [Fact]
    public void SetServoLink_WithMixedLinks_SwapsRolesAndGivesFreshId()
    {
        var build = PistonAndSpring();
        var servoId = build.PlacePart(BuildPart.Servo, _middleJoint)!.Value;
        var presentation = new BuildPresentationViewModel(build).SinglePart!;

        presentation.Pickers![0].Options.ShouldBe([UiText.Format("Piston {0}", 1), UiText.Format("Spring {0}", 1)]);

        var newId = build.SetServoLink(servoId, fixedRole: true, linkId: 5);

        newId.ShouldBe(7);
        build.Servos.Select(servo => (servo.Id, servo.FixedLinkId, servo.TargetLinkId))
            .ShouldBe([(7, 5, 4)]);
        build.SingleSelectedServoId.ShouldBe(7);
    }

    [Fact]
    public void SetServoLink_AsFirstUndoStep_PresentationNeverSeesOldServoId()
    {
        var build = PistonAndSpring();
        var servoId = build.PlacePart(BuildPart.Servo, _middleJoint)!.Value;
        build.Load(build.Snapshot());
        build.ReplaceSelection(PartSet.None with { Servos = new HashSet<int> { servoId } });
        var presentation = new BuildPresentationViewModel(build);
        var shownIds = new List<int?>();
        presentation.PresentationChanged += (_, _) => shownIds.Add(presentation.SinglePart?.Id);

        var newId = build.SetServoLink(servoId, fixedRole: true, linkId: 5);

        build.CanUndo.ShouldBeTrue();
        shownIds.ShouldNotBeEmpty();
        shownIds.ShouldAllBe(id => id == newId);
    }

    [Fact]
    public void UndoAndRedo_OfServoLinkChange_KeepTheServoSelected()
    {
        var build = PistonAndSpring();
        var servoId = build.PlacePart(BuildPart.Servo, _middleJoint)!.Value;
        var newId = build.SetServoLink(servoId, fixedRole: true, linkId: 5)!.Value;
        var presentation = new BuildPresentationViewModel(build);
        var shownIds = new List<int?>();
        presentation.PresentationChanged += (_, _) => shownIds.Add(presentation.SinglePart?.Id);

        build.Undo();

        build.SingleSelectedServoId.ShouldBe(servoId);
        build.Servos.Single().FixedLinkId.ShouldBe(4);

        build.Redo();

        build.SingleSelectedServoId.ShouldBe(newId);
        shownIds.ShouldNotBeEmpty();
        shownIds.ShouldAllBe(id => id == servoId || id == newId);
    }

    [Fact]
    public void Redo_OfServoPlacement_UnderASelectedJoint_SelectsTheServoInstead()
    {
        var build = PistonAndSpring();
        var servoId = build.PlacePart(BuildPart.Servo, _middleJoint)!.Value;
        build.Undo();
        build.ReplaceSelection(PartSet.None with { Nodes = new HashSet<int> { _middleJoint.Id } });

        build.Redo();

        build.Selection.Nodes.ShouldBeEmpty();
        build.SingleSelectedServoId.ShouldBe(servoId);
    }

    [Fact]
    public void Undo_OfServoLinkChange_WithServoNotSelected_SelectsNothing()
    {
        var build = PistonAndSpring();
        var servoId = build.PlacePart(BuildPart.Servo, _middleJoint)!.Value;
        build.SetServoLink(servoId, fixedRole: true, linkId: 5);
        build.ReplaceSelection(PartSet.None);

        build.Undo();

        build.SelectedPartCount.ShouldBe(0);
    }

    [Fact]
    public void UndoingADelete_WithAServoSelectedSince_SelectsOnlyTheDeletedPart()
    {
        var build = PistonAndSpring();
        var servoId = build.PlacePart(BuildPart.Servo, _middleJoint)!.Value;
        build.ReplaceSelection(PartSet.None with { Pistons = new HashSet<int> { 4 } });
        build.DeleteSelectedParts();
        build.ReplaceSelection(PartSet.None with { Servos = new HashSet<int> { servoId } });

        build.Undo();

        build.SelectedServoCount.ShouldBe(0);
        build.Selection.Pistons.ShouldBe([4]);
    }

    [Fact]
    public void ServoPanel_KeepsItsPanelId_ThroughLinkChangeUndoAndRedo()
    {
        var build = PistonAndSpring();
        var servoId = build.PlacePart(BuildPart.Servo, _middleJoint)!.Value;
        var presentation = new BuildPresentationViewModel(build);
        var panelId = presentation.SinglePart!.PanelId;

        build.SetServoLink(servoId, fixedRole: true, linkId: 5);
        presentation.SinglePart!.PanelId.ShouldBe(panelId);
        build.Undo();
        presentation.SinglePart!.PanelId.ShouldBe(panelId);
        build.Redo();
        presentation.SinglePart!.PanelId.ShouldBe(panelId);

        build.ReplaceSelection(PartSet.None with { Pistons = new HashSet<int> { 4 } });
        presentation.SinglePart!.PanelId.ShouldBe(4);
    }

    [Fact]
    public void SetServoLink_PickerOrder_UsesLowestIdsAcrossLinkKinds()
    {
        var build = PistonWithLowerIdThanBeam();
        build.PlacePart(BuildPart.Servo, _middleJoint).ShouldNotBeNull();

        var picker = new BuildPresentationViewModel(build).SinglePart!.Pickers![0];

        picker.LinkIds.ShouldBe([3, 4]);
        picker.LinkKinds.ShouldBe([CreatureElementKind.Piston, CreatureElementKind.Beam]);
        picker.Options.ShouldBe([UiText.Format("Piston {0}", 1), UiText.Format("Beam {0}", 1)]);
    }

    [Fact]
    public void DeleteHeldPistonOrSpring_ClearsServoRole()
    {
        var build = PistonAndSpring();
        var servoId = build.PlacePart(BuildPart.Servo, _middleJoint)!.Value;

        build.ReplaceSelection(PartSet.None with { Pistons = new HashSet<int> { 4 } });
        build.DeleteSelectedParts();

        var servo = build.Servos.Single();
        servo.Id.ShouldBe(servoId);
        servo.FixedLinkId.ShouldBeNull();
        servo.TargetLinkId.ShouldBe(5);
        build.ShowTrainingBlockers();
        var note = build.CanvasNotes().Single(note => note.Target.Id == servoId);
        note.Text.ShouldBe(CreatureBuilder.ServoNeedsTwoLinksReason);
        CanvasNoteTargets.JointIds(note.Target, build).ShouldBe([2]);

        build.ReplaceSelection(PartSet.None with { Springs = new HashSet<int> { 5 } });
        build.DeleteSelectedParts();

        build.Servos.Single().TargetLinkId.ShouldBeNull();
    }

    [Fact]
    public void DeleteServo_Alone_LeavesItsJoint()
    {
        var build = TwoBeams();
        var servoId = build.PlacePart(BuildPart.Servo, _middleJoint)!.Value;

        build.ReplaceSelection(PartSet.None with { Servos = new HashSet<int> { servoId } });
        build.DeleteSelectedParts();

        build.Servos.ShouldBeEmpty();
        build.Nodes.Select(node => node.Id).ShouldBe([1, 2, 3]);
        build.Beams.Select(beam => beam.Id).ShouldBe([4, 5]);
    }

    [Fact]
    public void DeleteServo_OnAJointWithNoLinks_LeavesTheJoint()
    {
        var build = ThreeNodes();
        var servoId = build.PlacePart(BuildPart.Servo, _middleJoint)!.Value;

        build.ReplaceSelection(PartSet.None with { Servos = new HashSet<int> { servoId } });
        build.DeleteSelectedParts();

        build.Nodes.Select(node => node.Id).ShouldBe([1, 2, 3]);
    }

    [Fact]
    public void DeleteServo_WithSomeOfItsLinks_LeavesItsJointOnTheOthers()
    {
        var build = TwoBeams();
        var servoId = build.PlacePart(BuildPart.Servo, _middleJoint)!.Value;

        build.ReplaceSelection(PartSet.None with { Beams = new HashSet<int> { 4 }, Servos = new HashSet<int> { servoId } });
        build.DeleteSelectedParts();

        build.Nodes.Select(node => node.Id).ShouldBe([1, 2, 3]);
        build.Beams.Select(beam => beam.Id).ShouldBe([5]);
    }

    [Fact]
    public void DeleteServo_WithEveryLinkOnItsJoint_TakesTheJointToo()
    {
        var build = TwoBeams();
        var servoId = build.PlacePart(BuildPart.Servo, _middleJoint)!.Value;

        build.ReplaceSelection(PartSet.None with { Beams = new HashSet<int> { 4, 5 }, Servos = new HashSet<int> { servoId } });
        build.DeleteSelectedParts();

        build.Nodes.Select(node => node.Id).ShouldBe([1, 3]);
        build.Beams.ShouldBeEmpty();
    }

    [Fact]
    public void DeleteServo_WithTheJointsAroundIt_ClearsTheArea_AndOneUndoBringsItAllBack()
    {
        var build = TwoBeams();
        var servoId = build.PlacePart(BuildPart.Servo, _middleJoint)!.Value;
        var selection = PartSet.None with { Nodes = new HashSet<int> { 1, 3 }, Beams = new HashSet<int> { 4, 5 }, Servos = new HashSet<int> { servoId } };

        build.ReplaceSelection(selection);
        build.DeleteSelectedParts();

        build.Nodes.ShouldBeEmpty();
        build.Servos.ShouldBeEmpty();

        build.Undo();

        build.Nodes.Select(node => node.Id).ShouldBe([1, 2, 3]);
        build.Servos.Select(servo => servo.Id).ShouldBe([servoId]);
        build.Selection.Nodes.ShouldBe([1, 3], ignoreOrder: true);
        build.Selection.Servos.ShouldBe([servoId]);
    }

    [Fact]
    public void DeleteHeldBeam_ClearsServoRole()
    {
        var build = TwoBeams();
        var servoId = build.PlacePart(BuildPart.Servo, _middleJoint)!.Value;

        build.ReplaceSelection(PartSet.None with { Beams = new HashSet<int> { 4 } });
        build.DeleteSelectedParts();

        var servo = build.Snapshot().Servos.Single();
        servo.Id.ShouldBe(servoId);
        servo.FixedLinkId.ShouldBeNull();
        servo.TargetLinkId.ShouldBe(5);
        build.ShowTrainingBlockers();
        build.CanvasNotes().Single(note => note.Target.Id == servoId).Text.ShouldBe(CreatureBuilder.ServoNeedsTwoLinksReason);
        build.ReplaceSelection(PartSet.None with { Servos = new HashSet<int> { servoId } });
        var pickers = new BuildPresentationViewModel(build).SinglePart!.Pickers!;
        pickers[0].Note.ShouldBe(CreatureBuilder.ServoNeedsTwoLinksReason);
        pickers[1].Note.ShouldBeNull();
    }

    [Fact]
    public void DeleteHeldLink_WithTwoLinksLeft_AsksForTheMissingRole()
    {
        var build = new BuildViewModel();
        build.Load(new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(100, 0)), new NodeDef(3, new Vector2D(200, 0)), new NodeDef(4, new Vector2D(100, 100))],
            [new BeamDef(5, 1, 2), new BeamDef(6, 2, 3), new BeamDef(7, 2, 4)],
            [],
            [],
            [],
            [],
            nextPartId: 8));
        var servoId = build.PlacePart(BuildPart.Servo, _middleJoint)!.Value;

        build.ReplaceSelection(PartSet.None with { Beams = new HashSet<int> { 5 } });
        build.DeleteSelectedParts();
        build.ShowTrainingBlockers();

        build.CanvasNotes().Single(note => note.Target.Id == servoId).Text.ShouldBe(UiText.Plain("Pick a Fixed link"));
        build.ReplaceSelection(PartSet.None with { Servos = new HashSet<int> { servoId } });
        var picker = new BuildPresentationViewModel(build).SinglePart!.Pickers![0];
        picker.SelectedIndex.ShouldBeNull();
        picker.Placeholder.ShouldBe(UiText.Plain("Pick a Fixed link"));
        picker.Note.ShouldBeNull();
        picker.LinkIds.ShouldBe([6, 7]);
        picker.Options.ShouldBe([UiText.Format("Beam {0}", 1), UiText.Format("Beam {0}", 2)]);
        picker.LinkKinds.ShouldBe([CreatureElementKind.Beam, CreatureElementKind.Beam]);
    }

    // Picking a link gives the Servo a new id (#911); its Play-tap note stays until both roles are set (#1006).
    [Fact]
    public void AServosPlayTapNote_StaysThroughALinkPick_UntilBothRolesAreSet()
    {
        var build = new BuildViewModel();
        build.Load(new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(100, 0)), new NodeDef(3, new Vector2D(200, 0)), new NodeDef(4, new Vector2D(100, 100))],
            [new BeamDef(5, 1, 2), new BeamDef(6, 2, 3), new BeamDef(7, 2, 4)],
            [],
            [],
            [],
            [],
            nextPartId: 8));
        var servoId = build.PlacePart(BuildPart.Servo, _middleJoint)!.Value;
        build.ReplaceSelection(PartSet.None with { Beams = new HashSet<int> { 5 } });
        build.DeleteSelectedParts();
        build.ShowTrainingBlockers();

        var picked = build.SetServoLink(servoId, fixedRole: false, linkId: 7)!.Value;

        picked.ShouldNotBe(servoId);
        ServoNotes().ShouldHaveSingleItem().ShouldBe(
            new CanvasNote(CanvasNoteKind.Danger, new CreatureElementSelection(CreatureElementKind.Servo, picked), UiText.Plain("Pick a Fixed link")));

        build.SetServoLink(picked, fixedRole: true, linkId: 6);

        ServoNotes().ShouldBeEmpty();

        // Joint 1 lost its beam, so it has its own "Not connected" note.
        IEnumerable<CanvasNote> ServoNotes() => build.CanvasNotes().Where(note => note.Target.Kind == CreatureElementKind.Servo);
    }

    // A Play tap marks each blocker for its own note: a Servo's joint loosened later waits for the next tap (#1006).
    [Fact]
    public void AServosPlayTapMark_DoesNotNoteItsJointLoosenedLater()
    {
        var build = TwoBeams();
        build.PlacePart(BuildPart.Servo, _middleJoint);
        build.ReplaceSelection(PartSet.None with { Beams = new HashSet<int> { 4 } });
        build.DeleteSelectedParts();
        build.ShowTrainingBlockers();

        build.ReplaceSelection(PartSet.None with { Beams = new HashSet<int> { 5 } });
        build.DeleteSelectedParts();

        build.CanvasNotes().Where(note => note.Target.Kind == CreatureElementKind.Node).Select(note => note.Target.Id).ShouldBe([1]);
    }

    [Fact]
    public void DeleteFarEndJoint_ClearsServoRole()
    {
        var build = TwoBeams();
        var servoId = build.PlacePart(BuildPart.Servo, _middleJoint)!.Value;

        build.ReplaceSelection([1]);
        build.DeleteSelectedParts();

        var servo = build.Snapshot().Servos.Single();
        servo.Id.ShouldBe(servoId);
        servo.FixedLinkId.ShouldBeNull();
        servo.TargetLinkId.ShouldBe(5);
        build.ShowTrainingBlockers();
        var note = build.CanvasNotes().Single(note => note.Target.Id == servoId);
        note.Text.ShouldBe(CreatureBuilder.ServoNeedsTwoLinksReason);
        CanvasNoteTargets.JointIds(note.Target, build).ShouldBe([2]);
    }

    [Fact]
    public void DeleteSelectedServo_RemovesIt()
    {
        var build = TwoBeams();
        var servoId = build.PlacePart(BuildPart.Servo, _middleJoint)!.Value;

        build.ReplaceSelection(PartSet.None with { Servos = new HashSet<int> { servoId } });
        build.DeleteSelectedParts();

        build.Snapshot().Servos.ShouldBeEmpty();
        build.CanvasNotes().ShouldBeEmpty();
    }

    [Fact]
    public void PlacePart_Servo_RefusesNonJointAndOccupiedJoint()
    {
        var build = TwoBeams();

        build.PlacePart(BuildPart.Servo, _firstBeam).ShouldBeNull();
        build.PlacementNote.ShouldBe(new CanvasNote(CanvasNoteKind.Danger, _firstBeam, UiText.Plain("Servos go on a joint")));

        build.PlacePart(BuildPart.Servo, _middleJoint).ShouldNotBeNull();
        build.PlacePart(BuildPart.Servo, _middleJoint).ShouldBeNull();
        build.PlacementNote.ShouldBe(new CanvasNote(CanvasNoteKind.Danger, _middleJoint, UiText.Plain("One part per joint")));
    }

    [Fact]
    public void PlacePart_Servo_OnOneLinkJoint_HoldsItAsFixed_AndAsksForASecondLink()
    {
        var build = TwoBeams();

        var servoId = build.PlacePart(BuildPart.Servo, _firstJoint).ShouldNotBeNull();

        build.PlacementNote.ShouldBeNull();
        var servo = build.Servos.Single();
        servo.FixedLinkId.ShouldBe(4);
        servo.TargetLinkId.ShouldBeNull();
        CreatureReadiness.CanTrain(build.Snapshot()).ShouldBeFalse();
        build.CanvasNotes().ShouldBeEmpty();

        build.ShowTrainingBlockers();

        build.CanvasNotes().Single(note => note.Target.Id == servoId).Text.ShouldBe(CreatureBuilder.ServoNeedsTwoLinksReason);
    }

    // Like "Not connected" (#844, #1006): a Servo missing a link is marked on a Play tap, stays
    // marked while it misses one, and the next canvas touch hides it.
    [Fact]
    public void AServoMissingALink_IsNotedOnAPlayTap_UntilFixedOrTheCanvasIsTouched()
    {
        var build = TwoBeams();
        var servoId = build.PlacePart(BuildPart.Servo, _firstJoint)!.Value;

        build.ShowTrainingBlockers();
        build.CanvasNotes().ShouldHaveSingleItem().Target.ShouldBe(new CreatureElementSelection(CreatureElementKind.Servo, servoId));
        build.DismissTapNotes();
        build.CanvasNotes().ShouldBeEmpty();

        build.ShowTrainingBlockers();
        build.ReplaceSelection(PartSet.None with { Servos = new HashSet<int> { servoId } });
        build.DeleteSelectedParts();
        build.CanvasNotes().ShouldBeEmpty();
        build.Undo();
        build.CanvasNotes().ShouldHaveSingleItem().Target.Id.ShouldBe(servoId);
    }

    [Fact]
    public void ALinkReplacingASelectedServosBeam_KeepsTheServoSelected_AndUndoesInOneStep()
    {
        var build = TwoBeams();
        var servoId = build.PlacePart(BuildPart.Servo, _middleJoint)!.Value;
        build.Load(build.Snapshot());
        build.ReplaceSelection(PartSet.None with { Servos = new HashSet<int> { servoId } });
        var presentation = new BuildPresentationViewModel(build);
        var shownIds = new List<int?>();
        presentation.PresentationChanged += (_, _) => shownIds.Add(presentation.SinglePart?.Id);

        var piston = build.ConnectLink(BuildLink.Piston, 1, 2)!.Value;

        build.CanUndo.ShouldBeTrue();
        build.Beams.Select(beam => beam.Id).ShouldBe([5]);
        var moved = build.Servos.Single();
        (moved.FixedLinkId, moved.TargetLinkId).ShouldBe((piston, 5));
        build.SingleSelectedServoId.ShouldBe(moved.Id);
        shownIds.ShouldNotBeEmpty();
        shownIds.ShouldAllBe(id => id == moved.Id);

        build.Undo();

        build.Beams.Select(beam => beam.Id).ShouldBe([4, 5]);
        build.Pistons.ShouldBeEmpty();
        build.Servos.Single().Id.ShouldBe(servoId);
        build.SingleSelectedServoId.ShouldBe(servoId);

        build.Redo();

        build.SingleSelectedServoId.ShouldBe(moved.Id);
    }

    [Fact]
    public void ALinkReplacingASelectedBeam_LeavesNothingOfItSelected()
    {
        var build = TwoBeams();
        build.Load(build.Snapshot());
        build.ReplaceSelection(PartSet.None with { Beams = new HashSet<int> { 4 } });
        var presentation = new BuildPresentationViewModel(build);
        var shownIds = new List<int?>();
        presentation.PresentationChanged += (_, _) => shownIds.Add(presentation.SinglePart?.Id);

        build.ConnectLink(BuildLink.Spring, 2, 1);

        build.CanUndo.ShouldBeTrue();
        build.SelectedPartCount.ShouldBe(0);
        shownIds.ShouldNotBeEmpty();
        shownIds.ShouldAllBe(id => id == null);
    }

    [Fact]
    public void PlacePart_Servo_AndItsSettings_AreUndoSteps()
    {
        var build = TwoBeams();
        var id = build.PlacePart(BuildPart.Servo, _middleJoint)!.Value;

        build.SetParameter(PartParameterId.Range, Math.PI / 2);
        build.CanUndo.ShouldBeTrue();
        build.Undo();
        build.Servos.Single().Range.ShouldBe(ServoDef.DefaultRange);

        build.Undo();
        build.Servos.ShouldBeEmpty();
        build.Redo();
        build.Servos.Single().Id.ShouldBe(id);
    }

    /// <summary>Joints 1 (0,0), 2 (100,0) and 3 (200,0); beam 4 joins 1–2 and beam 5 joins 2–3.</summary>
    private static BuildViewModel TwoBeams()
    {
        var build = new BuildViewModel();
        build.PlaceNode(new Vector2D(0, 0));
        build.PlaceNode(new Vector2D(100, 0));
        build.PlaceNode(new Vector2D(200, 0));
        build.ConnectLink(BuildLink.Beam, 1, 2);
        build.ConnectLink(BuildLink.Beam, 2, 3);
        return build;
    }

    private static BuildViewModel BeamAndPiston()
    {
        var build = ThreeNodes();
        build.ConnectLink(BuildLink.Beam, 1, 2);
        build.ConnectLink(BuildLink.Piston, 2, 3).ShouldBe(5);
        return build;
    }

    private static BuildViewModel BeamAndSpring()
    {
        var build = ThreeNodes();
        build.ConnectLink(BuildLink.Beam, 1, 2);
        build.ConnectLink(BuildLink.Spring, 2, 3).ShouldBe(5);
        return build;
    }

    private static BuildViewModel PistonAndSpring()
    {
        var build = ThreeNodes();
        build.ConnectLink(BuildLink.Piston, 1, 2).ShouldBe(4);
        build.ConnectLink(BuildLink.Spring, 2, 3).ShouldBe(5);
        return build;
    }

    private static BuildViewModel PistonWithLowerIdThanBeam()
    {
        var build = new BuildViewModel();
        build.Load(new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(100, 0)), new NodeDef(5, new Vector2D(200, 0))],
            [new BeamDef(4, 2, 5)],
            [],
            [],
            [new PistonDef(3, 1, 2)],
            [],
            nextPartId: 6));
        return build;
    }

    private static BuildViewModel SpringWithLowerIdThanBeam()
    {
        var build = new BuildViewModel();
        build.Load(new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(100, 0)), new NodeDef(5, new Vector2D(200, 0))],
            [new BeamDef(4, 2, 5)],
            [],
            [],
            [],
            [new SpringDef(3, 1, 2)],
            nextPartId: 6));
        return build;
    }

    /// <summary>Beam 5 joins joints 1 (0,0) and 2 (100,0); a Piston or Spring 6 crosses it at (50,0), from joint 3 (50,-50) to 4 (50,50).</summary>
    private static (BuildViewModel Build, CreatureElementSelection Link) BeamCrossedBy(BuildLink kind)
    {
        var build = new BuildViewModel();
        build.PlaceNode(new Vector2D(0, 0));
        build.PlaceNode(new Vector2D(100, 0));
        build.PlaceNode(new Vector2D(50, -50));
        build.PlaceNode(new Vector2D(50, 50));
        build.ConnectLink(BuildLink.Beam, 1, 2).ShouldBe(5);
        build.ConnectLink(kind, 3, 4).ShouldBe(6);
        build.ClearSelection();
        return (build, new CreatureElementSelection(kind == BuildLink.Piston ? CreatureElementKind.Piston : CreatureElementKind.Spring, 6));
    }

    private static BuildViewModel ThreeNodes()
    {
        var build = new BuildViewModel();
        build.PlaceNode(new Vector2D(0, 0));
        build.PlaceNode(new Vector2D(100, 0));
        build.PlaceNode(new Vector2D(200, 0));
        return build;
    }

    private static Func<int> CountChanges(BuildViewModel build)
    {
        var changes = 0;
        build.AnatomyChanged += (_, _) => changes++;
        return () => changes;
    }
}
