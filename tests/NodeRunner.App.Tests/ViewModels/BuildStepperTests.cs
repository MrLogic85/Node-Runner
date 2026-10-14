using NodeRunner.App.Builders;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.ViewModels;

/// <summary>
/// A Stepper in Build (#1036) before it can be placed (#1038): undo, relinking, copying, deleting and
/// box selecting keep it as they keep a Velocity motor, and its brain ports guard a locked Creation.
/// </summary>
public sealed class BuildStepperTests
{
    private const int _stepperId = 6;
    private static readonly CreatureElementSelection _stepper = new(CreatureElementKind.Stepper, _stepperId);

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
