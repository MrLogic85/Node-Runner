using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.ViewModels;

/// <summary>
/// The Touch sensor in Build (#1045): placed on a joint from the tray, last on the joint's stack,
/// selected, copied and boxed with its joint, and kept by a locked Creation as a part with a port.
/// </summary>
public sealed class BuildTouchSensorTests
{
    private const int _servoId = 6;
    private const int _wheelId = 7;
    private const int _touchId = 8;
    private static readonly CreatureElementSelection _middleJoint = new(CreatureElementKind.Node, 2);
    private static readonly CreatureElementSelection _servo = new(CreatureElementKind.Servo, _servoId);
    private static readonly CreatureElementSelection _wheel = new(CreatureElementKind.Wheel, _wheelId);
    private static readonly CreatureElementSelection _touch = new(CreatureElementKind.TouchSensor, _touchId);

    [Fact]
    public void PlacePart_OnAJoint_AddsItSelected_WithoutGrowingTheJoint()
    {
        var build = TwoBeams();

        var id = build.PlacePart(BuildPart.TouchSensor, _middleJoint);

        id.ShouldNotBeNull();
        build.TouchSensors.ShouldBe([new TouchSensorDef(id.Value, 2)]);
        build.Selection.Parts.ShouldBe([new CreatureElementSelection(CreatureElementKind.TouchSensor, id.Value)]);
        build.NodeRadius(2).ShouldBe(NodeDef.PlainJointRadius);
        build.PlacementNote.ShouldBeNull();
    }

    [Fact]
    public void PlacePart_OnABeam_OrOnAJointWithOne_IsRefused_WithANote()
    {
        var build = TwoBeams();
        var beam = new CreatureElementSelection(CreatureElementKind.Beam, 4);

        build.PlacePart(BuildPart.TouchSensor, beam).ShouldBeNull();
        build.PlacementNote!.Text.ShouldBe(UiText.Plain("Touch sensors go on a joint"));

        build.PlacePart(BuildPart.TouchSensor, _middleJoint).ShouldNotBeNull();
        build.PlacePart(BuildPart.TouchSensor, _middleJoint).ShouldBeNull();
        build.PlacementNote!.Text.ShouldBe(UiText.Plain("One Touch sensor per joint"));
        build.TouchSensors.Count.ShouldBe(1);
    }

    [Fact]
    public void PlacePart_OnALockedCreation_ChangesNothing_WithoutANote()
    {
        var build = new BuildViewModel();
        build.Load(TwoBeams().Snapshot(), locked: true);

        build.CanPlacePart(BuildPart.TouchSensor, _middleJoint, out _).ShouldBeFalse();
        build.PlacePart(BuildPart.TouchSensor, _middleJoint).ShouldBeNull();

        build.TouchSensors.ShouldBeEmpty();
        build.PlacementNote.ShouldBeNull();
    }

    [Fact]
    public void PlacingTargets_RingEveryFreeJoint_RoundItsCushion_AndRefuseOneThatHasATouchSensor()
    {
        var (build, _) = Stacked();

        var targets = build.PlacingTargetsOf(BuildPart.TouchSensor, movingSensorId: null);

        targets.Joints.ShouldBe([1, 3], ignoreOrder: true);
        targets.RefusedJoints.ShouldBe([2]);
        targets.JointRingOf(1).ShouldBe(NodeDef.PlainJointRadius + TouchSensorDef.Cushion);
        targets.JointRingOf(3).ShouldBe(NodeDef.PlainJointRadius + TouchSensorDef.Cushion);
    }

    // The cushion goes round the joint's largest part, so the placing ring does too, and a drop lands inside it (#1055).
    [Fact]
    public void PlacingTargets_RingAServosOrWheelsJoint_OutsideItsPart_WhereADropLands()
    {
        var build = TwoBeams();
        build.PlacePart(BuildPart.Servo, new CreatureElementSelection(CreatureElementKind.Node, 1));
        build.PlacePart(BuildPart.Wheel, new CreatureElementSelection(CreatureElementKind.Node, 3));
        var gestures = new BuildGestures(build);

        var targets = build.PlacingTargetsOf(BuildPart.TouchSensor, movingSensorId: null);

        targets.JointRingOf(1).ShouldBe(JointMotor.Radius + TouchSensorDef.Cushion);
        targets.JointRingOf(2).ShouldBe(NodeDef.PlainJointRadius + TouchSensorDef.Cushion);
        targets.JointRingOf(3).ShouldBe(WheelDef.DefaultRadius + TouchSensorDef.Cushion);
        build.PlacingTargetsOf(BuildPart.Servo, movingSensorId: null).JointRingOf(3).ShouldBe(JointMotor.Radius);
        var edge = SelectionMarks.JointHalo(targets.JointRingOf(3));
        gestures.DropTargetAt(new Vector2D(200, edge), BuildPart.TouchSensor).ShouldBe(new CreatureElementSelection(CreatureElementKind.Node, 3));
        gestures.DropTargetAt(new Vector2D(200, edge + 1), BuildPart.TouchSensor).ShouldBeNull();
    }

    [Fact]
    public void Redo_OfPlacement_UnderASelectedJoint_SelectsTheTouchSensorInstead()
    {
        var build = TwoBeams();
        var touch = build.PlacePart(BuildPart.TouchSensor, _middleJoint)!.Value;
        build.Undo();
        build.ReplaceSelection(PartSet.None with { Nodes = new HashSet<int> { 2 } });

        build.Redo();

        build.Selection.Nodes.ShouldBeEmpty();
        build.SingleSelectedTouchSensorId.ShouldBe(touch);
    }

    [Fact]
    public void PlacePart_OnAStackedJoint_TakesItsOwnSlot()
    {
        var build = TwoBeams();

        build.PlacePart(BuildPart.Servo, _middleJoint).ShouldNotBeNull();
        build.PlacePart(BuildPart.Wheel, _middleJoint).ShouldNotBeNull();
        build.PlacePart(BuildPart.TouchSensor, _middleJoint).ShouldNotBeNull();

        build.JointPartsAt(2).Select(part => part.Kind).ShouldBe([CreatureElementKind.Servo, CreatureElementKind.Wheel, CreatureElementKind.TouchSensor]);
        build.NodeRadius(2).ShouldBe(WheelDef.DefaultRadius);
    }

    [Fact]
    public void Tap_OnAStack_StepsDownToTheTouchSensorLast()
    {
        var (build, gestures) = Stacked();

        Tap(gestures, new Vector2D(100, 0));
        build.Selection.Parts.ShouldBe([_servo]);
        Tap(gestures, new Vector2D(100, 0));
        build.Selection.Parts.ShouldBe([_wheel]);
        Tap(gestures, new Vector2D(100, 0));
        build.Selection.Parts.ShouldBe([_touch]);
        Tap(gestures, new Vector2D(100, 0));
        build.SelectedPartCount.ShouldBe(0);
    }

    [Fact]
    public void Box_OverAStack_SelectsTheTouchSensorWithTheOtherParts_InPlaceOfTheJoint()
    {
        var (build, gestures) = Stacked();

        Drag(gestures, new Vector2D(70, -70), new Vector2D(130, 70));

        build.Selection.Nodes.ShouldBeEmpty();
        build.Selection.Servos.ShouldBe([_servoId]);
        build.Selection.Wheels.ShouldBe([_wheelId]);
        build.Selection.TouchSensors.ShouldBe([_touchId]);
    }

    [Fact]
    public void OnThisJoint_ListsThreeTabs_WithTheTouchSensorLast()
    {
        var (build, _) = Stacked();
        build.ReplaceSelection(PartSet.Of(_touch));

        var part = new BuildPresentationViewModel(build).SinglePart!;

        part.OnThisJoint!.ShouldBe([
            new JointPartTab(PartKind.Servo, _servo),
            new JointPartTab(PartKind.Wheel, _wheel),
            new JointPartTab(PartKind.TouchSensor, _touch)]);
        part.OnThisJointIndex.ShouldBe(2);
    }

    [Fact]
    public void Settings_OfALoneTouchSensor_AreItsNameAndWhatItDoes()
    {
        var build = TwoBeams();
        var id = build.PlacePart(BuildPart.TouchSensor, _middleJoint)!.Value;

        var part = new BuildPresentationViewModel(build).SinglePart!;

        part.Id.ShouldBe(id);
        part.Kind.ShouldBe(PartKind.TouchSensor);
        part.Name.ShouldBe(UiText.Format("Touch {0}", 1));
        part.Note.ShouldBe(PartInfo.Touch);
        part.Settings.ShouldBeEmpty();
        part.CanDelete.ShouldBeTrue();
        part.OnThisJoint.ShouldBeNull();
    }

    [Fact]
    public void Copy_OfATouchSensorAndItsLinks_BringsItsJointAlong_AndSelectsTheCopy()
    {
        var build = TwoBeams();
        var id = build.PlacePart(BuildPart.TouchSensor, _middleJoint)!.Value;
        build.ReplaceSelection(PartSet.None with { Nodes = new HashSet<int> { 1, 3 }, Beams = new HashSet<int> { 4, 5 }, TouchSensors = new HashSet<int> { id } });

        build.CopySelectedParts();

        var copy = build.TouchSensors.Single(touch => touch.Id != id);
        copy.NodeId.ShouldNotBe(2);
        build.Nodes.ShouldContain(node => node.Id == copy.NodeId);
        build.Selection.TouchSensors.ShouldBe([copy.Id]);
    }

    [Fact]
    public void Copy_OnALockedCreation_IsBlocked_WithANoteOnTheTouchSensor()
    {
        var (build, _) = Stacked(locked: true);
        build.ReplaceSelection(PartSet.None with { Wheels = new HashSet<int> { _wheelId }, TouchSensors = new HashSet<int> { _touchId } });

        build.CanCopySelection.ShouldBeFalse();
        build.CopyBlockers().ShouldBe([new CanvasNote(CanvasNoteKind.Danger, _touch, UiText.Plain("Locked: would change the network"))]);
    }

    [Fact]
    public void Locked_TheTouchSensorStays_WhileTheWheelCanGo()
    {
        var (build, _) = Stacked(locked: true);

        build.ReplaceSelection(PartSet.Of(_touch));
        build.DeleteLockedReason.ShouldNotBeNull();
        build.ReplaceSelection(PartSet.Of(_wheel));
        build.DeleteLockedReason.ShouldBeNull();
    }

    [Fact]
    public void ASelectedTouchSensor_RaisesItsJointAndLinks_ToTheSelectedSurface()
    {
        var (build, _) = Stacked();

        var raised = build.RaisedParts(PartSet.Of(_touch), PlacingTargets.None);

        raised.Joints.ShouldBe([1, 2, 3], ignoreOrder: true);
        raised.Links.ShouldBe([4, 5], ignoreOrder: true);
    }

    [Fact]
    public void CanvasNotes_OnATouchSensor_PointAtItsJoint()
    {
        var (build, _) = Stacked();

        CanvasNoteTargets.JointIds(_touch, build).ShouldBe([2]);
    }

    /// <summary>Joints 1 (0,0), 2 (100,0) and 3 (200,0); beam 4 joins 1–2 and beam 5 joins 2–3.</summary>
    private static BuildViewModel TwoBeams()
    {
        var build = new BuildViewModel();
        build.Load(new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(100, 0)), new NodeDef(3, new Vector2D(200, 0))],
            [new BeamDef(4, 1, 2), new BeamDef(5, 2, 3)],
            [],
            nextPartId: 6));
        return build;
    }

    /// <summary><see cref="TwoBeams"/> with a Servo, a Wheel and a Touch sensor on joint 2 and nothing selected; Select is active.</summary>
    private static (BuildViewModel Build, BuildGestures Gestures) Stacked(bool locked = false)
    {
        var build = new BuildViewModel();
        build.Load(
            new CreatureDef(
                [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(100, 0)), new NodeDef(3, new Vector2D(200, 0))],
                [new BeamDef(4, 1, 2), new BeamDef(5, 2, 3)],
                [],
                [new ServoDef(_servoId, 2, 4, 5)],
                [],
                [],
                [new WheelDef(_wheelId, 2)],
                [new TouchSensorDef(_touchId, 2)],
                nextPartId: 9),
            locked: locked);
        build.ActiveTool = BuildTool.Select;
        return (build, new BuildGestures(build));
    }

    private static void Tap(BuildGestures gestures, Vector2D position)
    {
        gestures.Press(position);
        gestures.Release(position);
    }

    private static void Drag(BuildGestures gestures, Vector2D from, Vector2D to)
    {
        gestures.Press(from);
        gestures.Drag(to);
        gestures.Release(to);
    }
}
