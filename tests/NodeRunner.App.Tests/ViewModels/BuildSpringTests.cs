using NodeRunner.App.Builders;
using NodeRunner.App.Lifecycle;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.ViewModels;

/// <summary>Placing, refusing, selecting and tuning a Spring in Build (#453).</summary>
public sealed class BuildSpringTests
{
    [Fact]
    public void Drag_FromJointToJoint_PlacesASpring_AndTheToolStaysPicked()
    {
        var (build, gestures) = ThreeLooseJoints();

        Drag(gestures, new Vector2D(0, 0), new Vector2D(100, 0));

        build.Springs.ShouldBe([new SpringDef(4, 1, 2)]);
        build.Beams.ShouldBeEmpty();
        build.Pistons.ShouldBeEmpty();
        build.PickedLink.ShouldBe(BuildLink.Spring);
    }

    [Fact]
    public void Drag_OntoAPairAPistonJoins_PlacesNothing_AndSaysWhyThere()
    {
        var (build, gestures) = ThreeLooseJoints();
        build.ConnectLink(BuildLink.Piston, 1, 2).ShouldNotBeNull();
        build.PickLink(BuildLink.Spring);

        Drag(gestures, new Vector2D(0, 0), new Vector2D(100, 0));

        build.Springs.ShouldBeEmpty();
        build.PlacementNote.ShouldBe(new CanvasNote(CanvasNoteKind.Danger, new CreatureElementSelection(CreatureElementKind.Node, 2), CreatureBuilder.PistonJoinsTheseNodesReason));
    }

    [Fact]
    public void TapOnASpring_SelectsIt_BeforeTheBeamUnderIt()
    {
        var (build, gestures) = ThreeLooseJoints();
        var below = build.PlaceNode(new Vector2D(50, -60));
        var above = build.PlaceNode(new Vector2D(50, 60));
        build.ConnectBeam(below, above).ShouldBeTrue();
        var link = build.ConnectLink(BuildLink.Spring, 1, 2)!.Value;
        build.ActiveTool = BuildTool.Parts;

        Tap(gestures, new Vector2D(50, 0));

        build.SingleSelectedSpringId.ShouldBe(link);
        build.SelectedPartCount.ShouldBe(1);
        build.SelectedBeamCount.ShouldBe(0);
    }

    [Fact]
    public void DeleteSelectedParts_RemovesTheSelectedSpring_AndUndoBringsItBack()
    {
        var (build, _) = ThreeLooseJoints();
        var spring = build.ConnectLink(BuildLink.Spring, 1, 2)!.Value;
        build.SelectOnly(CreatureElementKind.Spring, spring);

        build.DeleteSelectedParts();
        build.Springs.ShouldBeEmpty();
        build.Nodes.Count.ShouldBe(3);

        build.Undo();
        build.Springs.ShouldBe([new SpringDef(spring, 1, 2)]);
    }

    [Fact]
    public void SetParameter_ChangesASpring_EvenWhenLocked()
    {
        var builder = new CreatureBuilder();
        builder.AddNode(new Vector2D(0, 0));
        builder.AddNode(new Vector2D(100, 0));
        var spring = builder.AddSpring(1, 2);
        var build = new BuildViewModel();
        build.Load(builder.Build(), moveOnly: true);
        build.SelectOnly(CreatureElementKind.Spring, spring);

        build.SetParameter(PartParameterId.Stiffness, 1200);
        build.SetParameter(PartParameterId.Damping, 0.8);

        build.Springs.Single().ShouldBe(new SpringDef(spring, 1, 2, null, 1200, 0.8));
    }

    [Fact]
    public void PickLink_WhenLocked_KeepsBeamPicked()
    {
        var build = new BuildViewModel();
        build.Load(new CreatureBuilder().Build(), moveOnly: true);
        build.ActiveTool = BuildTool.Beam;

        build.PickLink(BuildLink.Spring);

        build.PickedLink.ShouldBe(BuildLink.Beam);
    }

    [Fact]
    public void ASpring_AttachesItsNodes_AndTrainsWithNothingToDrive()
    {
        var builder = new CreatureBuilder();
        builder.AddNode(new Vector2D(0, 0));
        builder.AddNode(new Vector2D(100, 0));
        builder.AddSpring(1, 2);

        var creature = builder.Build();

        CreatureReadiness.Problems(creature).ShouldBeEmpty();
        BrainPorts.Of(creature).Outputs.ShouldBeEmpty();
        CreatureReadiness.CanTrain(creature).ShouldBeTrue();
    }

    [Fact]
    public void ASpringTooShortForItsNodes_IsAProblem()
    {
        var builder = new CreatureBuilder();
        builder.AddNode(new Vector2D(0, 0));
        builder.AddNode(new Vector2D(20, 0));
        builder.AddSpring(1, 2);

        CreatureReadiness.Problems(builder.Build()).ShouldContain(UiText.Format("The spring between joint {0} and joint {1} is too short. Move one of the joints apart.", 1, 2));
    }

    private static (BuildViewModel Build, BuildGestures Gestures) ThreeLooseJoints()
    {
        var build = new BuildViewModel();
        build.PlaceNode(new Vector2D(0, 0));
        build.PlaceNode(new Vector2D(100, 0));
        build.PlaceNode(new Vector2D(0, 100));
        build.ActiveTool = BuildTool.Beam;
        build.PickLink(BuildLink.Spring);
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
