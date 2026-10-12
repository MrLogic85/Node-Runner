using NodeRunner.App.Lifecycle;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.ViewModels;

/// <summary>A Servo driving the Wheel on its joint, a one-sided link, as its Fixed or Target (#1118).</summary>
public sealed class BuildServoWheelTests
{
    private static readonly CreatureElementSelection _endJoint = new(CreatureElementKind.Node, 1);
    private static readonly CreatureElementSelection _middleJoint = new(CreatureElementKind.Node, 2);

    [Fact]
    public void PlacePart_Servo_OnAOneLinkJointWithAWheel_HoldsTheLink_AndTurnsTheWheel()
    {
        var (build, wheel) = WheelOnTheEndJoint();

        build.PlacePart(BuildPart.Servo, _endJoint).ShouldNotBeNull();

        build.Servos.Single().FixedLinkId.ShouldBe(4);
        build.Servos.Single().TargetLinkId.ShouldBe(wheel);
        build.ServoNeedsTwoSides(1).ShouldBeFalse();
        CreatureReadiness.CanTrain(build.Snapshot()).ShouldBeTrue();
    }

    [Fact]
    public void PlacePart_Servo_OnATwoLinkJointWithAWheel_TakesTheLinks_AsBefore()
    {
        var build = TwoBeams();
        build.PlacePart(BuildPart.Wheel, _middleJoint);

        build.PlacePart(BuildPart.Servo, _middleJoint);

        build.Servos.Single().FixedLinkId.ShouldBe(4);
        build.Servos.Single().TargetLinkId.ShouldBe(5);
    }

    [Fact]
    public void ServoPickers_ListTheWheelAfterTheLinks_EvenWithALowerId_WithItsRole()
    {
        var build = new BuildViewModel();
        build.Load(new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(100, 0)), new NodeDef(3, new Vector2D(0, 100))],
            [new BeamDef(7, 1, 3), new BeamDef(6, 1, 2)],
            [],
            [new ServoDef(5, 1, 6, 4)],
            [],
            [],
            [new WheelDef(4, 1)],
            nextPartId: 8));
        build.SelectOnly(CreatureElementKind.Servo, 5);

        var pickers = new BuildPresentationViewModel(build).SinglePart!.Pickers!;

        var swaps = UiText.Plain("swaps");
        pickers[0].Options.Select(option => (option.LinkId, option.Kind, option.Role, option.Note)).ShouldBe(
        [
            (6, CreatureElementKind.Beam, ServoLinkRole.Fixed, null),
            (7, CreatureElementKind.Beam, ServoLinkRole.None, null),
            (4, CreatureElementKind.Wheel, ServoLinkRole.Target, swaps),
        ]);
        pickers[1].SelectedIndex.ShouldBe(2);
    }

    // Adding a Wheel does not fill a Servo's empty role: the player picks it, so the notes ask for a pick.
    [Fact]
    public void AWheelAddedUnderAOneLinkServo_LetsItsEmptyRoleBePicked()
    {
        var build = TwoBeams();
        var servoId = build.PlacePart(BuildPart.Servo, _endJoint)!.Value;
        var wheel = build.PlacePart(BuildPart.Wheel, _endJoint)!.Value;
        build.SelectOnly(CreatureElementKind.Servo, servoId);

        build.Servos.Single().TargetLinkId.ShouldBeNull();
        build.ServoNeedsTwoSides(1).ShouldBeFalse();
        var target = new BuildPresentationViewModel(build).SinglePart!.Pickers![1];
        target.Note.ShouldBeNull();
        target.Options.Select(option => option.LinkId).ShouldBe([4, wheel]);
        CreatureReadiness.Problems(build.Snapshot()).ShouldBe(
            [UiText.Format("{0} is missing a link. Pick its Fixed and Target links or delete it.",
                PartNames.Display(build.Nodes, build.Beams, build.Sensors, build.Servos, build.Pistons, build.Springs, build.Wheels, build.TouchSensors, build.Rubbers, build.VelocityMotors, servoId))]);
        build.ShowTrainingBlockers();
        build.CanvasNotes().Single(note => note.Target.Id == servoId).Text.ShouldBe(UiText.Plain("Pick a Target link"));
    }

    [Fact]
    public void SetServoLink_ToTheWheel_SwapsWithTheOtherRole_GivesAFreshId_AndUndoes()
    {
        var (build, wheel) = WheelOnTheEndJoint();
        var servoId = build.PlacePart(BuildPart.Servo, _endJoint)!.Value;

        var swapped = build.SetServoLink(servoId, fixedRole: true, linkId: wheel).ShouldNotBeNull();

        swapped.ShouldNotBe(servoId);
        build.Servos.Single().ShouldBe(new ServoDef(swapped, 1, wheel, 4));
        build.Undo();
        build.Servos.Single().ShouldBe(new ServoDef(servoId, 1, 4, wheel));
    }

    [Fact]
    public void SetServoLink_ToAnotherJointsWheel_Throws()
    {
        var (build, _) = WheelOnTheEndJoint();
        var other = build.PlacePart(BuildPart.Wheel, _middleJoint)!.Value;
        var servoId = build.PlacePart(BuildPart.Servo, _endJoint)!.Value;

        Should.Throw<ArgumentException>(() => build.SetServoLink(servoId, fixedRole: false, linkId: other));
    }

    [Fact]
    public void DeleteTheServosWheel_LeavesItMissingASide_AndUndoRestoresIt()
    {
        var (build, wheel) = WheelOnTheEndJoint();
        var servoId = build.PlacePart(BuildPart.Servo, _endJoint)!.Value;
        build.SelectOnly(CreatureElementKind.Wheel, wheel);

        build.DeleteSelectedParts();

        build.Servos.Single().ShouldBe(new ServoDef(servoId, 1, 4, null));
        CreatureReadiness.CanTrain(build.Snapshot()).ShouldBeFalse();
        build.ServoNeedsTwoSides(1).ShouldBeTrue();
        build.Undo();
        build.Servos.Single().ShouldBe(new ServoDef(servoId, 1, 4, wheel));
        build.Wheels.Single().Id.ShouldBe(wheel);
    }

    [Fact]
    public void Locked_DeletingTheServosWheel_IsRefused_ForItChangesTheModel()
    {
        var creature = new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(100, 0))],
            [new BeamDef(3, 1, 2)],
            [],
            [new ServoDef(5, 1, 3, 4)],
            [],
            [],
            [new WheelDef(4, 1)],
            nextPartId: 6);
        var build = new BuildViewModel();
        build.Load(creature, locked: true);
        build.SelectOnly(CreatureElementKind.Wheel, 4);

        build.DeleteSelectedParts();

        build.Wheels.ShouldHaveSingleItem();
        build.CanvasNotes().ShouldContain(note => note.Target == new CreatureElementSelection(CreatureElementKind.Servo, 5));
    }

    [Fact]
    public void Copy_OfAServoWithItsWheel_TurnsTheCopiedWheel()
    {
        var (build, wheel) = WheelOnTheEndJoint();
        var servoId = build.PlacePart(BuildPart.Servo, _endJoint)!.Value;
        build.ReplaceSelection(PartSet.None with
        {
            Nodes = new HashSet<int> { 2 },
            Beams = new HashSet<int> { 4 },
            Servos = new HashSet<int> { servoId },
            Wheels = new HashSet<int> { wheel },
        });

        build.CopySelectedParts();

        var copy = build.Servos.Single(servo => servo.Id != servoId);
        var copiedWheel = build.Wheels.Single(other => other.Id != wheel);
        copy.TargetLinkId.ShouldBe(copiedWheel.Id);
        copiedWheel.NodeId.ShouldBe(copy.NodeId);
    }

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

    /// <summary><see cref="TwoBeams"/> with a Wheel on end joint 1, which has one link, beam 4.</summary>
    private static (BuildViewModel Build, int Wheel) WheelOnTheEndJoint()
    {
        var build = TwoBeams();
        var wheel = build.PlacePart(BuildPart.Wheel, _endJoint)!.Value;
        return (build, wheel);
    }
}
