using NodeRunner.App.Builders;
using NodeRunner.App.Lifecycle;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;
using static NodeRunner.App.Tests.ViewModels.BuildFixtures;

namespace NodeRunner.App.Tests.ViewModels;

/// <summary>Dropping a part from the Parts tray onto the drawing, and a Servo's links.</summary>
public sealed class BuildPlacementTests
{
    private static readonly UiText _oneSensorPerBeam = UiText.Plain("One sensor per beam");

    [Fact]
    public void ASensor_GoesOnAnyFreeBeam_WithAFreshId_OrIsRefusedWithANoteWhereItLanded()
    {
        var build = TwoBeams();
        var freshId = build.Snapshot().NextPartId;
        var changes = build.CountAnatomyChanges();
        build.PlacePart(BuildPart.Accelerometer, null).ShouldBeNull();
        (changes(), build.PlacementNote).ShouldBe((0, null));
        build.CanvasNotes().ShouldBeEmpty();

        build.PlacePart(BuildPart.Accelerometer, Beam(4)).ShouldBe(freshId);
        var camera = build.PlacePart(BuildPart.Camera, Beam(5)).ShouldNotBeNull();
        build.Sensors.Select(sensor => (sensor.Id, sensor.BeamId, sensor.Kind, sensor.Aim)).ShouldBe(
            [(freshId, 4, SensorKind.Accelerometer, (double?)null), (camera, 5, SensorKind.Camera, 0)]);
        (changes(), build.PlacementNote).ShouldBe((2, null));

        build.CanPlacePart(BuildPart.Accelerometer, Beam(4), out var taken).ShouldBeFalse();
        taken.ShouldBe(_oneSensorPerBeam);
        build.PlacePart(BuildPart.Accelerometer, Beam(4)).ShouldBeNull();
        build.PlacementNote.ShouldBe(new CanvasNote(CanvasNoteKind.Danger, Beam(4), _oneSensorPerBeam));
        build.PlacePart(BuildPart.Accelerometer, Joint(1)).ShouldBeNull();
        build.PlacementNote.ShouldBe(new CanvasNote(CanvasNoteKind.Danger, Joint(1), UiText.Plain("Accelerometers go on a beam")));
        build.CanvasNotes()[0].ShouldBe(build.PlacementNote);
        build.DismissPlacementNote();
        build.CanvasNotes().ShouldBeEmpty();
        build.PlacePart(BuildPart.Accelerometer, Joint(1));
        build.PlacePart(BuildPart.Accelerometer, null);
        build.PlacementNote.ShouldBeNull();
        build.PlacePart(BuildPart.Accelerometer, Joint(1));
        build.ReplaceSelection([1]);
        build.DeleteSelectedParts();
        build.CanvasNotes().ShouldBeEmpty();

        foreach (var later in new[] { BuildPart.Battery, BuildPart.Pulse })
        {
            build = TwoBeams();
            build.CanPlacePart(later, Beam(4), out var reason).ShouldBeFalse();
            reason.ShouldBe(PartTray.ComingLater);
            build.PlacePart(later, Beam(4)).ShouldBeNull();
            build.Sensors.ShouldBeEmpty();
        }

        build = Loaded(TwoBeamsDef(), locked: true);
        build.PlacePart(BuildPart.Accelerometer, Beam(4)).ShouldBeNull();
        (build.Sensors.Count, build.PlacementNote).ShouldBe((0, null));

        // The reason names the part, so a new kind needs its own sentence.
        foreach (var kind in Enum.GetValues<SensorKind>())
        {
            BuildViewModel.GoesOnABeamReason(kind).Message.ShouldStartWith(kind.ToString());
        }

        (PartTray.SensorKindOf(BuildPart.Camera), PartTray.SensorKindOf(BuildPart.Servo)).ShouldBe((SensorKind.Camera, null));
        (PartTray.IsAvailable(BuildPart.Accelerometer), PartTray.IsAvailable(BuildPart.Battery)).ShouldBe((true, false));
    }

    // The canvas marks these takes and refusals on the parts themselves.
    [Fact]
    public void PlacingTargetsOf_SplitsWhatTakesThePartFromWhatRefusesIt()
    {
        var build = TwoBeams();
        var sensor = build.PlacePart(BuildPart.Accelerometer, Beam(4))!.Value;
        build.PlacePart(BuildPart.Servo, Joint(2)).ShouldNotBeNull();

        var camera = build.PlacingTargetsOf(BuildPart.Camera, null);
        camera.Beams.ShouldBe([5]);
        camera.RefusedBeams.ShouldBe([4]);
        build.PlacePart(BuildPart.Camera, Beam(5)).ShouldNotBeNull();
        var moving = build.PlacingTargetsOf(null, sensor);
        moving.Beams.ShouldBe([4]);
        moving.RefusedBeams.ShouldBe([5]);
        moving.MovingSensor.ShouldBe(sensor);

        build.PlacePart(BuildPart.Wheel, Joint(3)).ShouldNotBeNull();
        // One per slot: a Servo goes beside the Wheel, and a Wheel beside the Servo.
        var servo = build.PlacingTargetsOf(BuildPart.Servo, null);
        servo.Joints.ShouldBe([1, 3], ignoreOrder: true);
        servo.RefusedJoints.ShouldBe([2]);
        servo.JointRingOf(1).ShouldBe(JointMotor.Radius);
        var wheel = build.PlacingTargetsOf(BuildPart.Wheel, null);
        wheel.Joints.ShouldBe([1, 2], ignoreOrder: true);
        wheel.RefusedJoints.ShouldBe([3]);
        wheel.JointRingOf(1).ShouldBe(WheelDef.DefaultRadius);
    }

    // The part drawn on top, which covers every joint's reach, then a beam's reach. A beam
    // that would take the sensor rises alone over its joints, so it is hit there too.
    [Fact]
    public void DropTargetAt_TakesThePartDrawnOnTop_ThenABeamWithinReach_AndAServoAJointInsideItsRing()
    {
        var build = TwoBeams();
        build.PlacePart(BuildPart.Accelerometer, Beam(5));
        var gestures = new BuildGestures(build);
        gestures.DropTargetAt(new Vector2D(5, 9), BuildPart.Accelerometer).ShouldBe(Joint(1));
        gestures.DropTargetAt(new Vector2D(150, 9), BuildPart.Accelerometer).ShouldBe(Beam(5));
        gestures.DropTargetAt(new Vector2D(30, 10), BuildPart.Accelerometer).ShouldBe(Beam(4));
        gestures.DropTargetAt(new Vector2D(300, 300), BuildPart.Accelerometer).ShouldBeNull();

        // A Servo being placed rings every joint, and the ring is where a finger aims.
        var ring = SelectionMarks.JointHalo(JointMotor.Radius);
        gestures.DropTargetAt(new Vector2D(20, 0), BuildPart.Servo).ShouldBe(Joint(1));
        gestures.DropTargetAt(new Vector2D(20, 0), BuildPart.Accelerometer).ShouldBe(Beam(4));
        gestures.DropTargetAt(new Vector2D(0, ring), BuildPart.Servo).ShouldBe(Joint(1));
        gestures.DropTargetAt(new Vector2D(0, ring + 1), BuildPart.Servo).ShouldBeNull();

        // Zoomed in, the beam's finger-sized reach is shorter than the joint's gap.
        gestures.View.ZoomAbout(new Vector2D(0, 0), 2);
        gestures.DropTargetAt(gestures.View.ToView(new Vector2D(-17, 0)), BuildPart.Accelerometer).ShouldBe(Joint(1));

        build = TwoBeams();
        gestures = new BuildGestures(build);
        gestures.DropPart(BuildPart.Accelerometer, new Vector2D(40, 0)).ShouldNotBeNull();
        build.Sensors.Single().BeamId.ShouldBe(4);
        gestures.DropPart(BuildPart.Accelerometer, new Vector2D(0, 0));
        build.PlacementNote.ShouldNotBeNull();
        gestures.Press(new Vector2D(300, 300));
        build.PlacementNote.ShouldBeNull();
    }

    // A Piston or Spring takes no tray part, but is a target so a drop on it says why.
    [Theory]
    [InlineData(BuildPart.Accelerometer, BuildLink.Piston, "Accelerometers go on a beam")]
    [InlineData(BuildPart.Camera, BuildLink.Spring, "Cameras go on a beam")]
    [InlineData(BuildPart.Servo, BuildLink.Piston, "Servos go on a joint")]
    [InlineData(BuildPart.Servo, BuildLink.Spring, "Servos go on a joint")]
    public void APartTappedOrDroppedOnAPistonOrSpring_ChangesNothing_AndNotesWhyThere(BuildPart part, BuildLink kind, string reason)
    {
        // Beam 5 joins joints 1 (0,0) and 2 (100,0); link 6 crosses it at (50,0), from joint 3 (50,-50) to 4 (50,50).
        var build = Loaded(new CreatureDef(
            [
                new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(100, 0)), new NodeDef(3, new Vector2D(50, -50)), new NodeDef(4, new Vector2D(50, 50)),
                new BeamDef(5, 1, 2), kind == BuildLink.Piston ? new PistonDef(6, 3, 4) : new SpringDef(6, 3, 4),
            ]));
        var link = new CreatureElementSelection(kind == BuildLink.Piston ? CreatureElementKind.Piston : CreatureElementKind.Spring, 6);
        build.ActiveTool = BuildTool.Parts;
        var gestures = new BuildGestures(build);
        gestures.DropTargetAt(new Vector2D(50, 0), placing: null).ShouldBe(link);
        gestures.DropTargetAt(new Vector2D(50, 30), BuildPart.Accelerometer).ShouldBe(link);
        // A beam that would take the sensor rises over the link it crosses.
        gestures.DropTargetAt(new Vector2D(50, 0), BuildPart.Accelerometer).ShouldBe(Beam(5));
        gestures.DropTargetAt(new Vector2D(50, -33), BuildPart.Accelerometer).ShouldBe(Joint(3));

        // Off the beam it crosses, which a sensor's target halo or a selected joint raises over it,
        // and off the Servo's target rings.
        var onLink = new Vector2D(50, 15);
        var refused = new CanvasNote(CanvasNoteKind.Danger, link, UiText.Plain(reason));
        build.PickPart(part);
        var changes = build.CountAnatomyChanges();
        gestures.Tap(onLink);
        (changes(), build.PlacementNote, build.PickedPart, build.SelectedPartCount).ShouldBe((0, refused, part, 0));
        build.ReplaceSelection([1]);
        changes = build.CountAnatomyChanges();
        gestures.DropPart(part, onLink).ShouldBeNull();
        build.PlacementNote.ShouldBe(refused);
        build.SelectedNodeIds.ShouldBe([1]);
        (changes(), build.Sensors.Count, build.Servos.Count).ShouldBe((0, 0, 0));
    }

    [Theory]
    [InlineData("beams", 4, 5)]
    [InlineData("beam-piston", 4, 5)]
    [InlineData("beam-spring", 4, 5)]
    [InlineData("piston-spring", 4, 5)]
    [InlineData("piston-before-beam", 3, 4)]
    [InlineData("spring-before-beam", 3, 4)]
    public void AServo_OnAJointWithAnyTwoLinks_HoldsTheLowestIdsAndIsSelected(string links, int fixedLink, int targetLink)
    {
        static IPartDef Link(int id, string kind, int a, int b) => kind switch
        {
            "beam" => new BeamDef(id, a, b),
            "piston" => new PistonDef(id, a, b),
            _ => new SpringDef(id, a, b),
        };
        var kinds = links == "beams" ? ["beam", "beam"] : links.Replace("-before", string.Empty).Split('-');
        var build = Loaded(new CreatureDef(
            [
                new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(100, 0)), new NodeDef(9, new Vector2D(200, 0)),
                Link(fixedLink, kinds[0], 1, 2), Link(targetLink, kinds[1], 2, 9),
            ],
            nextPartId: 10));

        build.PlacePart(BuildPart.Servo, Joint(2)).ShouldBe(10);

        build.Servos.Select(servo => (servo.Id, servo.NodeId, servo.FixedLinkId, servo.TargetLinkId)).ShouldBe([(10, 2, fixedLink, targetLink)]);
        (build.SingleSelectedServoId, build.NodeRadius(2)).ShouldBe((10, JointMotor.Radius));
        new BuildPresentationViewModel(build).SinglePart!.Pickers![0].Options.Select(option => option.LinkId).ShouldBe([fixedLink, targetLink]);
    }

    [Fact]
    public void SwappingAServosLinks_KeepsItsIdSelectionAndPanel_ThroughUndoAndRedo()
    {
        var build = Loaded(PistonAndSpring());
        var servo = build.PlacePart(BuildPart.Servo, Joint(2))!.Value;
        build.Load(build.Snapshot());
        build.SelectOnly(CreatureElementKind.Servo, servo);
        var presentation = new BuildPresentationViewModel(build);
        presentation.SinglePart!.Pickers![0].Options.Select(option => option.Name).ShouldBe([UiText.Format("Piston {0}", 1), UiText.Format("Spring {0}", 1)]);
        var shownIds = new List<int?>();
        presentation.PresentationChanged += (_, _) => shownIds.Add(presentation.SinglePart?.Id);

        build.SetHingePartLink(servo, fixedRole: true, linkId: 5);
        build.Servos.Select(each => (each.Id, each.FixedLinkId, each.TargetLinkId)).ShouldBe([(servo, 5, 4)]);
        (build.SingleSelectedServoId, build.Snapshot().NextPartId, build.CanUndo).ShouldBe((servo, servo + 1, true));
        build.Undo();
        (build.SingleSelectedServoId, build.Servos.Single().FixedLinkId).ShouldBe((servo, 4));
        build.Redo();
        (build.SingleSelectedServoId, build.Servos.Single().FixedLinkId).ShouldBe((servo, 5));
        shownIds.ShouldNotBeEmpty();
        shownIds.ShouldAllBe(id => id == servo);
        build.SelectOnly(CreatureElementKind.Piston, 4);
        presentation.SinglePart!.Id.ShouldBe(4);

        build.ClearSelection();
        build.Undo();
        build.SelectedPartCount.ShouldBe(0);
        build.DeleteSelectedParts();
        build.SelectOnly(CreatureElementKind.Piston, 4);
        build.DeleteSelectedParts();
        build.SelectOnly(CreatureElementKind.Servo, servo);
        build.Undo();
        (build.SelectedServoCount, build.Selection.Pistons.Single()).ShouldBe((0, 4));

        // A Servo placed again under its selected joint is selected instead.
        build = Loaded(PistonAndSpring());
        servo = build.PlacePart(BuildPart.Servo, Joint(2))!.Value;
        build.Undo();
        build.ReplaceSelection([2]);
        build.Redo();
        (build.Selection.Nodes.Count, build.SingleSelectedServoId).ShouldBe((0, servo));
    }

    [Theory]
    [InlineData(new int[0], new int[0], new[] { 1, 2, 3 }, new[] { 4, 5 })]
    [InlineData(new int[0], new[] { 4 }, new[] { 1, 2, 3 }, new[] { 5 })]
    [InlineData(new int[0], new[] { 4, 5 }, new[] { 1, 3 }, new int[0])]
    [InlineData(new[] { 1, 3 }, new[] { 4, 5 }, new int[0], new int[0])]
    public void DeletingAServo_WithSomeOfWhatIsAroundIt_TakesItsJointOnlyWithItsLastLink(int[] joints, int[] beams, int[] jointsLeft, int[] beamsLeft)
    {
        var build = TwoBeams();
        var servo = build.PlacePart(BuildPart.Servo, Joint(2))!.Value;
        var selection = Set([new(CreatureElementKind.Servo, servo), .. joints.Select(Joint), .. beams.Select(Beam)]);
        build.ReplaceSelection(selection);
        build.DeleteSelectedParts();

        (build.Servos.Count, build.CanvasNotes().Count).ShouldBe((0, 0));
        build.Nodes.Select(node => node.Id).ShouldBe(jointsLeft);
        build.Beams.Select(beam => beam.Id).ShouldBe(beamsLeft);
        build.Undo();
        (build.Nodes.Count, build.Beams.Count, build.Servos.Single().Id).ShouldBe((3, 2, servo));
        build.Selection.Parts.ShouldBe(selection.Parts, ignoreOrder: true);

        build = Loaded(new CreatureDef([new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(100, 0))], [], []));
        build.SelectOnly(CreatureElementKind.Servo, build.PlacePart(BuildPart.Servo, Joint(2))!.Value);
        build.DeleteSelectedParts();
        build.Nodes.Count.ShouldBe(2);
    }

    [Fact]
    public void DeletingAHeldLinkOrItsFarJoint_EmptiesItsRole_WhichThePlayTapAndThePickersAskFor()
    {
        foreach (var (creature, deleted) in new[] { (PistonAndSpring(), new CreatureElementSelection(CreatureElementKind.Piston, 4)), (TwoBeamsDef(), Beam(4)), (TwoBeamsDef(), Joint(1)) })
        {
            var build = Loaded(creature);
            var servo = build.PlacePart(BuildPart.Servo, Joint(2))!.Value;
            build.ReplaceSelection(Set(deleted));
            build.DeleteSelectedParts();
            build.Servos.Select(each => (each.Id, each.FixedLinkId, each.TargetLinkId)).ShouldBe([(servo, (int?)null, (int?)5)]);
            build.ShowTrainingBlockers();
            var note = build.CanvasNotes().Single(note => note.Target.Id == servo);
            note.Text.ShouldBe(CreatureBuilder.ServoNeedsTwoSidesReason);
            CanvasNoteTargets.JointIds(note.Target, build).ShouldBe([2]);
            build.SelectOnly(CreatureElementKind.Servo, servo);
            new BuildPresentationViewModel(build).SinglePart!.Pickers!.Select(picker => picker.Note).ShouldBe([CreatureBuilder.ServoNeedsTwoSidesReason, null]);
        }

        // With a third link on its joint, a Servo asks for the role it lost.
        var three = Loaded(ThreeBeams());
        var held = three.PlacePart(BuildPart.Servo, Joint(2))!.Value;
        three.SelectOnly(CreatureElementKind.Beam, 5);
        three.DeleteSelectedParts();
        three.ShowTrainingBlockers();
        three.CanvasNotes().Single(note => note.Target.Id == held).Text.ShouldBe(UiText.Plain("Pick a Fixed part"));
        three.SelectOnly(CreatureElementKind.Servo, held);
        var picker = new BuildPresentationViewModel(three).SinglePart!.Pickers![0];
        (picker.SelectedIndex, picker.Placeholder, picker.Note).ShouldBe((null, UiText.Plain("Pick a Fixed part"), null));
        picker.Options.Select(option => (option.LinkId, option.Name)).ShouldBe([(6, UiText.Format("Beam {0}", 1)), (7, UiText.Format("Beam {0}", 2))]);

        var four = Loaded(new CreatureDef([.. ThreeBeams().Parts, new NodeDef(8, new Vector2D(100, -100)), new BeamDef(9, 2, 8)], nextPartId: 10));
        held = four.PlacePart(BuildPart.Servo, Joint(2))!.Value;
        four.ReplaceSelection(Set(Beam(5), Beam(6)));
        four.DeleteSelectedParts();
        four.ShowTrainingBlockers();
        four.CanvasNotes().Single(note => note.Target.Id == held).Text.ShouldBe(UiText.Plain("Pick Fixed and Target parts"));
        four.SelectOnly(CreatureElementKind.Servo, held);
        new BuildPresentationViewModel(four).SinglePart!.Pickers!.Select(each => (each.SelectedIndex, each.Placeholder)).ShouldBe(
            [(null, UiText.Plain("Pick a Fixed part")), (null, UiText.Plain("Pick a Target part"))]);
    }

    // Only the role links carry a role, and only the other role's link swaps; with one role empty,
    // picking the other role's link moves it rather than swapping.
    [Fact]
    public void ServoPickers_GiveEachLinkItsRole_AndMarkASwapOnlyWhileBothRolesAreSet()
    {
        var build = Loaded(ThreeBeams());
        var servo = build.PlacePart(BuildPart.Servo, Joint(2))!.Value;
        build.SetHingePartLink(servo, fixedRole: true, linkId: 6);
        build.SetHingePartLink(servo, fixedRole: false, linkId: 7);
        var swaps = UiText.Plain("swaps");
        var pickers = new BuildPresentationViewModel(build).SinglePart!.Pickers!;
        pickers[0].Options.Select(option => (option.LinkId, option.Role, option.Note)).ShouldBe(
            [(5, ServoLinkRole.None, null), (6, ServoLinkRole.Fixed, null), (7, ServoLinkRole.Target, swaps)]);
        pickers[1].Options.Select(option => (option.LinkId, option.Role, option.Note)).ShouldBe(
            [(5, ServoLinkRole.None, null), (6, ServoLinkRole.Fixed, swaps), (7, ServoLinkRole.Target, null)]);

        build = Loaded(ThreeBeams());
        servo = build.PlacePart(BuildPart.Servo, Joint(2))!.Value;
        build.SelectOnly(CreatureElementKind.Beam, 5);
        build.DeleteSelectedParts();
        build.SelectOnly(CreatureElementKind.Servo, servo);
        foreach (var picker in new BuildPresentationViewModel(build).SinglePart!.Pickers!)
        {
            picker.Options.Select(option => (option.LinkId, option.Role, option.Note)).ShouldBe([(6, ServoLinkRole.Target, null), (7, ServoLinkRole.None, null)]);
        }
    }

    // Like "Not connected": a Servo missing a link is marked on a Play tap, stays
    // marked while it misses one, and the next canvas touch hides it. The mark is the Servo's own.
    [Fact]
    public void AServoMissingALink_IsNotedOnAPlayTap_UntilFixedOrTheCanvasIsTouched()
    {
        var build = TwoBeams();
        var servo = build.PlacePart(BuildPart.Servo, Joint(1)).ShouldNotBeNull();
        (build.PlacementNote, build.Servos.Single().FixedLinkId, build.Servos.Single().TargetLinkId).ShouldBe((null, 4, null));
        CreatureReadiness.CanTrain(build.Snapshot()).ShouldBeFalse();
        build.CanvasNotes().ShouldBeEmpty();
        var marked = new CanvasNote(CanvasNoteKind.Danger, new(CreatureElementKind.Servo, servo), CreatureBuilder.ServoNeedsTwoSidesReason);
        build.ShowTrainingBlockers();
        build.CanvasNotes().ShouldBe([marked]);
        build.DismissTapNotes();
        build.CanvasNotes().ShouldBeEmpty();

        build.ShowTrainingBlockers();
        build.SelectOnly(CreatureElementKind.Servo, servo);
        build.DeleteSelectedParts();
        build.CanvasNotes().ShouldBeEmpty();
        build.Undo();
        build.CanvasNotes().ShouldBe([marked]);
        build.SelectOnly(CreatureElementKind.Servo, servo);
        build.DeleteSelectedParts();
        var newServo = build.PlacePart(BuildPart.Servo, Joint(1))!.Value;
        newServo.ShouldNotBe(servo);
        CreatureReadiness.IsMissingALink(build.Servos.Single()).ShouldBeTrue();
        build.CanvasNotes().ShouldBeEmpty();
        build.Undo();
        build.Undo();
        build.CanvasNotes().ShouldBe([marked]);

        // The note stays through a link pick until both roles are set, and a joint loosened later waits for the next tap.
        build = Loaded(ThreeBeams());
        servo = build.PlacePart(BuildPart.Servo, Joint(2))!.Value;
        build.SelectOnly(CreatureElementKind.Beam, 5);
        build.DeleteSelectedParts();
        build.ShowTrainingBlockers();
        build.SetHingePartLink(servo, fixedRole: false, linkId: 7);
        build.CanvasNotes().Where(note => note.Target.Kind == CreatureElementKind.Servo).ShouldBe(
            [new CanvasNote(CanvasNoteKind.Danger, new(CreatureElementKind.Servo, servo), UiText.Plain("Pick a Fixed part"))]);
        build.SetHingePartLink(servo, fixedRole: true, linkId: 6);
        build.CanvasNotes().Where(note => note.Target.Kind == CreatureElementKind.Servo).ShouldBeEmpty();

        build = TwoBeams();
        build.PlacePart(BuildPart.Servo, Joint(2));
        build.SelectOnly(CreatureElementKind.Beam, 4);
        build.DeleteSelectedParts();
        build.ShowTrainingBlockers();
        build.SelectOnly(CreatureElementKind.Beam, 5);
        build.DeleteSelectedParts();
        build.CanvasNotes().Where(note => note.Target.Kind == CreatureElementKind.Node).Select(note => note.Target.Id).ShouldBe([1]);
    }

    [Fact]
    public void ALinkReplacingASelectedServosBeam_KeepsTheServoSelected_ButASelectedBeamGoesWithIt()
    {
        var build = TwoBeams();
        var servo = build.PlacePart(BuildPart.Servo, Joint(2))!.Value;
        build.Load(build.Snapshot());
        build.SelectOnly(CreatureElementKind.Servo, servo);
        var presentation = new BuildPresentationViewModel(build);
        var shownIds = new List<int?>();
        presentation.PresentationChanged += (_, _) => shownIds.Add(presentation.SinglePart?.Id);

        var piston = build.ConnectLink(BuildLink.Piston, 1, 2)!.Value;
        build.Beams.Select(beam => beam.Id).ShouldBe([5]);
        build.Servos.Select(each => (each.Id, each.FixedLinkId, each.TargetLinkId)).ShouldBe([(servo, piston, 5)]);
        (build.SingleSelectedServoId, build.CanUndo).ShouldBe((servo, true));
        shownIds.ShouldNotBeEmpty();
        shownIds.ShouldAllBe(id => id == servo);
        build.Undo();
        (build.Beams.Count, build.Pistons.Count, build.SingleSelectedServoId).ShouldBe((2, 0, servo));
        build.Redo();
        (build.Servos.Single().FixedLinkId, build.SingleSelectedServoId).ShouldBe((piston, servo));

        build = TwoBeams();
        build.SelectOnly(CreatureElementKind.Beam, 4);
        presentation = new BuildPresentationViewModel(build);
        shownIds.Clear();
        presentation.PresentationChanged += (_, _) => shownIds.Add(presentation.SinglePart?.Id);
        build.ConnectLink(BuildLink.Spring, 2, 1);
        build.SelectedPartCount.ShouldBe(0);
        shownIds.ShouldNotBeEmpty();
        shownIds.ShouldAllBe(id => id == null);
    }

    /// <summary>Joints 1 (0,0), 2 (100,0) and 3 (200,0); Piston 4 joins 1–2 and Spring 5 joins 2–3.</summary>
    private static CreatureDef PistonAndSpring() => new(
        [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(100, 0)), new NodeDef(3, new Vector2D(200, 0)), new PistonDef(4, 1, 2), new SpringDef(5, 2, 3)],
        nextPartId: 6);

    /// <summary>Joints 1 (0,0), 2 (100,0), 3 (200,0) and 4 (100,100); beams 5, 6 and 7 join joint 2 to 1, 3 and 4.</summary>
    private static CreatureDef ThreeBeams() => new(
        [
            new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(100, 0)), new NodeDef(3, new Vector2D(200, 0)), new NodeDef(4, new Vector2D(100, 100)),
            new BeamDef(5, 1, 2), new BeamDef(6, 2, 3), new BeamDef(7, 2, 4),
        ],
        nextPartId: 8);
}
