using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.ViewModels;

/// <summary>
/// The Rubber in Build (#1142): placed on a joint from the tray, between the Wheel and the Touch sensor
/// on the joint's stack, selected, copied, boxed and deleted with its joint, and, without brain ports,
/// added and deleted on a locked Creation like a Wheel.
/// </summary>
public sealed class BuildRubberTests
{
    private const int _servoId = 6;
    private const int _wheelId = 7;
    private const int _rubberId = 8;
    private const int _touchId = 9;
    private static readonly CreatureElementSelection _middleJoint = new(CreatureElementKind.Node, 2);
    private static readonly CreatureElementSelection _servo = new(CreatureElementKind.Servo, _servoId);
    private static readonly CreatureElementSelection _wheel = new(CreatureElementKind.Wheel, _wheelId);
    private static readonly CreatureElementSelection _rubber = new(CreatureElementKind.Rubber, _rubberId);
    private static readonly CreatureElementSelection _touch = new(CreatureElementKind.TouchSensor, _touchId);

    [Fact]
    public void PlacePart_OnAJoint_AddsItSelected_WithoutGrowingTheJoint()
    {
        var build = TwoBeams();

        var id = build.PlacePart(BuildPart.Rubber, _middleJoint);

        id.ShouldNotBeNull();
        build.Rubbers.ShouldBe([new RubberDef(id.Value, 2)]);
        build.Selection.Parts.ShouldBe([new CreatureElementSelection(CreatureElementKind.Rubber, id.Value)]);
        build.NodeRadius(2).ShouldBe(NodeDef.PlainJointRadius);
        build.PlacementNote.ShouldBeNull();
    }

    [Fact]
    public void PlacePart_OnABeam_OrOnAJointWithOne_IsRefused_WithANote()
    {
        var build = TwoBeams();
        var beam = new CreatureElementSelection(CreatureElementKind.Beam, 4);

        build.PlacePart(BuildPart.Rubber, beam).ShouldBeNull();
        build.PlacementNote!.Text.ShouldBe(UiText.Plain("Rubber goes on a joint"));

        build.PlacePart(BuildPart.Rubber, _middleJoint).ShouldNotBeNull();
        build.PlacePart(BuildPart.Rubber, _middleJoint).ShouldBeNull();
        build.PlacementNote!.Text.ShouldBe(UiText.Plain("One rubber per joint"));
        build.Rubbers.Count.ShouldBe(1);
    }

    // A Rubber has no brain ports, so it does not change the model a locked Creation's brain was bred for.
    [Fact]
    public void PlacePart_OnALockedCreation_AddsIt()
    {
        var build = new BuildViewModel();
        build.Load(TwoBeams().Snapshot(), locked: true);

        build.CanPlacePart(BuildPart.Rubber, _middleJoint, out _).ShouldBeTrue();
        build.PlacePart(BuildPart.Rubber, _middleJoint).ShouldNotBeNull();

        build.Rubbers.ShouldHaveSingleItem().NodeId.ShouldBe(2);
    }

    [Fact]
    public void PlacingTargets_RingEveryFreeJoint_RoundItsSkin_AndRefuseOneThatHasARubber()
    {
        var (build, _) = Stacked();

        var targets = build.PlacingTargetsOf(BuildPart.Rubber, movingSensorId: null);

        targets.Joints.ShouldBe([1, 3], ignoreOrder: true);
        targets.RefusedJoints.ShouldBe([2]);
        targets.JointRingOf(1).ShouldBe(NodeDef.PlainJointRadius + RubberDef.Skin);
        targets.JointRingOf(2).ShouldBe(WheelDef.DefaultRadius + RubberDef.Skin);
    }

    [Fact]
    public void PlacePart_OnAStackedJoint_TakesItsSlot_BetweenTheWheelAndTheTouchSensor()
    {
        var build = TwoBeams();

        build.PlacePart(BuildPart.TouchSensor, _middleJoint).ShouldNotBeNull();
        build.PlacePart(BuildPart.Rubber, _middleJoint).ShouldNotBeNull();
        build.PlacePart(BuildPart.Wheel, _middleJoint).ShouldNotBeNull();
        build.PlacePart(BuildPart.Servo, _middleJoint).ShouldNotBeNull();

        build.JointPartsAt(2).Select(part => part.Kind).ShouldBe([
            CreatureElementKind.Servo, CreatureElementKind.Wheel, CreatureElementKind.Rubber, CreatureElementKind.TouchSensor]);
        build.NodeRadius(2).ShouldBe(WheelDef.DefaultRadius);
    }

    [Fact]
    public void Tap_OnAStack_StepsDownThroughTheRubber_BeforeTheTouchSensor()
    {
        var (build, gestures) = Stacked();

        Tap(gestures, new Vector2D(100, 0));
        build.Selection.Parts.ShouldBe([_servo]);
        Tap(gestures, new Vector2D(100, 0));
        build.Selection.Parts.ShouldBe([_wheel]);
        Tap(gestures, new Vector2D(100, 0));
        build.Selection.Parts.ShouldBe([_rubber]);
        Tap(gestures, new Vector2D(100, 0));
        build.Selection.Parts.ShouldBe([_touch]);
        Tap(gestures, new Vector2D(100, 0));
        build.SelectedPartCount.ShouldBe(0);
    }

    // The skin adds no tap area: the joint is hit where it was before its Rubber.
    [Theory]
    [InlineData(BuildPart.Servo)]
    [InlineData(BuildPart.Wheel)]
    public void DrawnPartAt_IsTheSame_WithOrWithoutARubber(BuildPart part)
    {
        var bare = TwoBeams();
        bare.PlacePart(part, _middleJoint);
        var gripping = TwoBeams();
        gripping.PlacePart(part, _middleJoint);
        gripping.PlacePart(BuildPart.Rubber, _middleJoint);
        gripping.ClearSelection();
        bare.ClearSelection();

        for (var distance = 0; distance <= WheelDef.DefaultRadius + 20; distance++)
        {
            var position = new Vector2D(100, distance);
            gripping.DrawnPartAt(position, PlacingTargets.None).ShouldBe(bare.DrawnPartAt(position, PlacingTargets.None));
        }
    }

    [Fact]
    public void Box_OverAStack_SelectsTheRubberWithTheOtherParts_InPlaceOfTheJoint()
    {
        var (build, gestures) = Stacked();

        Drag(gestures, new Vector2D(70, -70), new Vector2D(130, 70));

        build.Selection.Nodes.ShouldBeEmpty();
        build.Selection.Servos.ShouldBe([_servoId]);
        build.Selection.Wheels.ShouldBe([_wheelId]);
        build.Selection.Rubbers.ShouldBe([_rubberId]);
        build.Selection.TouchSensors.ShouldBe([_touchId]);
    }

    [Fact]
    public void OnThisJoint_ListsFourTabs_WithTheRubberThird()
    {
        var (build, _) = Stacked();
        build.ReplaceSelection(PartSet.Of(_rubber));

        var part = new BuildPresentationViewModel(build).SinglePart!;

        part.OnThisJoint!.ShouldBe([
            new JointPartTab(PartSettingsKind.Servo, _servo),
            new JointPartTab(PartSettingsKind.Wheel, _wheel),
            new JointPartTab(PartSettingsKind.Rubber, _rubber),
            new JointPartTab(PartSettingsKind.Touch, _touch)]);
        part.OnThisJointIndex.ShouldBe(2);
    }

    [Fact]
    public void Settings_OfALoneRubber_AreItsNameAndWhatItDoes()
    {
        var build = TwoBeams();
        var id = build.PlacePart(BuildPart.Rubber, _middleJoint)!.Value;

        var part = new BuildPresentationViewModel(build).SinglePart!;

        part.Id.ShouldBe(id);
        part.Kind.ShouldBe(PartSettingsKind.Rubber);
        part.Name.ShouldBe(UiText.Format("Rubber {0}", 1));
        part.Note.ShouldBe(PartInfo.Rubber);
        part.Settings.ShouldBeEmpty();
        part.CanDelete.ShouldBeTrue();
        part.OnThisJoint.ShouldBeNull();
    }

    [Fact]
    public void UndoAndRedo_OfPlacement_TakeTheRubberAndBringItBackSelected()
    {
        var build = TwoBeams();
        var id = build.PlacePart(BuildPart.Rubber, _middleJoint)!.Value;

        build.Undo();
        build.Rubbers.ShouldBeEmpty();
        build.ReplaceSelection(PartSet.None with { Nodes = new HashSet<int> { 2 } });

        build.Redo();
        build.Rubbers.ShouldBe([new RubberDef(id, 2)]);
        build.Selection.Nodes.ShouldBeEmpty();
        build.SingleSelectedRubberId.ShouldBe(id);
    }

    [Fact]
    public void Rename_OfARubber_IsAnUndoStep()
    {
        var build = TwoBeams();
        var id = build.PlacePart(BuildPart.Rubber, _middleJoint)!.Value;
        build.RenamePart(id, "Sole", shownDefault: null);

        build.Undo();

        build.Rubbers.ShouldBe([new RubberDef(id, 2)]);
    }

    [Fact]
    public void Undo_OfAnotherEdit_KeepsTheRubber()
    {
        var build = TwoBeams();
        var id = build.PlacePart(BuildPart.Rubber, _middleJoint)!.Value;
        build.RenamePart(4, "Shin", shownDefault: null);

        build.Undo();

        build.Rubbers.ShouldBe([new RubberDef(id, 2)]);
    }

    [Fact]
    public void Rename_OfARubber_KeepsItsJoint()
    {
        var build = TwoBeams();
        var id = build.PlacePart(BuildPart.Rubber, _middleJoint)!.Value;

        build.RenamePart(id, "Sole", shownDefault: null);

        build.Rubbers.ShouldBe([new RubberDef(id, 2, "Sole")]);
        build.PartDisplayName(id).ShouldBe(UiText.AsWritten("Sole"));
    }

    [Fact]
    public void Copy_OfARubberAndItsLinks_BringsItsJointAlong_AndSelectsTheCopy()
    {
        var build = TwoBeams();
        var id = build.PlacePart(BuildPart.Rubber, _middleJoint)!.Value;
        build.ReplaceSelection(PartSet.None with { Nodes = new HashSet<int> { 1, 3 }, Beams = new HashSet<int> { 4, 5 }, Rubbers = new HashSet<int> { id } });

        build.CopySelectedParts();

        var copy = build.Rubbers.Single(rubber => rubber.Id != id);
        copy.NodeId.ShouldNotBe(2);
        build.Nodes.ShouldContain(node => node.Id == copy.NodeId);
        build.Selection.Rubbers.ShouldBe([copy.Id]);
    }

    [Fact]
    public void Copy_OfAWheelAndItsRubber_OnALockedCreation_IsAllowed()
    {
        var (build, _) = Stacked(locked: true);
        build.ReplaceSelection(PartSet.None with { Wheels = new HashSet<int> { _wheelId }, Rubbers = new HashSet<int> { _rubberId } });

        build.CanCopySelection.ShouldBeTrue();
        build.CopyBlockers().ShouldBeEmpty();
    }

    [Fact]
    public void Locked_TheRubberCanGo_WhileTheTouchSensorStays()
    {
        var (build, _) = Stacked(locked: true);

        build.ReplaceSelection(PartSet.Of(_rubber));
        build.DeleteLockedReason.ShouldBeNull();
        build.DeleteSelectedParts();

        build.Rubbers.ShouldBeEmpty();
        build.TouchSensors.ShouldHaveSingleItem();
    }

    [Fact]
    public void Delete_OfARubber_KeepsItsJoint()
    {
        var build = TwoBeams();
        var id = build.PlacePart(BuildPart.Rubber, _middleJoint)!.Value;
        build.ReplaceSelection(PartSet.None with { Rubbers = new HashSet<int> { id } });

        build.DeleteSelectedParts();

        build.Rubbers.ShouldBeEmpty();
        build.Nodes.ShouldContain(node => node.Id == 2);
    }

    // A joint keeps its Rubber after a delete clears its links, like any joint part (#973).
    [Fact]
    public void Delete_OfTheLinksOfAJointWithARubber_KeepsTheJointAndTheRubber()
    {
        var build = TwoBeams();
        build.PlacePart(BuildPart.Rubber, _middleJoint);
        build.PlacePart(BuildPart.Wheel, _middleJoint);
        build.ReplaceSelection(PartSet.None with { Beams = new HashSet<int> { 4, 5 }, Wheels = new HashSet<int>(build.Wheels.Select(wheel => wheel.Id)) });

        build.DeleteSelectedParts();

        build.Nodes.ShouldContain(node => node.Id == 2);
        build.Rubbers.ShouldHaveSingleItem();
    }

    [Fact]
    public void Delete_OfAJoint_TakesItsRubber()
    {
        var build = TwoBeams();
        build.PlacePart(BuildPart.Rubber, _middleJoint);
        build.SelectOnly(CreatureElementKind.Node, 2);

        build.DeleteSelectedParts();

        build.Rubbers.ShouldBeEmpty();
    }

    [Fact]
    public void ASelectedRubber_RaisesItsJointAndLinks_ToTheSelectedSurface()
    {
        var (build, _) = Stacked();

        var raised = build.RaisedParts(PartSet.Of(_rubber), PlacingTargets.None);

        raised.Joints.ShouldBe([1, 2, 3], ignoreOrder: true);
        raised.Links.ShouldBe([4, 5], ignoreOrder: true);
    }

    [Fact]
    public void CanvasNotes_OnARubber_PointAtItsJoint()
    {
        var (build, _) = Stacked();

        CanvasNoteTargets.JointIds(_rubber, build).ShouldBe([2]);
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

    /// <summary><see cref="TwoBeams"/> with a Servo, a Wheel, a Rubber and a Touch sensor on joint 2 and nothing selected; Select is active.</summary>
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
                [new RubberDef(_rubberId, 2)],
                nextPartId: 10),
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
