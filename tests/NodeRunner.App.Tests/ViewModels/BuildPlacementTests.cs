using NodeRunner.App.Builders;
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
        build.PlacementNote.ShouldBe(new CanvasNote(CanvasNoteKind.Danger, _firstJoint, UiText.Plain("Sensors go on a beam")));
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

    [Theory]
    [InlineData(BuildPart.Battery)]
    [InlineData(BuildPart.Camera)]
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

        build.CanPlacePart(BuildPart.Accelerometer, _firstBeam, out var taken).ShouldBeFalse();
        taken.ShouldBe(UiText.Plain("One sensor per beam"));
        build.CanPlacePart(BuildPart.Accelerometer, _secondBeam, out var free).ShouldBeTrue();
        free.ShouldBeNull();
    }

    [Fact]
    public void PlacementNote_ComesFirst_AndGoesWhenDismissed_OrOnTheNextDrop()
    {
        var build = TwoBeams();
        build.PlacePart(BuildPart.Accelerometer, _firstJoint);

        build.CanvasNotes()[0].Text.ShouldBe(UiText.Plain("Sensors go on a beam"));
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

    [Fact]
    public void DropTargetAt_PrefersJointDiscs_ThenSensors_ThenBeams_ThenJointReach()
    {
        var build = TwoBeams();
        build.PlacePart(BuildPart.Accelerometer, _secondBeam);
        var gestures = new BuildGestures(build);

        gestures.DropTargetAt(new Vector2D(5, 0)).ShouldBe(_firstJoint);
        gestures.DropTargetAt(new Vector2D(150, 9)).ShouldBe(_secondBeam);
        gestures.DropTargetAt(new Vector2D(30, 10)).ShouldBe(_firstBeam);
        gestures.DropTargetAt(new Vector2D(300, 300)).ShouldBeNull();

        // Zoomed in, the beam's finger-sized reach is shorter than the joint's gap.
        gestures.View.ZoomAbout(new Vector2D(0, 0), 2);
        gestures.DropTargetAt(gestures.View.ToView(new Vector2D(-17, 0))).ShouldBe(_firstJoint);
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
    public void PlacePart_Servo_OnAJointWithAnyTwoLinks_AddsDefaultLowestIds(string setup, int fixedLink, int targetLink)
    {
        var build = setup switch
        {
            "beam-piston" => BeamAndPiston(),
            "beam-spring" => BeamAndSpring(),
            "piston-spring" => PistonAndSpring(),
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
        build.CanvasNotes().Single(note => note.Target.Id == servoId).Text.ShouldBe(UiText.Plain("Pick two links"));

        build.ReplaceSelection(PartSet.None with { Springs = new HashSet<int> { 5 } });
        build.DeleteSelectedParts();

        build.Servos.Single().TargetLinkId.ShouldBeNull();
    }

    [Fact]
    public void PlacePart_Servo_RefusesNonJointAndOccupiedJoint()
    {
        var build = TwoBeams();

        build.PlacePart(BuildPart.Servo, _firstBeam).ShouldBeNull();
        build.PlacementNote.ShouldBe(new CanvasNote(CanvasNoteKind.Danger, _firstBeam, UiText.Plain("Joint parts go on a joint")));

        build.PlacePart(BuildPart.Servo, _middleJoint).ShouldNotBeNull();
        build.PlacePart(BuildPart.Servo, _middleJoint).ShouldBeNull();
        build.PlacementNote.ShouldBe(new CanvasNote(CanvasNoteKind.Danger, _middleJoint, UiText.Plain("One part per joint")));
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
        build.ConnectBeam(1, 2);
        build.ConnectBeam(2, 3);
        return build;
    }

    private static BuildViewModel BeamAndPiston()
    {
        var build = ThreeNodes();
        build.ConnectBeam(1, 2);
        build.ConnectLink(BuildLink.Piston, 2, 3).ShouldBe(5);
        return build;
    }

    private static BuildViewModel BeamAndSpring()
    {
        var build = ThreeNodes();
        build.ConnectBeam(1, 2);
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
