using NodeRunner.App.Builders;
using NodeRunner.App.Lifecycle;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.ViewModels;

/// <summary>Placing, setting up and relinking a Velocity motor in Build, as a Servo is (#454).</summary>
public sealed class BuildVelocityMotorTests
{
    private static readonly CreatureElementSelection _endJoint = new(CreatureElementKind.Node, 1);
    private static readonly CreatureElementSelection _middleJoint = new(CreatureElementKind.Node, 2);

    [Fact]
    public void PlacePart_OnAJoint_TakesTheDefaultSides_SelectsIt_AndCanTrain()
    {
        var build = TwoBeams();

        var motorId = build.PlacePart(BuildPart.VelocityMotor, _middleJoint).ShouldNotBeNull();

        build.VelocityMotors.Single().ShouldBe(new VelocityMotorDef(motorId, 2, fixedLinkId: 4, targetLinkId: 5));
        build.SingleSelectedVelocityMotorId.ShouldBe(motorId);
        CreatureReadiness.CanTrain(build.Snapshot()).ShouldBeTrue();
        build.Undo();
        build.VelocityMotors.ShouldBeEmpty();
    }

    [Fact]
    public void PlacePart_OnAOneLinkJointWithAWheel_TurnsTheWheel()
    {
        var build = TwoBeams();
        var wheel = build.PlacePart(BuildPart.Wheel, _endJoint)!.Value;

        build.PlacePart(BuildPart.VelocityMotor, _endJoint);

        build.VelocityMotors.Single().FixedLinkId.ShouldBe(4);
        build.VelocityMotors.Single().TargetLinkId.ShouldBe(wheel);
    }

    [Fact]
    public void CanPlacePart_OnABeam_IsRefused()
    {
        var build = TwoBeams();

        build.CanPlacePart(BuildPart.VelocityMotor, new CreatureElementSelection(CreatureElementKind.Beam, 4), out var reason).ShouldBeFalse();

        reason.ShouldBe(CreatureBuilder.VelocityMotorsGoOnAJointReason);
    }

    [Theory]
    [InlineData(BuildPart.Servo, BuildPart.VelocityMotor)]
    [InlineData(BuildPart.VelocityMotor, BuildPart.Servo)]
    public void AJointHoldsOneMotor_ServoOrVelocityMotor(BuildPart first, BuildPart second)
    {
        var build = TwoBeams();
        build.PlacePart(first, _middleJoint);

        build.CanPlacePart(second, _middleJoint, out var reason).ShouldBeFalse();

        reason.ShouldBe(CreatureBuilder.OneMotorOrBrakePerJointReason);
    }

    [Fact]
    public void Locked_PlacingAndRelinking_AreRefused_AndThePickersAreLocked()
    {
        var build = new BuildViewModel();
        build.Load(WithAMotor(), locked: true);
        build.SelectOnly(CreatureElementKind.VelocityMotor, 6);

        build.CanPlacePart(BuildPart.VelocityMotor, _endJoint, out var reason).ShouldBeFalse();
        reason.ShouldBe(BuildViewModel.LockedReason);
        build.SetMotorLink(6, fixedRole: true, linkId: 5).ShouldBeNull();
        build.VelocityMotors.Single().Id.ShouldBe(6);
        new BuildPresentationViewModel(build).SinglePart!.Pickers!.ShouldAllBe(picker => picker.IsLocked);
    }

    [Fact]
    public void SelectedVelocityMotor_ShowsItsNote_SettingsAndPickers()
    {
        var build = new BuildViewModel();
        build.Load(WithAMotor());
        build.SelectOnly(CreatureElementKind.VelocityMotor, 6);

        var part = new BuildPresentationViewModel(build).SinglePart!;

        part.Kind.ShouldBe(PartSettingsKind.VelocityMotor);
        part.Title.ShouldBe(UiText.Plain("Motor"));
        part.Note.ShouldBe(PartInfo.VelocityMotor);
        part.JointId.ShouldBe(2);
        part.BasicSettings.Select(slider => slider.Id).ShouldBe([PartParameterId.MotorStrength]);
        part.AdvancedSettings.Select(slider => slider.Id).ShouldBe([PartParameterId.MotorMaxSpeed, PartParameterId.RiseTime]);
        part.Pickers!.Select(picker => (picker.Label, picker.SelectedIndex, picker.IsLocked)).ShouldBe(
        [
            (UiText.Plain("Fixed link"), 0, false),
            (UiText.Plain("Target link"), 1, false),
        ]);
    }

    [Fact]
    public void SetMotorLink_OnAVelocityMotor_SwapsItsSides_ReselectsItsNewId_AndUndoes()
    {
        var build = new BuildViewModel();
        build.Load(WithAMotor());
        build.SelectOnly(CreatureElementKind.VelocityMotor, 6);

        var swapped = build.SetMotorLink(6, fixedRole: true, linkId: 5).ShouldNotBeNull();

        swapped.ShouldNotBe(6);
        build.VelocityMotors.Single().ShouldBe(new VelocityMotorDef(swapped, 2, fixedLinkId: 5, targetLinkId: 4));
        build.SingleSelectedVelocityMotorId.ShouldBe(swapped);
        build.Undo();
        build.VelocityMotors.Single().Id.ShouldBe(6);
    }

    [Fact]
    public void DeleteSelectedParts_RemovesTheMotor_KeepsItsLinks_AndUndoBringsItBack()
    {
        var build = new BuildViewModel();
        build.Load(WithAMotor());
        build.SelectOnly(CreatureElementKind.VelocityMotor, 6);

        build.DeleteSelectedParts();

        build.VelocityMotors.ShouldBeEmpty();
        build.Beams.Count.ShouldBe(2);
        build.Selection.Count.ShouldBe(0);
        build.Undo();
        build.VelocityMotors.ShouldBe([new VelocityMotorDef(6, 2, fixedLinkId: 4, targetLinkId: 5)]);
        build.Selection.VelocityMotors.ShouldBe([6]);
    }

    [Fact]
    public void DeleteSelectedParts_OfItsFixedLink_LeavesTheMotorWithoutIt_AndUndoGivesItBack()
    {
        var build = new BuildViewModel();
        build.Load(WithAMotor());
        build.SelectOnly(CreatureElementKind.Beam, 4);

        build.DeleteSelectedParts();

        build.VelocityMotors.Single().ShouldBe(new VelocityMotorDef(6, 2, fixedLinkId: null, targetLinkId: 5));
        build.Undo();
        build.VelocityMotors.Single().ShouldBe(new VelocityMotorDef(6, 2, fixedLinkId: 4, targetLinkId: 5));
    }

    [Fact]
    public void AVelocityMotorMissingASide_BlocksTraining_WithItsOwnReason()
    {
        var build = TwoBeams();
        var motorId = build.PlacePart(BuildPart.VelocityMotor, _endJoint)!.Value;

        CreatureReadiness.CanTrain(build.Snapshot()).ShouldBeFalse();
        build.ShowTrainingBlockers();
        build.CanvasNotes().Single(note => note.Target.Id == motorId).Text.ShouldBe(CreatureBuilder.VelocityMotorNeedsTwoSidesReason);
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

    private static CreatureDef WithAMotor() => new(
        [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(100, 0)), new NodeDef(3, new Vector2D(200, 0))],
        [new BeamDef(4, 1, 2), new BeamDef(5, 2, 3)],
        [],
        [],
        [],
        [],
        [],
        [],
        [],
        [new VelocityMotorDef(6, 2, fixedLinkId: 4, targetLinkId: 5)],
        nextPartId: 7);
}
