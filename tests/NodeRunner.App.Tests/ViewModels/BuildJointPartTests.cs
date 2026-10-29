using NodeRunner.App.Builders;
using NodeRunner.App.Lifecycle;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;
using NodeRunner.ML;
using static NodeRunner.App.Tests.ViewModels.BuildFixtures;

namespace NodeRunner.App.Tests.ViewModels;

/// <summary>
/// The parts that sit on a joint: the hinge parts (Servo, Stepper, Motor, Brake, Angle sensor), which take a
/// Fixed and a Target side, and the Wheel, Rubber and Touch sensor. Each is placed, shown, renamed, copied,
/// deleted and undone alike, stacks on its joint in a fixed order, and a locked Creation keeps those with
/// network ports.
/// </summary>
public sealed class BuildJointPartTests
{
    private const string _oneHingePart = "One Servo, Stepper, Motor, Brake or Angle sensor per joint";
    private static readonly BuildPart[] _hingeParts = [BuildPart.Servo, BuildPart.Stepper, BuildPart.VelocityMotor, BuildPart.Brake, BuildPart.AngleSensor];

    public static TheoryData<BuildPart, string, string, PartParameterId[], PartParameterId[]> Panels => new()
    {
        { BuildPart.Servo, "Servo", "Servo {0}", [PartParameterId.ServoStrength, PartParameterId.Range], [PartParameterId.ServoStartPosition, PartParameterId.AngularMaxSpeed, PartParameterId.RiseTime] },
        { BuildPart.Stepper, "Stepper", "Stepper {0}", [PartParameterId.ServoStrength, PartParameterId.StepsPerTurn, PartParameterId.MaxFrequency], [] },
        { BuildPart.VelocityMotor, "Motor", "Motor {0}", [PartParameterId.MotorStrength, PartParameterId.MotorMaxSpeed], [PartParameterId.RiseTime] },
        { BuildPart.Brake, "Brake", "Brake {0}", [PartParameterId.BrakeStrength], [] },
        { BuildPart.AngleSensor, "Angle sensor", "Angle {0}", [], [] },
        { BuildPart.Wheel, "Wheel", "Wheel {0}", [PartParameterId.WheelRadius], [] },
        { BuildPart.Rubber, "Rubber", "Rubber {0}", [], [] },
        { BuildPart.TouchSensor, "Touch sensor", "Touch {0}", [], [] },
    };

    // A placing ring marks a joint at its halo; past an Angle sensor's arc it is 28 on a bare joint.
    public static TheoryData<BuildPart, string, string, double> Refusals => new()
    {
        { BuildPart.Servo, "Servos go on a joint", _oneHingePart, SelectionMarks.JointHalo(JointMotor.Radius) },
        { BuildPart.Stepper, "Steppers go on a joint", _oneHingePart, SelectionMarks.JointHalo(JointMotor.Radius) },
        { BuildPart.VelocityMotor, "Motors go on a joint", _oneHingePart, SelectionMarks.JointHalo(JointMotor.Radius) },
        { BuildPart.Brake, "Brakes go on a joint", _oneHingePart, NodeDef.HubRadius + SelectionMarks.Gap },
        { BuildPart.AngleSensor, "Angle sensors go on a joint", _oneHingePart, 28 },
        { BuildPart.Wheel, "Wheels go on a joint", "One wheel per joint", SelectionMarks.JointHalo(WheelDef.DefaultRadius) },
        { BuildPart.Rubber, "Rubber goes on a joint", "One rubber per joint", SelectionMarks.JointHalo(NodeDef.PlainJointRadius + RubberDef.Skin) },
        { BuildPart.TouchSensor, "Touch sensors go on a joint", "One Touch sensor per joint", SelectionMarks.JointHalo(NodeDef.PlainJointRadius + TouchSensorDef.Cushion) },
    };

    [Theory]
    [MemberData(nameof(Panels))]
    public void AJointPart_IsPlacedShownRenamedCopiedAndDeleted_EachAnUndoStep(
        BuildPart part, string title, string defaultName, PartParameterId[] basic, PartParameterId[] advanced)
    {
        var build = TwoBeams();
        var id = build.PlacePart(part, Joint(2)).ShouldNotBeNull();
        var placed = Element(part, id);
        var hinge = _hingeParts.Contains(part);

        build.PlacementNote.ShouldBeNull();
        build.Selection.Parts.ShouldBe([placed]);
        build.JointPartsAt(2).ShouldBe([placed]);
        if (hinge)
        {
            Sides(build, id).ShouldBe((4, 5));
        }

        CreatureReadiness.CanTrain(build.Snapshot()).ShouldBeTrue();
        build.Undo();
        build.JointPartsAt(2).ShouldBeEmpty();
        build.ReplaceSelection(PartSet.Of(Joint(2)));
        build.Redo();
        build.Selection.Parts.ShouldBe([placed]);

        var panel = new BuildPresentationViewModel(build).SinglePart!;
        panel.Kind.ShouldBe(Enum.Parse<PartKind>(part.ToString()));
        panel.Title.ShouldBe(UiText.Plain(title));
        panel.Note.ShouldBe(NoteOf(part));
        panel.Name.ShouldBe(UiText.Format(defaultName, 1));
        panel.DefaultName.ShouldBe(panel.Name);
        panel.CanDelete.ShouldBeTrue();
        panel.BasicSettings.Select(slider => slider.Id).ShouldBe(basic);
        panel.AdvancedSettings.Select(slider => slider.Id).ShouldBe(advanced);
        panel.OnThisJoint.ShouldBeNull();
        panel.ConnectionsLabel.ShouldBeNull();
        if (hinge)
        {
            panel.Pickers!.Select(picker => (picker.Label, picker.SelectedIndex, picker.Note)).ShouldBe(
                [(UiText.Plain("Fixed part"), 0, null), (UiText.Plain("Target part"), 1, null)]);
        }

        build.RenamePart(id, "Knee", shownDefault: null);
        build.PartDisplayName(id).ShouldBe(UiText.AsWritten("Knee"));

        build.ReplaceSelection(Set(Joint(1), Joint(3), Beam(4), Beam(5), placed));
        build.CanCopySelection.ShouldBeTrue();
        build.CopySelectedParts();
        var copy = JointPart(build, part, other: id);
        copy.NodeId.ShouldBe(build.Nodes[4].Id);
        copy.Name.ShouldBeNull();
        if (hinge)
        {
            Sides(build, copy.Id).ShouldBe((build.Beams[2].Id, build.Beams[3].Id));
        }

        build.Selection.Parts.ShouldBe(
            [Joint(build.Nodes[3].Id), Joint(build.Nodes[5].Id), Beam(build.Beams[2].Id), Beam(build.Beams[3].Id), Element(part, copy.Id)],
            ignoreOrder: true);
        build.Undo();

        build.ReplaceSelection(PartSet.Of(placed));
        build.DeleteSelectedParts();
        build.JointPartsAt(2).ShouldBeEmpty();
        (build.Nodes.Count, build.Beams.Count, build.SelectedPartCount).ShouldBe((3, 2, 0));
        build.Undo();
        build.Selection.Parts.ShouldBe([placed]);
        build.Snapshot().Parts.Single(other => other.Id == id).Name.ShouldBe("Knee");
        build.Undo();
        build.Snapshot().Parts.Single(other => other.Id == id).Name.ShouldBeNull();

        // A joint holding a part is not bare, so a delete of its links keeps it; the part goes with its joint.
        build.ReplaceSelection(Set(Beam(4), Beam(5)));
        build.DeleteSelectedParts();
        build.JointPartsAt(2).ShouldBe([placed]);
        if (hinge)
        {
            Sides(build, id).ShouldBe((null, null));
        }

        build.Undo();
        build.SelectOnly(CreatureElementKind.Node, 2);
        build.DeleteSelectedParts();
        build.Snapshot().Parts.ShouldNotContain(other => other.Id == id);
        build.Undo();
        build.ReplaceSelection(Set(Beam(4), Beam(5), placed));
        build.DeleteSelectedParts();
        build.Nodes.Select(node => node.Id).ShouldBe([1, 3]);
    }

    [Theory]
    [MemberData(nameof(Refusals))]
    public void AJointPart_GoesOnlyOnAJoint_OnePerSlot_WhereItsPlacingRingsShow(BuildPart part, string onAJoint, string onePerSlot, double halo)
    {
        var build = TwoBeams();

        build.PlacePart(part, Beam(4)).ShouldBeNull();
        build.PlacementNote.ShouldBe(new CanvasNote(CanvasNoteKind.Danger, Beam(4), UiText.Plain(onAJoint)));
        build.PlacePart(part, Joint(2)).ShouldNotBeNull();
        build.CanPlacePart(part, Joint(2), out var reason).ShouldBeFalse();
        reason.ShouldBe(UiText.Plain(onePerSlot));
        build.PlacePart(part, Joint(2)).ShouldBeNull();
        build.PlacementNote.ShouldBe(new CanvasNote(CanvasNoteKind.Danger, Joint(2), UiText.Plain(onePerSlot)));
        build.JointPartsAt(2).ShouldHaveSingleItem();

        var targets = build.PlacingTargetsOf(part, movingSensorId: null);
        targets.Joints.ShouldBe([1, 3], ignoreOrder: true);
        targets.RefusedJoints.ShouldBe([2]);
        SelectionMarks.JointHalo(targets.JointRingOf(1)).ShouldBe(halo);

        build.ActiveTool = BuildTool.Parts;
        build.PickPart(part);
        var gestures = new BuildGestures(build);
        gestures.DropTargetAt(new Vector2D(-BuildGestures.BeamHitDistance, -halo - 1), part).ShouldBeNull();
        gestures.Tap(new Vector2D(0, -halo + 1));
        build.JointPartsAt(1).ShouldHaveSingleItem().Kind.ShouldBe(Element(part, 1).Kind);
        build.PickedPart.ShouldBe(part);
    }

    [Theory]
    [InlineData(BuildPart.Servo, "A Servo needs two links, or a link and a Wheel")]
    [InlineData(BuildPart.Stepper, "A Stepper needs two links, or a link and a Wheel")]
    [InlineData(BuildPart.VelocityMotor, "A Motor needs two links, or a link and a Wheel")]
    [InlineData(BuildPart.Brake, "A Brake needs two links, or a link and a Wheel")]
    [InlineData(BuildPart.AngleSensor, "An Angle sensor needs two links, or a link and a Wheel")]
    public void AHingePart_HoldsItsJointsHingeSlot_AndTakesTwoSides_OrALinkAndAWheel(BuildPart part, string needsTwoSides)
    {
        var build = TwoBeams();
        var id = build.PlacePart(part, Joint(2))!.Value;
        foreach (var other in _hingeParts)
        {
            build.CanPlacePart(other, Joint(2), out var reason).ShouldBeFalse();
            reason.ShouldBe(UiText.Plain(_oneHingePart));
        }

        build.SetHingePartLink(id, fixedRole: true, linkId: 5);
        Sides(build, id).ShouldBe((5, 4));
        build.Selection.Parts.ShouldBe([Element(part, id)]);
        build.Snapshot().NextPartId.ShouldBe(id + 1);
        new BuildPresentationViewModel(build).SinglePart!.Pickers!.Select(picker => picker.SelectedIndex).ShouldBe([1, 0]);
        build.Undo();
        Sides(build, id).ShouldBe((4, 5));
        build.Redo();
        Sides(build, id).ShouldBe((5, 4));
        build.ActiveTool = BuildTool.Select;
        build.ClearSelection();
        new BuildGestures(build).DragAcross(new Vector2D(70, -70), new Vector2D(130, 70));
        build.Selection.Parts.ShouldBe([Element(part, id)]);
        build.DrawnPartAt(new Vector2D(100, 0), PlacingTargets.None).ShouldBe(Joint(2));

        var wheel = build.PlacePart(BuildPart.Wheel, Joint(1))!.Value;
        var end = build.PlacePart(part, Joint(1))!.Value;
        Sides(build, end).ShouldBe((4, wheel));
        CreatureReadiness.CanTrain(build.Snapshot()).ShouldBeTrue();
        var panel = new BuildPresentationViewModel(build).SinglePart!;
        panel.OnThisJoint!.Select(tab => tab.Part).ShouldBe([Element(part, end), new CreatureElementSelection(CreatureElementKind.Wheel, wheel)]);
        panel.OnThisJointIndex.ShouldBe(0);
        panel.Pickers![1].Options.Select(option => (option.LinkId, option.Kind)).ShouldContain((wheel, CreatureElementKind.Wheel));
        build.SetHingePartLink(end, fixedRole: true, linkId: wheel);
        Sides(build, end).ShouldBe((wheel, 4));
        build.Undo();
        var otherJointsWheel = build.PlacePart(BuildPart.Wheel, Joint(3))!.Value;
        Should.Throw<ArgumentException>(() => build.SetHingePartLink(end, fixedRole: false, linkId: otherJointsWheel));

        build.SelectOnly(CreatureElementKind.Wheel, wheel);
        build.DeleteSelectedParts();
        Sides(build, end).ShouldBe((4, null));
        CreatureReadiness.CanTrain(build.Snapshot()).ShouldBeFalse();
        build.ShowTrainingBlockers();
        build.CanvasNotes().Single(note => note.Target.Id == end).Text.ShouldBe(UiText.Plain(needsTwoSides));
        build.SelectOnly(Element(part, end).Kind, end);
        new BuildPresentationViewModel(build).SinglePart!.Pickers!.Select(picker => picker.Note).ShouldBe([null, UiText.Plain(needsTwoSides)]);
        build.Undo();
        Sides(build, end).ShouldBe((4, wheel));
    }

    [Theory]
    [InlineData(BuildPart.Servo, true)]
    [InlineData(BuildPart.Stepper, true)]
    [InlineData(BuildPart.VelocityMotor, true)]
    [InlineData(BuildPart.AngleSensor, true)]
    [InlineData(BuildPart.TouchSensor, true)]
    [InlineData(BuildPart.Brake, false)]
    [InlineData(BuildPart.Wheel, false)]
    [InlineData(BuildPart.Rubber, false)]
    public void Locked_KeepsAJointPartWithNetworkPorts_AndEditsOneWithout(BuildPart part, bool hasPorts)
    {
        var open = TwoBeams();
        var id = open.PlacePart(part, Joint(2))!.Value;
        var placed = Element(part, id);
        var build = Loaded(open.Snapshot(), locked: true);
        var refused = hasPorts ? BuildViewModel.LockedReason : null;

        build.CanPlacePart(part, Joint(1), out var reason).ShouldBe(!hasPorts);
        reason.ShouldBe(refused);
        (build.PlacePart(part, Joint(1)) is not null).ShouldBe(!hasPorts);
        build.PlacementNote.ShouldBeNull();
        build.ReplaceSelection(Set(Joint(1), Joint(3), Beam(4), Beam(5), placed));
        build.CopyBlockers().ShouldBe(hasPorts ? [new CanvasNote(CanvasNoteKind.Danger, placed, UiText.Plain(LockedText))] : []);
        build.CanCopySelection.ShouldBe(!hasPorts);

        if (_hingeParts.Contains(part))
        {
            build.SetHingePartLink(id, fixedRole: false, linkId: 4);
            Sides(build, id).ShouldBe((5, 4));
            build.SelectOnly(CreatureElementKind.Beam, 4);
            build.DeleteLockedReason.ShouldBe(refused);
        }

        build.SelectOnly(CreatureElementKind.Node, 2);
        build.DeleteLockedReason.ShouldBe(refused);
        build.ReplaceSelection(PartSet.Of(placed));
        build.DeleteLockedReason.ShouldBe(refused);
        new BuildPresentationViewModel(build).SinglePart!.CanDelete.ShouldBe(!hasPorts);
        build.IsLocked.ShouldBeTrue();
    }

    [Fact]
    public void AStack_KeepsItsSlotOrder_StepsDownByTaps_AndBoxesTabsAndCopiesItsParts()
    {
        var build = TwoBeams();
        var touch = new CreatureElementSelection(CreatureElementKind.TouchSensor, build.PlacePart(BuildPart.TouchSensor, Joint(2))!.Value);
        var rubber = new CreatureElementSelection(CreatureElementKind.Rubber, build.PlacePart(BuildPart.Rubber, Joint(2))!.Value);
        var wheel = new CreatureElementSelection(CreatureElementKind.Wheel, build.PlacePart(BuildPart.Wheel, Joint(2))!.Value);
        var servo = new CreatureElementSelection(CreatureElementKind.Servo, build.PlacePart(BuildPart.Servo, Joint(2))!.Value);
        build.JointPartsAt(2).ShouldBe([servo, wheel, rubber, touch]);
        build.NodeRadius(2).ShouldBe(WheelDef.DefaultRadius);
        build.ActiveTool = BuildTool.Select;
        build.ClearSelection();
        var gestures = new BuildGestures(build);
        build.DrawnPartAt(new Vector2D(100, 0), PlacingTargets.None).ShouldBe(Joint(2));

        List<CreatureElementSelection[]> taps = [];
        for (var tap = 0; tap < 5; tap++)
        {
            gestures.Tap(new Vector2D(100, 0));
            taps.Add([.. build.Selection.Parts]);
        }

        taps.ShouldBe([[servo], [wheel], [rubber], [touch], []]);
        build.ReplaceSelection(Set(servo, wheel, rubber, touch));
        gestures.Tap(new Vector2D(100, 0));
        build.SelectedPartCount.ShouldBe(0);
        build.ReplaceSelection(Set(servo, Beam(4)));
        gestures.Tap(new Vector2D(100, 0));
        build.Selection.Parts.ShouldBe([Beam(4), wheel], ignoreOrder: true);

        build.ClearSelection();
        gestures.DragAcross(new Vector2D(70, -70), new Vector2D(130, 70));
        build.Selection.Parts.ShouldBe([servo, wheel, rubber, touch], ignoreOrder: true);
        new BuildPresentationViewModel(build).SinglePart.ShouldBeNull();

        build.ReplaceSelection(PartSet.Of(rubber));
        var panel = new BuildPresentationViewModel(build).SinglePart!;
        panel.OnThisJoint!.ShouldBe([
            new JointPartTab(PartKind.Servo, servo), new JointPartTab(PartKind.Wheel, wheel),
            new JointPartTab(PartKind.Rubber, rubber), new JointPartTab(PartKind.TouchSensor, touch)]);
        panel.OnThisJointIndex.ShouldBe(2);
        var raised = build.RaisedParts(PartSet.Of(touch), PlacingTargets.None);
        raised.Joints.ShouldBe([1, 2, 3], ignoreOrder: true);
        raised.Links.ShouldBe([4, 5], ignoreOrder: true);
        CanvasNoteTargets.JointIds(rubber, build).ShouldBe([2]);
        CanvasNoteTargets.JointIds(touch, build).ShouldBe([2]);

        build.ClearSelection();
        gestures.DragAcross(new Vector2D(100, 0), new Vector2D(100, 40));
        build.Nodes[1].Position.ShouldBe(new Vector2D(100, 40));
        build.Selection.Parts.ShouldBe([servo]);
        build.ReplaceSelection(PartSet.Of(wheel));
        gestures.DragAcross(new Vector2D(100, 40), new Vector2D(100, 0));
        build.Nodes[1].Position.ShouldBe(new Vector2D(100, 0));
        build.Selection.Parts.ShouldBe([wheel]);

        build.ReplaceSelection(Set(Joint(1), Joint(3), Beam(4), Beam(5), servo, rubber));
        build.CopySelectedParts();
        var copiedJoint = build.Servos[1].NodeId;
        build.Rubbers.Select(part => part.NodeId).ShouldBe([2, copiedJoint]);
        build.Wheels.Concat<IJointPartDef>(build.TouchSensors).Select(part => part.NodeId).ShouldBe([2, 2]);
        build.NodeRadius(copiedJoint).ShouldBe(JointMotor.Radius);

        build.ReplaceSelection(Set(Beam(4), Beam(5), servo));
        build.DeleteSelectedParts();
        build.Nodes.ShouldContain(node => node.Id == 2);
        build.JointPartsAt(2).ShouldBe([wheel, rubber, touch]);
    }

    [Fact]
    public void PlacingRings_OnAnOccupiedJoint_ClearWhatItAlreadyDraws_AndTheRubbersSkinAddsNoTapArea()
    {
        var build = Loaded(TwoBeamsDef(new AngleSensorDef(6, 2, fixedLinkId: 4, targetLinkId: 5)));
        foreach (var refused in new[] { BuildPart.AngleSensor, BuildPart.Servo, BuildPart.Brake })
        {
            SelectionMarks.JointHalo(build.PlacingTargetsOf(refused, movingSensorId: null).RefusedRingOf(2)).ShouldBe(28);
        }

        new[] { BuildPart.Rubber, BuildPart.TouchSensor, BuildPart.Wheel }
            .Select(part => SelectionMarks.JointHalo(build.PlacingTargetsOf(part, movingSensorId: null).JointRingOf(2)))
            .ShouldBe([31, 33, 43]);
        SelectionMarks.JointHalo(build.RingRadiusAt(2, build.NodeRadius(2))).ShouldBe(28);
        build.RingRadiusAt(1, build.NodeRadius(1)).ShouldBe(NodeDef.PlainJointRadius);
        build.PlacePart(BuildPart.TouchSensor, Joint(2));
        SelectionMarks.JointHalo(build.RingRadiusAt(2, build.NodeRadius(2))).ShouldBe(33);
        build.PlacePart(BuildPart.Rubber, Joint(1)).ShouldNotBeNull();
        SelectionMarks.JointHalo(build.RingRadiusAt(2, build.NodeRadius(2))).ShouldBe(33);

        build = TwoBeams();
        build.PlacePart(BuildPart.Servo, Joint(1));
        build.PlacePart(BuildPart.Wheel, Joint(3));
        var touch = build.PlacingTargetsOf(BuildPart.TouchSensor, movingSensorId: null);
        touch.JointRingOf(1).ShouldBe(JointMotor.Radius + TouchSensorDef.Cushion);
        touch.JointRingOf(3).ShouldBe(WheelDef.DefaultRadius + TouchSensorDef.Cushion);
        build.PlacingTargetsOf(BuildPart.Rubber, movingSensorId: null).JointRingOf(3).ShouldBe(WheelDef.DefaultRadius + RubberDef.Skin);
        build.PlacingTargetsOf(BuildPart.Servo, movingSensorId: null).JointRingOf(3).ShouldBe(JointMotor.Radius);
        var gestures = new BuildGestures(build);
        var edge = SelectionMarks.JointHalo(touch.JointRingOf(3));
        gestures.DropTargetAt(new Vector2D(200, edge), BuildPart.TouchSensor).ShouldBe(Joint(3));
        gestures.DropTargetAt(new Vector2D(200, edge + 1), BuildPart.TouchSensor).ShouldBeNull();
        gestures.DropTargetAt(new Vector2D(165, 0), BuildPart.Accelerometer).ShouldBe(Beam(5));
        gestures.DropPart(BuildPart.Wheel, new Vector2D(100, 30)).ShouldNotBeNull();
        build.Wheels.Select(wheel => wheel.NodeId).ShouldBe([3, 2]);

        var bare = TwoBeams();
        bare.PlacePart(BuildPart.Wheel, Joint(2));
        var gripping = TwoBeams();
        gripping.PlacePart(BuildPart.Wheel, Joint(2));
        gripping.PlacePart(BuildPart.Rubber, Joint(2));
        for (var distance = 0; distance <= WheelDef.DefaultRadius + 20; distance++)
        {
            var position = new Vector2D(100, distance);
            gripping.DrawnPartAt(position, PlacingTargets.None).ShouldBe(bare.DrawnPartAt(position, PlacingTargets.None));
        }
    }

    [Fact]
    public void AServo_OnAOneLinkJoint_TurnsItsWheel_ButAWheelAddedLaterLeavesItsRoleToPick()
    {
        var build = TwoBeams();
        build.PlacePart(BuildPart.Wheel, Joint(2));
        build.PlacePart(BuildPart.Servo, Joint(2));
        Sides(build, build.Servos[0].Id).ShouldBe((4, 5));

        var servo = build.PlacePart(BuildPart.Servo, Joint(1))!.Value;
        var wheel = build.PlacePart(BuildPart.Wheel, Joint(1))!.Value;
        build.SelectOnly(CreatureElementKind.Servo, servo);
        build.Servos[1].TargetLinkId.ShouldBeNull();
        build.ServoNeedsTwoSides(1).ShouldBeFalse();
        var target = new BuildPresentationViewModel(build).SinglePart!.Pickers![1];
        target.Note.ShouldBeNull();
        target.Options.Select(option => option.LinkId).ShouldBe([4, wheel]);
        CreatureReadiness.Problems(build.Snapshot()).ShouldBe(
            [UiText.Format("{0} is missing a side. Pick its Fixed and Target parts or delete it.", PartNames.Display(build.Snapshot().Parts, servo))]);
        build.ShowTrainingBlockers();
        build.CanvasNotes().Single(note => note.Target.Id == servo).Text.ShouldBe(UiText.Plain("Pick a Target part"));

        // The Wheel is listed after the links, even with a lower id, and picking it for the other role swaps them.
        build.Load(new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(100, 0)), new NodeDef(3, new Vector2D(0, 100))],
            [new BeamDef(7, 1, 3), new BeamDef(6, 1, 2)], [], [new ServoDef(5, 1, 6, 4)], [], [], [new WheelDef(4, 1)], nextPartId: 8));
        build.SelectOnly(CreatureElementKind.Servo, 5);
        var pickers = new BuildPresentationViewModel(build).SinglePart!.Pickers!;
        pickers[0].Options.Select(option => (option.LinkId, option.Kind, option.Role, option.Note)).ShouldBe(
        [
            (6, CreatureElementKind.Beam, ServoLinkRole.Fixed, null),
            (7, CreatureElementKind.Beam, ServoLinkRole.None, null),
            (4, CreatureElementKind.Wheel, ServoLinkRole.Target, UiText.Plain("swaps")),
        ]);
        pickers[1].SelectedIndex.ShouldBe(2);

        build.ReplaceSelection(Set(Joint(2), Beam(6), new(CreatureElementKind.Servo, 5), new(CreatureElementKind.Wheel, 4)));
        build.CopySelectedParts();
        build.Servos[1].TargetLinkId.ShouldBe(build.Wheels[1].Id);
        build.Wheels[1].NodeId.ShouldBe(build.Servos[1].NodeId);

        build.Load(build.Snapshot(), locked: true);
        build.SelectOnly(CreatureElementKind.Wheel, 4);
        build.DeleteSelectedParts();
        build.Wheels.Count.ShouldBe(2);
        build.CanvasNotes().ShouldContain(note => note.Target == new CreatureElementSelection(CreatureElementKind.Servo, 5));
    }

    [Fact]
    public void ABrakesStrength_ReadsLockedAtTheTop_LeavesAnOffStepValue_AndTakesNoOtherInfinity()
    {
        var build = Loaded(TwoBeamsDef(new BrakeDef(6, 2, fixedLinkId: 4, targetLinkId: 5, strength: 1500000)));
        build.SelectOnly(CreatureElementKind.Brake, 6);
        new BuildPresentationViewModel(build).SinglePart!.BasicSettings.Single().Readout.ShouldBe(UiText.Format("{0} N·m", new FixedNumber(150, 0)));
        build.Brakes.Single().Strength.ShouldBe(1500000);
        build.CanUndo.ShouldBeFalse();

        build.SetParameter(PartParameterId.BrakeStrength, PartParameters.ValueAt(PartParameterId.BrakeStrength, 1));
        build.Brakes.Single().Locked.ShouldBeTrue();
        var slider = new BuildPresentationViewModel(build).SinglePart!.BasicSettings.Single();
        (slider.Readout, slider.High).ShouldBe((UiText.Plain("Locked"), 1));
        new CreatureBuilder(build.Snapshot()).IsWithinBuildLimits().ShouldBeTrue();
        build.Undo();
        build.Brakes.Single().Strength.ShouldBe(1500000);
        build.Redo();

        // Only a Brake's Strength may be infinite, and only upward: Locked.
        Should.Throw<ArgumentOutOfRangeException>(() => build.SetParameter(PartParameterId.BrakeStrength, double.NaN));
        Should.Throw<ArgumentOutOfRangeException>(() => build.SetParameter(PartParameterId.BrakeStrength, double.NegativeInfinity));
        Should.Throw<ArgumentOutOfRangeException>(() => build.SetParameter(PartParameterId.MotorStrength, double.PositiveInfinity));
        build.Brakes.Single().Locked.ShouldBeTrue();

        var second = build.PlacePart(BuildPart.Brake, Joint(1))!.Value;
        build.ReplaceSelection(Set(new(CreatureElementKind.Brake, 6), new(CreatureElementKind.Brake, second)));
        build.Selection.IdsOf(CreatureElementKind.Brake).Count.ShouldBe(2);
        new BuildPresentationViewModel(build).Selection!.Settings.Single().Readout.ShouldBe(UiText.Format("{0} N·m–Locked", new FixedNumber(200, 0)));

        build.Load(build.Snapshot(), locked: true);
        build.SelectOnly(CreatureElementKind.Brake, second);
        new BuildPresentationViewModel(build).SinglePart!.BasicSettings.Single().Locked.ShouldBeFalse();
        build.SetParameter(PartParameterId.BrakeStrength, BrakeDef.LockedStrength);
        build.Brakes[1].Locked.ShouldBeTrue();
    }

    [Fact]
    public void Motors_ShareTheSettingsTheyHaveInCommon_AndSetEachOfThem()
    {
        // Motors 8 and 9 on joints 2 and 3, Servo 10 on joint 4, Stepper 14 on joint 1 and Piston 11 from joint 1 to 5.
        var build = Loaded(new CreatureDef(
        [
            new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(100, 0)), new NodeDef(3, new Vector2D(200, 0)),
            new NodeDef(4, new Vector2D(300, 0)), new NodeDef(5, new Vector2D(0, 100)),
            new BeamDef(6, 1, 2), new BeamDef(7, 2, 3), new BeamDef(12, 3, 4), new BeamDef(13, 4, 1),
            new ServoDef(10, 4, fixedLinkId: 12, targetLinkId: 13), new PistonDef(11, 1, 5),
            new VelocityMotorDef(8, 2, fixedLinkId: 6, targetLinkId: 7), new VelocityMotorDef(9, 3, fixedLinkId: 7, targetLinkId: 12, maxSpeed: 3),
            new StepperDef(14, 1, fixedLinkId: 6, targetLinkId: 13),
        ]));
        var motors = Set(new(CreatureElementKind.VelocityMotor, 8), new(CreatureElementKind.VelocityMotor, 9));
        build.ReplaceSelection(motors);
        var selection = new BuildPresentationViewModel(build).Selection!;
        selection.BasicSettings.Select(slider => slider.Id).ShouldBe([PartParameterId.MotorStrength, PartParameterId.MotorMaxSpeed]);
        selection.AdvancedSettings.Select(slider => slider.Id).ShouldBe([PartParameterId.RiseTime]);
        build.SetParameter(PartParameterId.MotorMaxSpeed, 2.0);
        build.VelocityMotors.Select(motor => motor.MaxSpeed).ShouldBe([2.0, 2.0]);

        // A Servo's and a Piston's Max speed are other settings, so a Motor shares none with them.
        foreach (var other in new CreatureElementSelection[] { new(CreatureElementKind.Servo, 10), new(CreatureElementKind.Piston, 11) })
        {
            build.ReplaceSelection(Set(new(CreatureElementKind.VelocityMotor, 8), other));
            new BuildPresentationViewModel(build).Selection!.Settings.Select(slider => slider.Id).ShouldBe([PartParameterId.RiseTime]);
        }

        // A Stepper's Max strength is the Servo's setting.
        build.ReplaceSelection(Set(new(CreatureElementKind.Servo, 10), new(CreatureElementKind.Stepper, 14)));
        new BuildPresentationViewModel(build).Selection!.BasicSettings.Select(slider => slider.Id).ShouldContain(PartParameterId.ServoStrength);
        build.SetParameter(PartParameterId.ServoStrength, 400000);
        (build.Steppers.Single().Strength, build.Servos.Single().Strength).ShouldBe((400000, 400000));
        build.SelectOnly(CreatureElementKind.Stepper, 14);
        build.SetParameter(PartParameterId.StepsPerTurn, 12);
        build.Undo();
        build.Steppers.Single().StepsPerTurn.ShouldBe(StepperDef.DefaultStepsPerTurn);
        build.Redo();
        build.Steppers.Single().StepsPerTurn.ShouldBe(12);
    }

    [Fact]
    public void Copy_KeepsEachPartsSettings_AndKeepsABigWheelInsideTheBuildArea()
    {
        var build = Loaded(TwoBeamsDef(
            new StepperDef(6, 2, fixedLinkId: 4, targetLinkId: 5, stepsPerTurn: 12, maxFrequency: 1.5),
            new BrakeDef(7, 3, fixedLinkId: 5, targetLinkId: null, strength: BrakeDef.LockedStrength),
            new WheelDef(8, 1, radius: 70)));
        build.ReplaceSelection(Set(Joint(1), Joint(2), Joint(3), Beam(4), Beam(5),
            new(CreatureElementKind.Stepper, 6), new(CreatureElementKind.Brake, 7), new(CreatureElementKind.Wheel, 8)));

        build.CopySelectedParts();

        (build.Steppers[1].StepsPerTurn, build.Steppers[1].MaxFrequency).ShouldBe((12, 1.5));
        build.Brakes[1].Locked.ShouldBeTrue();
        build.Wheels[1].Radius.ShouldBe(70);

        // A plain joint would fit one step down and right; the big Wheel's would not, so it goes up and left.
        var corner = BuildViewModel.BuildArea.Max;
        var step = BuildViewModel.BuildAreaCell;
        build.Load(new CreatureDef(
        [
            new NodeDef(1, new Vector2D(corner.X - 120, corner.Y - 120)), new NodeDef(2, new Vector2D(corner.X - 220, corner.Y - 120)),
            new BeamDef(3, 1, 2), new WheelDef(4, 1, radius: WheelDef.MaxRadius),
        ]));
        build.ReplaceSelection(Set(Joint(1), Joint(2), Beam(3), new(CreatureElementKind.Wheel, 4)));
        build.CopySelectedParts();
        build.Nodes.Single(node => node.Id == build.Wheels[1].NodeId).Position.ShouldBe(new Vector2D(corner.X - 120 - step, corner.Y - 120 - step));
    }

    [Fact]
    public void AWheelsDisc_StandsForTheWheel_OnlyWhereNoPartIsHit_AndOnlyForATap()
    {
        // Wheel 6 on joint 2 (100, 0) and Wheel 7 on joint 1 (0, 0), both 1 m, overlap between them.
        var build = Loaded(TwoBeamsDef(new WheelDef(7, 1, radius: WheelDef.MaxRadius), new WheelDef(6, 2, radius: WheelDef.MaxRadius)));
        build.ActiveTool = BuildTool.Select;
        var gestures = new BuildGestures(build);

        gestures.Tap(new Vector2D(100, 10));
        build.SingleSelectedId(CreatureElementKind.Wheel).ShouldBe(6);
        build.DrawnPartAt(new Vector2D(100, 0), PlacingTargets.None).ShouldBe(Joint(2));
        gestures.Tap(new Vector2D(100, 10));
        build.SelectedPartCount.ShouldBe(0);
        gestures.Tap(new Vector2D(100, WheelDef.MaxRadius - 2));
        build.SingleSelectedId(CreatureElementKind.Wheel).ShouldBe(6);
        gestures.Tap(new Vector2D(100, WheelDef.MaxRadius + 2));
        build.SelectedPartCount.ShouldBe(0);
        gestures.Tap(new Vector2D(198, 2));
        build.SingleSelectedId(CreatureElementKind.Node).ShouldBe(3);
        build.ClearSelection();
        gestures.Tap(new Vector2D(40, 60));
        build.SingleSelectedId(CreatureElementKind.Wheel).ShouldBe(7);
        build.ClearSelection();
        gestures.Tap(new Vector2D(50, 60));
        build.SingleSelectedId(CreatureElementKind.Wheel).ShouldBe(6);

        // By design, the disc wins over adding a joint and over clearing the picked link.
        foreach (var tool in new[] { BuildTool.Joint, BuildTool.Beam })
        {
            build.ClearSelection();
            build.ActiveTool = tool;
            gestures.Tap(new Vector2D(100, 50));
            build.SingleSelectedId(CreatureElementKind.Wheel).ShouldBe(6);
            build.Nodes.Count.ShouldBe(3);
        }

        // A drag draws a box as before, and a sensor dropped on the disc finds no beam.
        build.ActiveTool = BuildTool.Select;
        build.ClearSelection();
        gestures.DragAcross(new Vector2D(150, 50), new Vector2D(150, 90));
        build.SelectedPartCount.ShouldBe(0);
        gestures.DropTargetAt(new Vector2D(150, 50), BuildPart.Camera).ShouldBeNull();

        // A beam crossing a Wheel draws over its face, so a touch hits the beam there.
        var crossing = build.ConnectLink(BuildLink.Beam, build.PlaceNode(new Vector2D(60, -50)), build.PlaceNode(new Vector2D(60, 50)))!.Value;
        build.ClearSelection();
        gestures.DropTargetAt(new Vector2D(60, 0), BuildPart.Camera).ShouldBe(Beam(crossing));
        gestures.Tap(new Vector2D(60, 0));
        build.Selection.IdsOf(CreatureElementKind.Beam).ShouldBe([crossing]);
    }

    [Fact]
    public void AWheel_GrowsItsJoint_WeighsByItsRadius_AndNeverKeepsACreatureFromTraining()
    {
        var build = TwoBeams();
        var first = build.PlacePart(BuildPart.Wheel, Joint(1))!.Value;
        build.NodeRadius(1).ShouldBe(WheelDef.DefaultRadius);
        var part = new BuildPresentationViewModel(build).SinglePart!;
        part.ConnectionsLabel.ShouldBeNull();
        part.Settings.Select(slider => (slider.Label, slider.Readout)).ShouldBe([(UiText.Plain("Radius"), UiText.Format("{0} m", new FixedNumber(0.4, 1)))]);
        part.Readouts.ShouldBe([new PartReadout(UiText.Plain("Weight"), UiText.Format("{0} kg", new FixedNumber(1.2, 1)))]);
        build.SetParameter(PartParameterId.WheelRadius, WheelDef.MaxRadius);
        new BuildPresentationViewModel(build).SinglePart!.Readouts!.Single().Value.ShouldBe(UiText.Format("{0} kg", new FixedNumber(3, 1)));

        var second = build.PlacePart(BuildPart.Wheel, Joint(2))!.Value;
        build.DefaultPartName(second).ShouldBe(UiText.Format("Wheel {0}", 2));
        CanvasNoteTargets.JointIds(new CreatureElementSelection(CreatureElementKind.Wheel, second), build).ShouldBe([2]);
        build.SetParameter(PartParameterId.WheelRadius, WheelDef.MaxRadius);

        // A Wheel lies under its links, so even the biggest Wheels on both ends leave it long enough.
        CreatureReadiness.IsTooShort(build.Snapshot(), CreatureElementKind.Beam, 1, 2).ShouldBeFalse();
        CreatureReadiness.Problems(build.Snapshot()).ShouldBeEmpty();
        build.SelectOnly(CreatureElementKind.Wheel, first);
        build.DeleteSelectedParts();
        build.NodeRadius(1).ShouldBe(NodeDef.PlainJointRadius);

        build.Load(build.Snapshot(), locked: true);
        build.ActiveTool = BuildTool.Parts;
        build.PickPart(BuildPart.Wheel);
        build.PickedPart.ShouldBe(BuildPart.Wheel);
        build.SelectOnly(CreatureElementKind.Wheel, second);
        build.CanEdit(PartParameterId.WheelRadius).ShouldBeTrue();
    }

    [Fact]
    public void AnAngleSensor_IsAnAvailableSensorRow_AndItsCalloutShowsItsThreeInputs()
    {
        PartTray.Create(BuildPart.AngleSensor).Groups[1].Rows.Single(row => row.Part == BuildPart.AngleSensor)
            .ShouldBe(new PartTrayRow(BuildPart.AngleSensor, UiText.Plain("Angle sensor"), PartTrayRowState.Selected, Version: null));
        PartTray.Create(picked: null, creationLocked: true).Groups[1].Rows.Single(row => row.Part == BuildPart.AngleSensor)
            .State.ShouldBe(PartTrayRowState.CreationLocked);
        PartTray.CanPick(BuildPart.AngleSensor, creationLocked: true).ShouldBeFalse();

        var creature = TwoBeamsDef(new AngleSensorDef(6, 2, fixedLinkId: 4, targetLinkId: 5));
        var layout = NetworkPorts.Of(creature);
        int[] layers = [layout.Inputs.Count, layout.Outputs.Count];
        var network = NeuralNetwork.FromGenome(layers, new double[NeuralNetwork.GenomeLength(layers)], Activation.Tanh);
        var values = NetworkPortValues.Sample(layout, network, [0.5, -0.5, 0.25]);

        var callout = PartCalloutPresentation.For(creature, new CreatureElementSelection(CreatureElementKind.AngleSensor, 6), values);

        callout.Kind.ShouldBe(PartKind.AngleSensor);
        callout.Inputs.Select(port => (port.Label, port.Range, port.Value)).ShouldBe([
            (UiText.Plain("angle sin"), PortRange.MinusOneToOne, 0.5),
            (UiText.Plain("angle cos"), PortRange.MinusOneToOne, -0.5),
            (UiText.Plain("speed"), PortRange.MinusOneToOne, 0.25),
        ]);
        callout.Outputs.ShouldBeEmpty();
    }

    private static IJointPartDef JointPart(BuildViewModel build, BuildPart part, int other) =>
        build.Snapshot().Parts.OfType<IJointPartDef>().Single(candidate => candidate.PartKind.ToString() == part.ToString() && candidate.Id != other);

    private static (int? Fixed, int? Target) Sides(BuildViewModel build, int id) =>
        build.HingeParts.Single(part => part.Id == id) is var hinge ? (hinge.FixedLinkId, hinge.TargetLinkId) : default;

    private static UiText NoteOf(BuildPart part) => part switch
    {
        BuildPart.Servo => PartInfo.Servo,
        BuildPart.Stepper => PartInfo.Stepper,
        BuildPart.VelocityMotor => PartInfo.VelocityMotor,
        BuildPart.Brake => PartInfo.Brake,
        BuildPart.AngleSensor => PartInfo.AngleSensor,
        BuildPart.Wheel => PartInfo.Wheel,
        BuildPart.Rubber => PartInfo.Rubber,
        _ => PartInfo.Touch,
    };
}
