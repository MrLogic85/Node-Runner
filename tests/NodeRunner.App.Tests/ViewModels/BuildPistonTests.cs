using NodeRunner.App.Builders;
using NodeRunner.App.Lifecycle;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.ViewModels;

/// <summary>Placing, refusing, selecting and tuning a Piston in Build (#451).</summary>
public sealed class BuildPistonTests
{
    [Fact]
    public void PickPart_PicksThePistonTool_AndPickingAgainPutsMoveBack()
    {
        var build = new BuildViewModel();

        build.PickPart(BuildPart.Piston);
        build.ActiveTool.ShouldBe(BuildTool.Piston);
        build.PickPart(BuildPart.Piston);
        build.ActiveTool.ShouldBe(BuildTool.Move);
    }

    [Fact]
    public void PickPart_ADraggedPart_ChangesNoTool()
    {
        var build = new BuildViewModel();

        build.PickPart(BuildPart.Camera);

        build.ActiveTool.ShouldBe(BuildTool.Move);
    }

    [Fact]
    public void Drag_FromJointToJoint_PlacesAPiston_AndTheToolStaysPicked()
    {
        var (build, gestures) = ThreeLooseJoints();

        Drag(gestures, new Vector2D(0, 0), new Vector2D(100, 0));
        Drag(gestures, new Vector2D(0, 0), new Vector2D(0, 100));

        build.Pistons.ShouldBe([new PistonDef(4, 1, 2), new PistonDef(5, 1, 3)]);
        build.Beams.ShouldBeEmpty();
        build.ActiveTool.ShouldBe(BuildTool.Piston);
        build.SelectedPartCount.ShouldBe(0);
    }

    [Fact]
    public void Drag_OntoAJointABeamAlreadyJoins_PlacesNothing_AndSaysWhyThere()
    {
        var (build, gestures) = ThreeLooseJoints();
        build.ConnectBeam(1, 2);

        gestures.Press(new Vector2D(0, 0));
        gestures.Drag(new Vector2D(98, 2));
        gestures.RefusedTargetNodeId.ShouldBe(2);
        gestures.BeamTargetNodeId.ShouldBeNull();
        gestures.Release(new Vector2D(98, 2));

        build.Pistons.ShouldBeEmpty();
        build.PlacementNote.ShouldBe(new CanvasNote(CanvasNoteKind.Danger, new CreatureElementSelection(CreatureElementKind.Node, 2), CreatureBuilder.BeamJoinsTheseNodesReason));
    }

    [Fact]
    public void Drag_ASecondPistonOnTheSamePair_IsRefused()
    {
        var (build, gestures) = ThreeLooseJoints();

        Drag(gestures, new Vector2D(0, 0), new Vector2D(100, 0));
        Drag(gestures, new Vector2D(100, 0), new Vector2D(0, 0));

        build.Pistons.Count.ShouldBe(1);
        build.StatusMessage.ShouldBe(CreatureBuilder.PistonJoinsTheseNodesReason);
    }

    [Fact]
    public void Drag_ReleasedAwayFromAJoint_PlacesNothing_AndSaysWhy()
    {
        var (build, gestures) = ThreeLooseJoints();

        Drag(gestures, new Vector2D(0, 0), new Vector2D(300, 300));

        build.Pistons.ShouldBeEmpty();
        build.Nodes.Count.ShouldBe(3);
        build.StatusMessage.ShouldBe("Drop it on another node.");
    }

    [Fact]
    public void ABeam_BetweenTwoNodesAPistonJoins_IsRefused()
    {
        var (build, _) = ThreeLooseJoints();
        build.ConnectPiston(1, 2);

        build.ConnectBeam(1, 2);

        build.Beams.ShouldBeEmpty();
    }

    [Fact]
    public void TapOnAPiston_SelectsIt_BeforeTheBeamUnderIt()
    {
        var (build, gestures) = ThreeLooseJoints();
        build.ConnectBeam(1, 3);
        build.ConnectPiston(1, 2);
        build.ActiveTool = BuildTool.Move;

        Tap(gestures, new Vector2D(50, 0));

        build.SingleSelectedPistonId.ShouldBe(5);
        build.SelectedBeamCount.ShouldBe(0);
    }

    [Fact]
    public void DeletingANode_RemovesItsPistons()
    {
        var (build, _) = ThreeLooseJoints();
        build.ConnectPiston(1, 2);
        build.ToggleSelected(new(CreatureElementKind.Node, 2));

        build.DeleteSelectedParts();

        build.Pistons.ShouldBeEmpty();
    }

    [Fact]
    public void DeleteSelectedParts_RemovesTheSelectedPiston()
    {
        var (build, _) = ThreeLooseJoints();
        var piston = build.ConnectPiston(1, 2)!.Value;
        build.SelectPiston(piston);

        build.DeleteSelectedParts();

        build.Pistons.ShouldBeEmpty();
        build.Nodes.Count.ShouldBe(3);
    }

    [Fact]
    public void SetPistonSettings_ChangesThem_EvenWhenLocked()
    {
        var builder = new CreatureBuilder();
        builder.AddNode(new Vector2D(0, 0));
        builder.AddNode(new Vector2D(100, 0));
        var piston = builder.AddPiston(1, 2);
        var build = new BuildViewModel();
        build.Load(builder.Build(), moveOnly: true);
        var changes = 0;
        build.AnatomyChanged += (_, _) => changes++;

        build.SetPistonSettings(piston, 20000, 0.5, 100);
        build.SetPistonSettings(piston, 20000, 0.5, 100);

        build.Pistons.Single().ShouldBe(new PistonDef(piston, 1, 2, null, 20000, 0.5, 100));
        changes.ShouldBe(1);
    }

    [Fact]
    public void PickPart_WhenLocked_KeepsMove()
    {
        var build = new BuildViewModel();
        build.Load(new CreatureBuilder().Build(), moveOnly: true);

        build.PickPart(BuildPart.Piston);

        build.ActiveTool.ShouldBe(BuildTool.Move);
    }

    [Fact]
    public void APiston_AttachesItsNodes_SoTheyAreReady()
    {
        var builder = new CreatureBuilder();
        builder.AddNode(new Vector2D(0, 0));
        builder.AddNode(new Vector2D(100, 0));
        builder.AddPiston(1, 2);

        var creature = builder.Build();

        CreatureReadiness.Problems(creature).ShouldBeEmpty();
        CreatureReadiness.CanTrain(creature).ShouldBeTrue();
    }

    [Fact]
    public void APistonTooShortForItsNodes_IsAProblem()
    {
        var builder = new CreatureBuilder();
        builder.AddNode(new Vector2D(0, 0));
        builder.AddNode(new Vector2D(20, 0));
        builder.AddPiston(1, 2);

        CreatureReadiness.Problems(builder.Build()).ShouldContain(problem => problem.StartsWith("The piston between", StringComparison.Ordinal));
    }

    private static (BuildViewModel Build, BuildGestures Gestures) ThreeLooseJoints()
    {
        var build = new BuildViewModel();
        build.PlaceNode(new Vector2D(0, 0));
        build.PlaceNode(new Vector2D(100, 0));
        build.PlaceNode(new Vector2D(0, 100));
        build.ActiveTool = BuildTool.Piston;
        return (build, new BuildGestures(build));
    }

    private static void Drag(BuildGestures gestures, Vector2D from, Vector2D to)
    {
        gestures.Press(from);
        gestures.Drag(new Vector2D((from.X + to.X) / 2, (from.Y + to.Y) / 2));
        gestures.Drag(to);
        gestures.Release(to);
    }

    private static void Tap(BuildGestures gestures, Vector2D position)
    {
        gestures.Press(position);
        gestures.Release(position);
    }
}
