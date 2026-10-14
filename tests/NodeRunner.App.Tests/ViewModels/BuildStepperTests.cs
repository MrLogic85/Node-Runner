using NodeRunner.App.Builders;
using NodeRunner.App.Lifecycle;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.ViewModels;

/// <summary>
/// A Stepper in Build (#1036, #1038): placing, its panel, undo, relinking, copying, deleting and box
/// selecting work as they do for a Velocity motor, and its brain ports guard a locked Creation.
/// </summary>
public sealed class BuildStepperTests
{
    private const int _stepperId = 6;
    private static readonly CreatureElementSelection _stepper = new(CreatureElementKind.Stepper, _stepperId);

    private static readonly CreatureElementSelection _endJoint = new(CreatureElementKind.Node, 1);
    private static readonly CreatureElementSelection _middleJoint = new(CreatureElementKind.Node, 2);

    [Fact]
    public void PlacePart_OnAJoint_TakesTheDefaultSides_SelectsIt_AndCanTrain()
    {
        var build = TwoBeams();

        var stepperId = build.PlacePart(BuildPart.Stepper, _middleJoint).ShouldNotBeNull();

        build.Steppers.Single().ShouldBe(new StepperDef(stepperId, 2, fixedLinkId: 4, targetLinkId: 5));
        build.SingleSelectedStepperId.ShouldBe(stepperId);
        CreatureReadiness.CanTrain(build.Snapshot()).ShouldBeTrue();
        build.Undo();
        build.Steppers.ShouldBeEmpty();
    }

    [Fact]
    public void PlacePart_OnAOneLinkJointWithAWheel_TurnsTheWheel()
    {
        var build = TwoBeams();
        var wheel = build.PlacePart(BuildPart.Wheel, _endJoint)!.Value;

        build.PlacePart(BuildPart.Stepper, _endJoint);

        build.Steppers.Single().FixedLinkId.ShouldBe(4);
        build.Steppers.Single().TargetLinkId.ShouldBe(wheel);
    }

    [Fact]
    public void CanPlacePart_OnABeam_IsRefused()
    {
        var build = TwoBeams();

        build.CanPlacePart(BuildPart.Stepper, new CreatureElementSelection(CreatureElementKind.Beam, 4), out var reason).ShouldBeFalse();

        reason.ShouldBe(CreatureBuilder.SteppersGoOnAJointReason);
    }

    [Theory]
    [InlineData(BuildPart.Servo, BuildPart.Stepper)]
    [InlineData(BuildPart.VelocityMotor, BuildPart.Stepper)]
    [InlineData(BuildPart.Brake, BuildPart.Stepper)]
    [InlineData(BuildPart.AngleSensor, BuildPart.Stepper)]
    [InlineData(BuildPart.Stepper, BuildPart.Servo)]
    [InlineData(BuildPart.Stepper, BuildPart.VelocityMotor)]
    [InlineData(BuildPart.Stepper, BuildPart.Brake)]
    [InlineData(BuildPart.Stepper, BuildPart.AngleSensor)]
    public void AJointHoldsOneHingePart_StepperIncluded(BuildPart first, BuildPart second)
    {
        var build = TwoBeams();
        build.PlacePart(first, _middleJoint);

        build.CanPlacePart(second, _middleJoint, out var reason).ShouldBeFalse();

        reason.ShouldBe(CreatureBuilder.OneHingePartPerJointReason);
    }

    [Fact]
    public void Locked_PlacingIsRefused()
    {
        var build = new BuildViewModel();
        build.Load(new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(100, 0)), new NodeDef(3, new Vector2D(200, 0))],
            [new BeamDef(4, 1, 2), new BeamDef(5, 2, 3)],
            [],
            nextPartId: 6), locked: true);

        build.CanPlacePart(BuildPart.Stepper, _middleJoint, out var reason).ShouldBeFalse();
        reason.ShouldBe(BuildViewModel.LockedReason);
    }

    [Fact]
    public void SelectedStepper_ShowsItsNote_SettingsAndPickers()
    {
        var build = new BuildViewModel();
        build.Load(WithAStepper());
        build.SelectOnly(CreatureElementKind.Stepper, _stepperId);

        var part = new BuildPresentationViewModel(build).SinglePart!;

        part.Kind.ShouldBe(PartKind.Stepper);
        part.Title.ShouldBe(UiText.Plain("Stepper"));
        part.Note.ShouldBe(PartInfo.Stepper);
        part.BasicSettings.Select(slider => slider.Id).ShouldBe([PartParameterId.ServoStrength, PartParameterId.StepsPerTurn]);
        part.AdvancedSettings.Select(slider => slider.Id).ShouldBe([PartParameterId.MaxFrequency]);
        part.Pickers!.Select(picker => (picker.Label, picker.SelectedIndex)).ShouldBe(
        [
            (UiText.Plain("Fixed part"), 0),
            (UiText.Plain("Target part"), 1),
        ]);
    }

    // A Stepper's Max strength is the Servo's setting, so selecting both edits it on each (#1036).
    [Fact]
    public void AStepperSelectedWithAServo_SharesMaxStrength_AndSettingItSetsBoth()
    {
        var build = TwoBeams();
        var stepperId = build.PlacePart(BuildPart.Stepper, _middleJoint)!.Value;
        build.PlacePart(BuildPart.Wheel, _endJoint);
        var servoId = build.PlacePart(BuildPart.Servo, _endJoint)!.Value;
        build.ReplaceSelection(PartSet.None with { Servos = new HashSet<int> { servoId }, Steppers = new HashSet<int> { stepperId } });

        var selection = new BuildPresentationViewModel(build).Selection!;
        selection.BasicSettings.Select(slider => slider.Id).ShouldContain(PartParameterId.ServoStrength);
        build.SetParameter(PartParameterId.ServoStrength, 400000);

        build.Steppers.Single().Strength.ShouldBe(400000);
        build.Servos.Single().Strength.ShouldBe(400000);
    }

    // While placing, the free joints take a ring at the motor's radius and a taken joint is refused (#1038).
    [Fact]
    public void PlacingTargets_RingTheFreeJointsAtTheMotorRadius_AndRefuseATakenJoint()
    {
        var build = TwoBeams();
        build.PlacePart(BuildPart.Servo, _middleJoint);

        var targets = build.PlacingTargetsOf(BuildPart.Stepper, null);

        targets.Joints.ShouldBe([1, 3], ignoreOrder: true);
        targets.RefusedJoints.ShouldBe([2]);
        targets.JointRingOf(1).ShouldBe(JointMotor.Radius);
    }

    [Fact]
    public void DeletingItsWheel_LeavesTheStepperASideShort_AndSaysWhy()
    {
        var build = TwoBeams();
        var wheel = build.PlacePart(BuildPart.Wheel, _endJoint)!.Value;
        var stepperId = build.PlacePart(BuildPart.Stepper, _endJoint)!.Value;
        build.SelectOnly(CreatureElementKind.Wheel, wheel);

        build.DeleteSelectedParts();

        build.Steppers.Single().ShouldBe(new StepperDef(stepperId, 1, fixedLinkId: 4, targetLinkId: null));
        CreatureReadiness.CanTrain(build.Snapshot()).ShouldBeFalse();
        build.ShowTrainingBlockers();
        build.CanvasNotes().Single(note => note.Target.Id == stepperId).Text.ShouldBe(UiText.Plain("A Stepper needs two links, or a link and a Wheel"));
        build.SelectOnly(CreatureElementKind.Stepper, stepperId);
        var pickers = new BuildPresentationViewModel(build).SinglePart!.Pickers!;
        pickers[0].Note.ShouldBeNull();
        pickers[1].Note.ShouldBe(UiText.Plain("A Stepper needs two links, or a link and a Wheel"));
    }

    [Fact]
    public void SettingOnlyAStepper_IsAnUndoStep()
    {
        var build = new BuildViewModel();
        build.Load(WithAStepper());
        build.SelectOnly(CreatureElementKind.Stepper, _stepperId);

        build.SetParameter(PartParameterId.StepsPerTurn, 12);

        build.Steppers.Single().StepsPerTurn.ShouldBe(12);
        build.CanUndo.ShouldBeTrue();
        build.Undo();
        build.Steppers.Single().StepsPerTurn.ShouldBe(StepperDef.DefaultStepsPerTurn);
        build.Redo();
        build.Steppers.Single().StepsPerTurn.ShouldBe(12);
    }

    [Fact]
    public void SetHingePartLink_OnAStepper_SwapsItsSides_KeepsItsId_AndUndoes()
    {
        var build = new BuildViewModel();
        build.Load(WithAStepper());
        build.SelectOnly(CreatureElementKind.Stepper, _stepperId);

        build.SetHingePartLink(_stepperId, fixedRole: true, linkId: 5);

        build.Steppers.Single().ShouldBe(new StepperDef(_stepperId, 2, fixedLinkId: 5, targetLinkId: 4));
        build.SingleSelectedStepperId.ShouldBe(_stepperId);
        build.Undo();
        build.Steppers.Single().ShouldBe(new StepperDef(_stepperId, 2, fixedLinkId: 4, targetLinkId: 5));
    }

    [Fact]
    public void Copy_TakesTheStepperOntoTheCopiedLinks_WithItsSettings()
    {
        var build = new BuildViewModel();
        build.Load(WithAStepper(stepsPerTurn: 12, maxFrequency: 1.5));
        build.ReplaceSelection(PartSet.None with
        {
            Nodes = new HashSet<int> { 1, 2, 3 },
            Beams = new HashSet<int> { 4, 5 },
            Steppers = new HashSet<int> { _stepperId },
        });

        build.CopySelectedParts();

        var copy = build.Steppers.Where(stepper => stepper.Id != _stepperId).ShouldHaveSingleItem();
        var beams = build.Beams.Skip(2).Select(beam => beam.Id).ToArray();
        (copy.FixedLinkId, copy.TargetLinkId, copy.StepsPerTurn, copy.MaxFrequency).ShouldBe((beams[0], beams[1], 12, 1.5));
        copy.NodeId.ShouldBe(build.Nodes.Skip(3).ElementAt(1).Id);
        build.Selection.Steppers.ShouldBe([copy.Id]);
    }

    [Fact]
    public void DeletingItsJoint_TakesTheStepper_AndUndoBringsItBack()
    {
        var build = new BuildViewModel();
        build.Load(WithAStepper());
        build.SelectOnly(CreatureElementKind.Node, 2);

        build.DeleteSelectedParts();

        build.Steppers.ShouldBeEmpty();
        build.Undo();
        build.Steppers.ShouldBe([new StepperDef(_stepperId, 2, fixedLinkId: 4, targetLinkId: 5)]);
    }

    [Theory]
    [InlineData(CreatureElementKind.Stepper, _stepperId)]
    [InlineData(CreatureElementKind.Node, 2)]
    [InlineData(CreatureElementKind.Beam, 4)]
    public void Locked_DeletingIt_ItsJoint_OrASide_IsRefused_WithANoteOnIt(CreatureElementKind kind, int id)
    {
        var build = new BuildViewModel();
        build.Load(WithAStepper(), locked: true);
        build.SelectOnly(kind, id);

        build.DeleteLockedReason.ShouldBe(BuildViewModel.LockedReason);
        build.DeleteSelectedParts();

        build.Steppers.ShouldBe([new StepperDef(_stepperId, 2, fixedLinkId: 4, targetLinkId: 5)]);
        build.CanvasNotes().ShouldContain(new CanvasNote(CanvasNoteKind.Danger, _stepper, UiText.Plain("Locked: would change the model")));
    }

    [Fact]
    public void Locked_CopyingIt_IsBlocked_WithANoteOnIt()
    {
        var build = new BuildViewModel();
        build.Load(WithAStepper(), locked: true);
        build.ReplaceSelection(PartSet.None with { Nodes = new HashSet<int> { 1, 3 }, Beams = new HashSet<int> { 4, 5 }, Steppers = new HashSet<int> { _stepperId } });

        build.CopyBlockers().ShouldBe([new CanvasNote(CanvasNoteKind.Danger, _stepper, UiText.Plain("Locked: would change the model"))]);
        build.CopySelectedParts();

        build.Steppers.ShouldHaveSingleItem();
    }

    [Fact]
    public void Box_OverItsJoint_SelectsTheStepper_InPlaceOfTheJoint()
    {
        var build = new BuildViewModel();
        build.Load(WithAStepper());
        build.ActiveTool = BuildTool.Select;
        var gestures = new BuildGestures(build);

        gestures.Press(new Vector2D(70, -70));
        gestures.Drag(new Vector2D(130, 70));
        gestures.Release(new Vector2D(130, 70));

        build.Selection.Nodes.ShouldBeEmpty();
        build.Selection.Steppers.ShouldBe([_stepperId]);
    }

    // Joints 1, 2 and 3 joined by beams 4 and 5, with no parts yet.
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

    // Joint 2 between beams 4 and 5 holds Stepper 6.
    private static CreatureDef WithAStepper(int stepsPerTurn = StepperDef.DefaultStepsPerTurn, double maxFrequency = StepperDef.DefaultMaxFrequency)
    {
        var builder = new CreatureBuilder(new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(100, 0)), new NodeDef(3, new Vector2D(200, 0))],
            [new BeamDef(4, 1, 2), new BeamDef(5, 2, 3)],
            [],
            nextPartId: _stepperId));
        var stepper = builder.AddStepper(2);
        builder.SetParameter(stepper, PartParameterId.StepsPerTurn, stepsPerTurn);
        builder.SetParameter(stepper, PartParameterId.MaxFrequency, maxFrequency);
        return builder.Build();
    }
}
