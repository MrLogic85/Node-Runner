using NodeRunner.App.Builders;
using NodeRunner.App.Lifecycle;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.ViewModels;

/// <summary>A Wheel (#129): placed on a joint, standing in for it, copied, deleted and tuned in Build.</summary>
public sealed class BuildWheelTests
{
    private static readonly CreatureElementSelection _firstJoint = new(CreatureElementKind.Node, 1);
    private static readonly CreatureElementSelection _middleJoint = new(CreatureElementKind.Node, 2);
    private static readonly CreatureElementSelection _firstBeam = new(CreatureElementKind.Beam, 4);

    [Fact]
    public void PlacePart_Wheel_OnAJointWithOneLink_AddsAndSelectsIt_AndGrowsTheJoint()
    {
        var build = TwoBeams();
        var freshId = build.Snapshot().NextPartId;

        var id = build.PlacePart(BuildPart.Wheel, _firstJoint);

        id.ShouldBe(freshId);
        build.Wheels.ShouldBe([new WheelDef(freshId, 1)]);
        build.SingleSelectedWheelId.ShouldBe(freshId);
        build.Selection.Nodes.ShouldBeEmpty();
        build.NodeRadius(1).ShouldBe(WheelDef.DefaultRadius);
        build.PlacementNote.ShouldBeNull();
    }

    [Fact]
    public void PlacePart_Wheel_OnALink_ChangesNothing_AndNotesWhyThere()
    {
        var build = TwoBeams();

        build.PlacePart(BuildPart.Wheel, _firstBeam).ShouldBeNull();

        build.Wheels.ShouldBeEmpty();
        build.PlacementNote.ShouldBe(new CanvasNote(CanvasNoteKind.Danger, _firstBeam, UiText.Plain("Wheels go on a joint")));
    }

    [Fact]
    public void PlacePart_Wheel_OnAJointWithAWheel_IsRefused_OneWheelPerJoint()
    {
        var build = TwoBeams();
        build.PlacePart(BuildPart.Wheel, _middleJoint);

        build.PlacePart(BuildPart.Wheel, _middleJoint).ShouldBeNull();

        build.Wheels.Count.ShouldBe(1);
        build.PlacementNote.ShouldBe(new CanvasNote(CanvasNoteKind.Danger, _middleJoint, UiText.Plain("One wheel per joint")));
    }

    // One part per joint until #1044.
    [Fact]
    public void AWheelAndAServo_NeverShareAJoint()
    {
        var build = TwoBeams();
        build.PlacePart(BuildPart.Wheel, _middleJoint);
        build.PlacePart(BuildPart.Servo, _firstJoint);

        build.CanPlacePart(BuildPart.Servo, _middleJoint, out var servoReason).ShouldBeFalse();
        build.CanPlacePart(BuildPart.Wheel, _firstJoint, out var wheelReason).ShouldBeFalse();

        servoReason.ShouldBe(CreatureBuilder.OnePartPerJointReason);
        wheelReason.ShouldBe(CreatureBuilder.OnePartPerJointReason);
    }

    [Fact]
    public void TapToPlace_PutsAWheelOnTheTappedJoint_AndKeepsTheTrayPick()
    {
        var build = TwoBeams();
        build.ActiveTool = BuildTool.Parts;
        build.PickPart(BuildPart.Wheel);
        var gestures = new BuildGestures(build);

        Tap(gestures, new Vector2D(200, 0));

        build.Wheels.Select(wheel => wheel.NodeId).ShouldBe([3]);
        build.PickedPart.ShouldBe(BuildPart.Wheel);
        build.SelectedPartCount.ShouldBe(0);
    }

    // A Wheel being placed rings every joint at a new Wheel's size, and the ring is where a finger aims (#1055).
    [Fact]
    public void DropTargetAt_ForAWheel_LandsOnAJointAnywhereInsideItsRing_BeforeABeam()
    {
        var gestures = new BuildGestures(TwoBeams());
        var ring = SelectionMarks.JointHalo(WheelDef.DefaultRadius);

        PartTray.PlacingRingRadius(BuildPart.Wheel).ShouldBe(WheelDef.DefaultRadius);
        gestures.DropTargetAt(new Vector2D(35, 0), BuildPart.Wheel).ShouldBe(_firstJoint);
        gestures.DropTargetAt(new Vector2D(35, 0), BuildPart.Accelerometer).ShouldBe(_firstBeam);
        gestures.DropTargetAt(new Vector2D(0, ring), BuildPart.Wheel).ShouldBe(_firstJoint);
        gestures.DropTargetAt(new Vector2D(0, ring + 1), BuildPart.Wheel).ShouldBeNull();
    }

    [Fact]
    public void DropPart_Wheel_PlacesItOnTheJointUnderThePointer()
    {
        var build = TwoBeams();
        var gestures = new BuildGestures(build);

        gestures.DropPart(BuildPart.Wheel, new Vector2D(100, 30)).ShouldNotBeNull();

        build.Wheels.Single().NodeId.ShouldBe(2);
        build.Wheels.Single().Id.ShouldBe(build.SingleSelectedWheelId!.Value);
    }

    // #1107: a beam lower on screen than a Wheel's joint draws over the Wheel, so a touch hits the
    // beam there, as drawn, and the Wheel only where it shows.
    [Fact]
    public void ABeamDrawnOverAWheel_IsHitOverIt()
    {
        var (build, gestures, wheel) = WheelOnTheMiddleJoint(radius: WheelDef.MaxRadius);
        var crossing = build.ConnectLink(BuildLink.Beam, build.PlaceNode(new Vector2D(60, -50)), build.PlaceNode(new Vector2D(60, 50)))!.Value;
        var onTheBeam = new Vector2D(60, 0);
        build.ClearSelection();

        gestures.DropTargetAt(onTheBeam, BuildPart.Camera).ShouldBe(new CreatureElementSelection(CreatureElementKind.Beam, crossing));
        Tap(gestures, onTheBeam);
        build.Selection.Beams.ShouldBe([crossing]);

        build.ClearSelection();
        Tap(gestures, new Vector2D(100, 60));
        build.SingleSelectedWheelId.ShouldBe(wheel);
    }

    [Fact]
    public void Tap_AnywhereOnAWheel_SelectsTheWheel_NotItsJoint()
    {
        var (build, gestures, wheel) = WheelOnTheMiddleJoint(radius: WheelDef.MaxRadius);

        Tap(gestures, new Vector2D(100, 90));

        build.SingleSelectedWheelId.ShouldBe(wheel);
        build.SingleSelectedNodeId.ShouldBeNull();
    }

    [Fact]
    public void Select_BoxOverAWheel_SelectsTheWheelInPlaceOfItsJoint()
    {
        var (build, gestures, wheel) = WheelOnTheMiddleJoint();

        gestures.Press(new Vector2D(70, -70));
        gestures.Drag(new Vector2D(130, 70));
        gestures.Release(new Vector2D(130, 70));

        build.Selection.Nodes.ShouldBeEmpty();
        build.Selection.Wheels.ShouldBe([wheel]);
        build.SelectedPartCount.ShouldBe(1);
    }

    [Fact]
    public void Select_DragAnUnselectedWheel_MovesItsJointAndSelectsTheWheel()
    {
        var (build, gestures, wheel) = WheelOnTheMiddleJoint();

        gestures.Press(new Vector2D(100, 0));
        gestures.Drag(new Vector2D(100, 40));
        gestures.Release(new Vector2D(100, 40));

        build.Nodes.Single(node => node.Id == 2).Position.ShouldBe(new Vector2D(100, 40));
        build.Selection.Nodes.ShouldBeEmpty();
        build.Selection.Wheels.ShouldBe([wheel]);
    }

    [Fact]
    public void Redo_OfWheelPlacement_UnderASelectedJoint_SelectsTheWheelInstead()
    {
        var build = TwoBeams();
        var wheel = build.PlacePart(BuildPart.Wheel, _middleJoint)!.Value;
        build.Undo();
        build.ReplaceSelection(PartSet.None with { Nodes = new HashSet<int> { 2 } });

        build.Redo();

        build.Selection.Nodes.ShouldBeEmpty();
        build.SingleSelectedWheelId.ShouldBe(wheel);
    }

    [Fact]
    public void DeleteWheel_Alone_LeavesItsJoint_AndUndoAndRedoBringItBackAndTakeItAgain()
    {
        var build = TwoBeams();
        var wheel = build.PlacePart(BuildPart.Wheel, _middleJoint)!.Value;
        build.SetParameter(PartParameterId.Grip, 0.3);

        build.DeleteSelectedParts();

        build.Wheels.ShouldBeEmpty();
        build.Nodes.Select(node => node.Id).ShouldBe([1, 2, 3]);
        build.NodeRadius(2).ShouldBe(NodeDef.PlainJointRadius);
        build.Undo();
        build.Wheels.ShouldBe([new WheelDef(wheel, 2, grip: 0.3)]);
        build.Selection.Wheels.ShouldBe([wheel]);
        build.Redo();
        build.Wheels.ShouldBeEmpty();
    }

    [Fact]
    public void DeleteWheel_WithEveryLinkOnItsJoint_TakesTheJointToo()
    {
        var build = TwoBeams();
        var wheel = build.PlacePart(BuildPart.Wheel, _middleJoint)!.Value;

        build.ReplaceSelection(PartSet.None with { Beams = new HashSet<int> { 4, 5 }, Wheels = new HashSet<int> { wheel } });
        build.DeleteSelectedParts();

        build.Nodes.Select(node => node.Id).ShouldBe([1, 3]);
        build.Wheels.ShouldBeEmpty();
    }

    [Fact]
    public void DeleteJoint_TakesItsWheel()
    {
        var build = new BuildViewModel();
        build.Load(new CreatureDef([new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(100, 0))], [new BeamDef(3, 1, 2)], [], [], [], [], [new WheelDef(4, 2)], nextPartId: 5));

        build.ReplaceSelection(PartSet.None with { Nodes = new HashSet<int> { 1, 2 }, Beams = new HashSet<int> { 3 } });
        build.DeleteSelectedParts();

        build.Wheels.ShouldBeEmpty();
        build.Nodes.ShouldBeEmpty();
    }

    [Fact]
    public void Copy_OfAWheel_BringsItsJoint_KeepsItsSettingsButNotItsName_AndSelectsTheCopy()
    {
        var build = TwoBeams();
        var wheel = build.PlacePart(BuildPart.Wheel, _middleJoint)!.Value;
        build.SetParameter(PartParameterId.WheelRadius, 70);
        build.SetParameter(PartParameterId.Grip, 0.3);
        build.RenamePart(wheel, "Front", shownDefault: null);
        build.ReplaceSelection(PartSet.None with { Nodes = new HashSet<int> { 1 }, Beams = new HashSet<int> { 4 }, Wheels = new HashSet<int> { wheel } });
        build.CanCopySelection.ShouldBeTrue();
        var nodesBefore = build.Nodes.Count;

        build.CopySelectedParts();

        build.Nodes.Count.ShouldBe(nodesBefore + 2);
        var copy = build.Wheels.Single(entry => entry.Id != wheel);
        copy.Radius.ShouldBe(70);
        copy.Grip.ShouldBe(0.3);
        copy.Name.ShouldBeNull();
        copy.NodeId.ShouldNotBe(2);
        build.Selection.Wheels.ShouldBe([copy.Id]);
        build.Selection.Nodes.Count.ShouldBe(1);
        build.Selection.Nodes.ShouldNotContain(copy.NodeId);
    }

    // A plain joint would fit one step down and right; the big Wheel's would not, so it goes up and left.
    [Fact]
    public void Copy_OfABigWheel_NearTheBuildAreasCorner_KeepsTheWheelInside_GoingTheOtherWay()
    {
        var corner = BuildViewModel.BuildArea.Max;
        var step = BuildViewModel.BuildGridStep;
        var build = new BuildViewModel();
        build.Load(new CreatureDef(
            [new NodeDef(1, new Vector2D(corner.X - 120, corner.Y - 120)), new NodeDef(2, new Vector2D(corner.X - 220, corner.Y - 120))],
            [new BeamDef(3, 1, 2)],
            [],
            [],
            [],
            [],
            [new WheelDef(4, 1, radius: WheelDef.MaxRadius)],
            nextPartId: 5));
        build.ReplaceSelection(PartSet.None with { Nodes = new HashSet<int> { 1, 2 }, Beams = new HashSet<int> { 3 }, Wheels = new HashSet<int> { 4 } });

        build.CopySelectedParts();

        var copy = build.Wheels.Single(wheel => wheel.Id != 4);
        var joint = build.Nodes.Single(node => node.Id == copy.NodeId).Position;
        joint.ShouldBe(new Vector2D(corner.X - 120 - step, corner.Y - 120 - step));
        (corner.X - joint.X).ShouldBeGreaterThanOrEqualTo(WheelDef.MaxRadius);
        (corner.Y - joint.Y).ShouldBeGreaterThanOrEqualTo(WheelDef.MaxRadius);
        build.Nodes.Skip(2).Select(node => node.Position).ShouldContain(new Vector2D(corner.X - 220 - step, corner.Y - 120 - step));
    }

    [Fact]
    public void Locked_PlacesEditsCopiesAndDeletesAWheel_WhosePortsAreNone()
    {
        var build = new BuildViewModel();
        build.Load(new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(100, 0)), new NodeDef(3, new Vector2D(100, 100))],
            [new BeamDef(4, 1, 2), new BeamDef(5, 2, 3)],
            [],
            [new ServoDef(6, 2, 4, 5)],
            [],
            [],
            nextPartId: 7),
            locked: true);

        build.ActiveTool = BuildTool.Parts;
        build.PickPart(BuildPart.Wheel);
        build.PickedPart.ShouldBe(BuildPart.Wheel);
        var wheel = build.PlacePart(BuildPart.Wheel, _firstJoint).ShouldNotBeNull();
        build.CanEdit(PartParameterId.WheelRadius).ShouldBeTrue();
        build.SetParameter(PartParameterId.WheelRadius, 100);
        build.SetParameter(PartParameterId.Grip, 0);
        build.Wheels.Single().ShouldBe(new WheelDef(wheel, 1, radius: 100, grip: 0));
        build.ReplaceSelection(PartSet.None with { Nodes = new HashSet<int> { 3 }, Beams = new HashSet<int> { 5 }, Wheels = new HashSet<int> { wheel } });
        build.CopyBlockers().ShouldContain(note => note.Target.Kind == CreatureElementKind.Beam);
        build.ReplaceSelection(PartSet.None with { Nodes = new HashSet<int> { 2 }, Wheels = new HashSet<int> { wheel } });
        build.CopyBlockers().ShouldBeEmpty();
        build.SelectOnly(CreatureElementKind.Wheel, wheel);
        build.DeleteLockedReason.ShouldBeNull();
        build.DeleteSelectedParts();

        build.Wheels.ShouldBeEmpty();
        build.IsLocked.ShouldBeTrue();
        build.CanPlacePart(BuildPart.Servo, _firstJoint, out var reason).ShouldBeFalse();
        reason.ShouldBe(BuildViewModel.LockedReason);
    }

    [Fact]
    public void SelectedWheel_ShowsItsRadiusGripAndWeight_AndItsNote()
    {
        var (build, _, wheel) = WheelOnTheMiddleJoint();
        build.SelectOnly(CreatureElementKind.Wheel, wheel);

        var part = new BuildPresentationViewModel(build).SinglePart!;

        part.Kind.ShouldBe(PartSettingsKind.Wheel);
        part.Title.ShouldBe(UiText.Plain("Wheel"));
        part.DefaultName.ShouldBe(UiText.Format("Wheel {0}", 1));
        part.Note.ShouldBe(UiText.Plain("Rolls freely on the ground."));
        part.CanDelete.ShouldBeTrue();
        part.ConnectionsLabel.ShouldBeNull();
        part.AdvancedSettings.ShouldBeEmpty();
        part.Settings.Select(slider => (slider.Id, slider.Label, slider.Readout)).ShouldBe(
        [
            (PartParameterId.WheelRadius, UiText.Plain("Radius"), UiText.Format("{0} m", new FixedNumber(0.4, 1))),
            (PartParameterId.Grip, UiText.Plain("Grip"), UiText.Format("{0}%", new FixedNumber(80, 0))),
        ]);
        part.Readouts.ShouldBe([new PartReadout(UiText.Plain("Weight"), UiText.Format("{0} kg", new FixedNumber(1.2, 1)))]);
    }

    [Fact]
    public void AWheelsWeight_FollowsItsRadius()
    {
        var (build, _, wheel) = WheelOnTheMiddleJoint();
        build.SelectOnly(CreatureElementKind.Wheel, wheel);

        build.SetParameter(PartParameterId.WheelRadius, PartParameters.ValueAt(PartParameterId.WheelRadius, 1));

        new BuildPresentationViewModel(build).SinglePart!.Readouts!.Single().Value.ShouldBe(UiText.Format("{0} kg", new FixedNumber(3, 1)));
    }

    [Fact]
    public void Wheels_AreNumberedAmongTheirOwnKind_AndANoteOnOneSitsOnItsJoint()
    {
        var build = TwoBeams();
        var first = build.PlacePart(BuildPart.Wheel, _firstJoint)!.Value;
        var second = build.PlacePart(BuildPart.Wheel, _middleJoint)!.Value;

        build.DefaultPartName(first).ShouldBe(UiText.Format("Wheel {0}", 1));
        build.DefaultPartName(second).ShouldBe(UiText.Format("Wheel {0}", 2));
        CanvasNoteTargets.JointIds(new CreatureElementSelection(CreatureElementKind.Wheel, second), build).ShouldBe([2]);
    }

    [Fact]
    public void AWheel_NeverKeepsACreatureFromTraining_ThoughItsSizeCountsInHowLongALinkMustBe()
    {
        var build = new BuildViewModel();
        build.Load(new CreatureDef([new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(150, 0))], [new BeamDef(3, 1, 2)], [], nextPartId: 4));
        build.PlacePart(BuildPart.Wheel, _firstJoint);
        build.PlacePart(BuildPart.Wheel, _middleJoint);

        CreatureReadiness.Problems(build.Snapshot()).ShouldBeEmpty();

        build.SetParameter(PartParameterId.WheelRadius, WheelDef.MaxRadius);
        CreatureReadiness.IsTooShort(build.Snapshot(), 1, 2).ShouldBeTrue();
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

    /// <summary><see cref="TwoBeams"/> with a Wheel on joint 2 and nothing selected; Select is active.</summary>
    private static (BuildViewModel Build, BuildGestures Gestures, int Wheel) WheelOnTheMiddleJoint(double radius = WheelDef.DefaultRadius)
    {
        var build = new BuildViewModel();
        build.Load(new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(100, 0)), new NodeDef(3, new Vector2D(200, 0))],
            [new BeamDef(4, 1, 2), new BeamDef(5, 2, 3)],
            [],
            [],
            [],
            [],
            [new WheelDef(6, 2, radius: radius)],
            nextPartId: 7));
        build.ActiveTool = BuildTool.Select;
        return (build, new BuildGestures(build), 6);
    }

    private static void Tap(BuildGestures gestures, Vector2D position)
    {
        gestures.Press(position);
        gestures.Release(position);
    }
}
