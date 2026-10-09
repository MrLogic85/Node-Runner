using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.ViewModels;

/// <summary>Tap to pick a tray part, then tap where it goes (#805).</summary>
public class BuildPartPickTests
{
    [Fact]
    public void PickPart_InParts_PicksIt_AndPickingItAgainClearsIt()
    {
        var build = PartsBuild();

        build.PickPart(BuildPart.Servo);
        build.PickedPart.ShouldBe(BuildPart.Servo);

        build.PickPart(BuildPart.Accelerometer);
        build.PickedPart.ShouldBe(BuildPart.Accelerometer);

        build.PickPart(BuildPart.Accelerometer);
        build.PickedPart.ShouldBeNull();
    }

    [Fact]
    public void PickPart_AComingLaterPart_OrOutsideParts_OrAPartWithPortsOnALockedCreation_PicksNothing()
    {
        var build = PartsBuild();
        build.PickPart(BuildPart.Stepper);
        build.PickedPart.ShouldBeNull();

        build.ActiveTool = BuildTool.Joint;
        build.PickPart(BuildPart.Servo);
        build.PickedPart.ShouldBeNull();

        build.Load(new CreatureDef([], [], []), locked: true);
        build.ActiveTool = BuildTool.Parts;
        build.PickPart(BuildPart.Servo);
        build.PickedPart.ShouldBeNull();
        build.PickPart(BuildPart.Wheel);
        build.PickedPart.ShouldBe(BuildPart.Wheel);
    }

    [Fact]
    public void ThePick_IsClearedByAnotherTool_ByClearPickedPart_AndByLoad()
    {
        var build = PartsBuild();
        build.PickPart(BuildPart.Servo);
        build.ActiveTool = BuildTool.Select;
        build.PickedPart.ShouldBeNull();

        build.ActiveTool = BuildTool.Parts;
        build.PickPart(BuildPart.Servo);
        build.ClearPickedPart().ShouldBeTrue();
        build.PickedPart.ShouldBeNull();
        build.ClearPickedPart().ShouldBeFalse();

        build.PickPart(BuildPart.Servo);
        build.Load(new CreatureDef([], [], []));
        build.PickedPart.ShouldBeNull();
    }

    [Fact]
    public void Tap_WithAPickedServo_PlacesOneOnEachJointTapped_KeepsThePick_AndSelectsNothing()
    {
        var build = PartsBuild();
        var gestures = new BuildGestures(build);
        build.PickPart(BuildPart.Servo);

        Tap(gestures, new Vector2D(0, 0));
        Tap(gestures, new Vector2D(100, 0));

        build.Servos.Select(servo => servo.NodeId).ShouldBe([1, 2]);
        build.PickedPart.ShouldBe(BuildPart.Servo);
        build.SelectedPartCount.ShouldBe(0);
    }

    [Fact]
    public void ATapPlacement_ReportsTheAnatomyChange_SoTheCanvasRedrawsAndAutosaveSeesIt()
    {
        var build = PartsBuild();
        var gestures = new BuildGestures(build);
        build.PickPart(BuildPart.Servo);
        var changes = 0;
        build.AnatomyChanged += (_, _) => changes++;

        Tap(gestures, new Vector2D(0, 0));

        changes.ShouldBe(1);
    }

    [Fact]
    public void Tap_WithAPickedSensor_PlacesItOnTheBeamTapped()
    {
        var build = PartsBuild();
        var gestures = new BuildGestures(build);
        build.PickPart(BuildPart.Accelerometer);

        Tap(gestures, new Vector2D(50, 0));

        build.Sensors.Single().BeamId.ShouldBe(build.Beams.Single().Id);
        build.PickedPart.ShouldBe(BuildPart.Accelerometer);
    }

    [Fact]
    public void Tap_OnATargetThePartCannotGoOn_ShowsTheDropNote_AndPlacesNothing()
    {
        var build = PartsBuild();
        var gestures = new BuildGestures(build);
        build.PickPart(BuildPart.Accelerometer);

        Tap(gestures, new Vector2D(0, 0));

        build.Sensors.ShouldBeEmpty();
        build.PlacementNote.ShouldBe(new CanvasNote(
            CanvasNoteKind.Danger,
            new CreatureElementSelection(CreatureElementKind.Node, 1),
            BuildViewModel.GoesOnABeamReason(SensorKind.Accelerometer)));
    }

    [Fact]
    public void Tap_OnEmptyCanvas_PlacesNothing_AndClearsThePick()
    {
        var build = PartsBuild();
        var gestures = new BuildGestures(build);
        build.PickPart(BuildPart.Servo);

        Tap(gestures, new Vector2D(50, 150));

        build.Servos.ShouldBeEmpty();
        build.PickedPart.ShouldBeNull();
        build.ActiveTool.ShouldBe(BuildTool.Parts);
    }

    [Fact]
    public void Tap_InsideAJointsServoRing_PlacesTheServo_AndKeepsThePick()
    {
        var build = PartsBuild();
        var gestures = new BuildGestures(build);
        build.PickPart(BuildPart.Servo);

        Tap(gestures, new Vector2D(0, SelectionMarks.JointHalo(ServoDef.JointRadius) - 1));

        build.Servos.Select(servo => servo.NodeId).ShouldBe([1]);
        build.PickedPart.ShouldBe(BuildPart.Servo);
    }

    [Fact]
    public void UndoAndRedo_CoverATapPlacement()
    {
        var build = PartsBuild();
        var gestures = new BuildGestures(build);
        build.PickPart(BuildPart.Servo);
        Tap(gestures, new Vector2D(0, 0));

        build.Undo();
        build.Servos.ShouldBeEmpty();

        build.Redo();
        build.Servos.Select(servo => servo.NodeId).ShouldBe([1]);
    }

    [Fact]
    public void ASelection_ClearsThePick_SoATapSelectsAsUsual()
    {
        var build = PartsBuild();
        var gestures = new BuildGestures(build);
        build.PickPart(BuildPart.Servo);

        build.ReplaceSelection([2]);
        build.PickedPart.ShouldBeNull();
        build.ClearPickedPart().ShouldBeFalse();

        Tap(gestures, new Vector2D(0, 0));

        build.Servos.ShouldBeEmpty();
        build.SelectedNodeIds.ShouldBe([1, 2], ignoreOrder: true);
    }

    [Fact]
    public void ADrop_StillSelectsThePlacedPart_AndClearsThePick()
    {
        var build = PartsBuild();
        var gestures = new BuildGestures(build);
        build.PickPart(BuildPart.Accelerometer);

        var servo = gestures.DropPart(BuildPart.Servo, new Vector2D(0, 0));

        build.SingleSelectedServoId.ShouldBe(servo);
        build.PickedPart.ShouldBeNull();
    }

    // Two joints 100 apart joined by a beam, in the Parts tool.
    private static BuildViewModel PartsBuild()
    {
        var build = new BuildViewModel();
        build.PlaceNode(new Vector2D(0, 0));
        build.PlaceNode(new Vector2D(100, 0));
        build.ConnectLink(BuildLink.Beam, 1, 2);
        build.ClearSelection();
        build.ActiveTool = BuildTool.Parts;
        return build;
    }

    private static void Tap(BuildGestures gestures, Vector2D at)
    {
        gestures.Press(at);
        gestures.Release(at);
    }
}
