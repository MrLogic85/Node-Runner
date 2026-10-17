using NodeRunner.App.ViewModels;
using NodeRunner.Domain;
using static NodeRunner.App.Tests.ViewModels.BuildFixtures;

namespace NodeRunner.App.Tests.ViewModels;

/// <summary>Tap to pick a tray part, then tap where it goes.</summary>
public sealed class BuildPartPickTests
{
    [Fact]
    public void APick_IsMadeInPartsOnly_OfAPartThatMayGo_AndClearedByARepickAToolOrALoad()
    {
        var build = PartsBuild();
        build.PickPart(BuildPart.Servo);
        build.PickedPart.ShouldBe(BuildPart.Servo);
        build.PickPart(BuildPart.Accelerometer);
        build.PickedPart.ShouldBe(BuildPart.Accelerometer);
        build.PickPart(BuildPart.Accelerometer);
        build.PickedPart.ShouldBeNull();
        build.PickPart(BuildPart.Pulse);
        build.PickedPart.ShouldBeNull();

        build.PickPart(BuildPart.Servo);
        build.ClearPickedPart().ShouldBeTrue();
        build.PickedPart.ShouldBeNull();
        build.ClearPickedPart().ShouldBeFalse();
        build.PickPart(BuildPart.Servo);
        build.ActiveTool = BuildTool.Select;
        build.PickedPart.ShouldBeNull();
        build.PickPart(BuildPart.Servo);
        build.PickedPart.ShouldBeNull();

        build.ActiveTool = BuildTool.Parts;
        build.PickPart(BuildPart.Servo);
        build.Load(new CreatureDef([], [], []), locked: true);
        build.PickedPart.ShouldBeNull();
        build.ActiveTool = BuildTool.Parts;
        build.PickPart(BuildPart.Servo);
        build.PickedPart.ShouldBeNull();
        build.PickPart(BuildPart.Wheel);
        build.PickedPart.ShouldBe(BuildPart.Wheel);
    }

    [Fact]
    public void ATap_PlacesThePickedPartWhereItGoes_AsAnUndoStep_AndKeepsThePickUntilATapOffThem()
    {
        var build = PartsBuild();
        var gestures = new BuildGestures(build);
        var changes = build.CountAnatomyChanges();
        build.PickPart(BuildPart.Servo);

        gestures.Tap(new Vector2D(0, 0));
        changes().ShouldBe(1);
        gestures.Tap(new Vector2D(100, 0));
        build.Servos.Select(servo => servo.NodeId).ShouldBe([1, 2]);
        (build.PickedPart, build.SelectedPartCount).ShouldBe((BuildPart.Servo, 0));
        build.Undo();
        build.Servos.Select(servo => servo.NodeId).ShouldBe([1]);
        build.Redo();
        build.Servos.Count.ShouldBe(2);

        build.PickPart(BuildPart.Accelerometer);
        gestures.Tap(new Vector2D(0, 0));
        build.Sensors.ShouldBeEmpty();
        build.PlacementNote.ShouldBe(new CanvasNote(CanvasNoteKind.Danger, Joint(1), BuildViewModel.GoesOnABeamReason(SensorKind.Accelerometer)));
        gestures.Tap(new Vector2D(50, 0));
        build.Sensors.Single().BeamId.ShouldBe(3);
        build.PickedPart.ShouldBe(BuildPart.Accelerometer);

        gestures.Tap(new Vector2D(50, 150));
        build.PickedPart.ShouldBeNull();
        (build.ActiveTool, build.Sensors.Count).ShouldBe((BuildTool.Parts, 1));
    }

    [Fact]
    public void ASelectionOrADrop_ClearsThePick_AndTheDroppedPartIsSelected()
    {
        var build = PartsBuild();
        var gestures = new BuildGestures(build);
        build.PickPart(BuildPart.Servo);

        build.ReplaceSelection([2]);
        build.PickedPart.ShouldBeNull();
        build.ClearPickedPart().ShouldBeFalse();
        gestures.Tap(new Vector2D(0, 0));
        build.Servos.ShouldBeEmpty();
        build.SelectedNodeIds.ShouldBe([1, 2], ignoreOrder: true);

        build.PickPart(BuildPart.Accelerometer);
        var servo = gestures.DropPart(BuildPart.Servo, new Vector2D(0, 0));
        build.SingleSelectedServoId.ShouldBe(servo);
        build.PickedPart.ShouldBeNull();
    }

    // Two joints 100 apart joined by beam 3, in the Parts tool.
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
}
