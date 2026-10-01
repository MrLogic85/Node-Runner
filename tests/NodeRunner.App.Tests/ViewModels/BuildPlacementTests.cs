using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.ViewModels;

/// <summary>Dropping a part from the Parts tray onto the drawing (#376).</summary>
public sealed class BuildPlacementTests
{
    private static readonly CreatureElementSelection _firstBeam = new(CreatureElementKind.Beam, 4);
    private static readonly CreatureElementSelection _secondBeam = new(CreatureElementKind.Beam, 5);
    private static readonly CreatureElementSelection _firstJoint = new(CreatureElementKind.Node, 1);

    [Theory]
    [InlineData(BuildPart.Accelerometer, SensorKind.Accelerometer)]
    [InlineData(BuildPart.Camera, SensorKind.Camera)]
    public void PlacePart_OnAFreeBeam_AddsTheSensorThereWithAFreshId(BuildPart part, SensorKind kind)
    {
        var build = TwoBeams();
        var freshId = build.Snapshot().NextPartId;
        var changes = CountChanges(build);

        var id = build.PlacePart(part, _firstBeam);

        id.ShouldBe(freshId);
        build.Sensors.Select(sensor => (sensor.Id, sensor.BeamId, sensor.Kind)).ShouldBe([(freshId, 4, kind)]);
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

        build.PlacePart(BuildPart.Camera, _firstJoint).ShouldBeNull();

        build.Sensors.ShouldBeEmpty();
        changes().ShouldBe(0);
        build.PlacementNote.ShouldBe(new CanvasNote(CanvasNoteKind.Danger, _firstJoint, "Sensors go on a beam"));
    }

    [Fact]
    public void PlacePart_OnABeamWithASensor_ChangesNothing_AndNotesWhyAtTheBeam()
    {
        var build = TwoBeams();
        build.PlacePart(BuildPart.Accelerometer, _firstBeam);
        var changes = CountChanges(build);

        build.PlacePart(BuildPart.Camera, _firstBeam).ShouldBeNull();

        build.Sensors.Count.ShouldBe(1);
        changes().ShouldBe(0);
        build.PlacementNote.ShouldBe(new CanvasNote(CanvasNoteKind.Danger, _firstBeam, "One sensor per beam"));
    }

    [Fact]
    public void PlacePart_ComingLater_IsRefused()
    {
        var build = TwoBeams();

        build.CanPlacePart(BuildPart.Spring, _firstBeam, out var reason).ShouldBeFalse();
        reason.ShouldBe(PartTray.ComingLater);
        build.PlacePart(BuildPart.Spring, _firstBeam).ShouldBeNull();

        build.Sensors.ShouldBeEmpty();
    }

    [Fact]
    public void PlacePart_OnALockedCreation_ChangesNothing_WithoutANote()
    {
        var build = new BuildViewModel();
        build.Load(TwoBeams().Snapshot(), moveOnly: true);

        build.PlacePart(BuildPart.Accelerometer, _firstBeam).ShouldBeNull();

        build.Sensors.ShouldBeEmpty();
        build.PlacementNote.ShouldBeNull();
    }

    [Fact]
    public void CanPlacePart_SaysWhichBeamsTakeASensor()
    {
        var build = TwoBeams();
        build.PlacePart(BuildPart.Accelerometer, _firstBeam);

        build.CanPlacePart(BuildPart.Camera, _firstBeam, out var taken).ShouldBeFalse();
        taken.ShouldBe("One sensor per beam");
        build.CanPlacePart(BuildPart.Camera, _secondBeam, out var free).ShouldBeTrue();
        free.ShouldBeEmpty();
    }

    [Fact]
    public void PlacementNote_ComesFirst_AndGoesWhenDismissed_OrOnTheNextDrop()
    {
        var build = TwoBeams();
        build.PlacePart(BuildPart.Accelerometer, _firstJoint);

        build.CanvasNotes()[0].Text.ShouldBe("Sensors go on a beam");
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

        build.ToggleSelectedNode(1);
        build.DeleteSelectedParts();

        build.CanvasNotes().ShouldBeEmpty();
    }

    [Fact]
    public void DropTargetAt_PrefersJointDiscs_ThenSensors_ThenBeams_ThenJointReach()
    {
        var build = TwoBeams();
        build.PlacePart(BuildPart.Accelerometer, _secondBeam);
        var gestures = new BuildGestures(build);

        gestures.DropTargetAt(new Vector2D(5, 0)).ShouldBe(_firstJoint);
        gestures.DropTargetAt(new Vector2D(150, 9)).ShouldBe(_secondBeam);
        gestures.DropTargetAt(new Vector2D(30, 10)).ShouldBe(_firstBeam);
        gestures.DropTargetAt(new Vector2D(-30, 0)).ShouldBe(_firstJoint);
        gestures.DropTargetAt(new Vector2D(300, 300)).ShouldBeNull();
    }

    [Fact]
    public void DropPart_PlacesOnTheBeamUnderThePointer()
    {
        var build = TwoBeams();
        var gestures = new BuildGestures(build);

        gestures.DropPart(BuildPart.Camera, new Vector2D(40, 0)).ShouldNotBeNull();

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
        PartTray.IsAvailable(BuildPart.Spring).ShouldBeFalse();
    }

    /// <summary>Joints 1 (0,0), 2 (100,0) and 3 (200,0); beam 4 joins 1–2 and beam 5 joins 2–3.</summary>
    private static BuildViewModel TwoBeams()
    {
        var build = new BuildViewModel();
        build.PlaceNode(new Vector2D(0, 0), 18);
        build.PlaceNode(new Vector2D(100, 0), 18);
        build.PlaceNode(new Vector2D(200, 0), 18);
        build.ConnectBeam(1, 2);
        build.ConnectBeam(2, 3);
        return build;
    }

    private static Func<int> CountChanges(BuildViewModel build)
    {
        var changes = 0;
        build.AnatomyChanged += (_, _) => changes++;
        return () => changes;
    }
}
