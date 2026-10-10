using NodeRunner.App.Builders;
using NodeRunner.App.Lifecycle;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.ViewModels;

/// <summary>Placing, refusing, selecting and tuning a Piston in Build (#451).</summary>
public sealed class BuildPistonTests
{
    [Fact]
    public void PickLink_PicksPiston_AndKeepsTheBeamsTool()
    {
        var build = new BuildViewModel { ActiveTool = BuildTool.Beam };

        build.PickLink(BuildLink.Piston);

        build.ActiveTool.ShouldBe(BuildTool.Beam);
        build.PickedLink.ShouldBe(BuildLink.Piston);
    }

    [Fact]
    public void Drag_FromJointToJoint_PlacesAPiston_AndTheToolStaysPicked()
    {
        var (build, gestures) = ThreeLooseJoints();

        Drag(gestures, new Vector2D(0, 0), new Vector2D(100, 0));
        Drag(gestures, new Vector2D(0, 0), new Vector2D(0, 100));

        build.Pistons.ShouldBe([new PistonDef(4, 1, 2), new PistonDef(5, 1, 3)]);
        build.Beams.ShouldBeEmpty();
        build.ActiveTool.ShouldBe(BuildTool.Beam);
        build.PickedLink.ShouldBe(BuildLink.Piston);
        build.SelectedPartCount.ShouldBe(0);
    }

    [Fact]
    public void Drag_OntoAJointABeamAlreadyJoins_ShowsTheBeam_AndReplacesItOnRelease()
    {
        var (build, gestures) = ThreeLooseJoints();
        build.ConnectLink(BuildLink.Beam, 1, 2);
        var beam = build.Beams.Single().Id;

        gestures.Press(new Vector2D(0, 0));
        gestures.Drag(new Vector2D(98, 2));
        gestures.BeamTargetNodeId.ShouldBe(2);
        gestures.RefusedTargetNodeId.ShouldBeNull();
        gestures.ReplacedBeamId.ShouldBe(beam);
        gestures.Release(new Vector2D(98, 2));

        gestures.ReplacedBeamId.ShouldBeNull();
        build.Beams.ShouldBeEmpty();
        build.Pistons.Single().NodeA.ShouldBe(1);
        build.Pistons.Single().NodeB.ShouldBe(2);
        build.PlacementNote.ShouldBeNull();
    }

    [Fact]
    public void Drag_OntoAJointWhoseBeamHasASensor_IsRefusedWithWhy()
    {
        var (build, gestures) = ThreeLooseJoints();
        build.ConnectLink(BuildLink.Beam, 1, 2);
        build.PlacePart(BuildPart.Accelerometer, new CreatureElementSelection(CreatureElementKind.Beam, build.Beams.Single().Id));

        gestures.Press(new Vector2D(0, 0));
        gestures.Drag(new Vector2D(98, 2));
        gestures.RefusedTargetNodeId.ShouldBe(2);
        gestures.ReplacedBeamId.ShouldBeNull();
        gestures.Release(new Vector2D(98, 2));

        build.Pistons.ShouldBeEmpty();
        build.Beams.Count.ShouldBe(1);
        build.PlacementNote.ShouldBe(new CanvasNote(CanvasNoteKind.Danger, new CreatureElementSelection(CreatureElementKind.Node, 2), CreatureBuilder.SensorSitsOnThisBeamReason));
    }

    [Fact]
    public void Drag_OntoAFreeJoint_ReplacesNoBeam()
    {
        var (_, gestures) = ThreeLooseJoints();

        gestures.Press(new Vector2D(0, 0));
        gestures.Drag(new Vector2D(98, 2));

        gestures.BeamTargetNodeId.ShouldBe(2);
        gestures.ReplacedBeamId.ShouldBeNull();
    }

    [Fact]
    public void Drag_ASecondPistonOnTheSamePair_IsRefused()
    {
        var (build, gestures) = ThreeLooseJoints();

        Drag(gestures, new Vector2D(0, 0), new Vector2D(100, 0));
        Drag(gestures, new Vector2D(100, 0), new Vector2D(0, 0));

        build.Pistons.Count.ShouldBe(1);
        build.PlacementNote.ShouldBe(new CanvasNote(CanvasNoteKind.Danger, new CreatureElementSelection(CreatureElementKind.Node, 1), CreatureBuilder.PistonJoinsTheseNodesReason));
    }

    [Fact]
    public void Drag_ReleasedAwayFromAJoint_PlacesNothing()
    {
        var (build, gestures) = ThreeLooseJoints();

        Drag(gestures, new Vector2D(0, 0), new Vector2D(300, 300));

        build.Pistons.ShouldBeEmpty();
        build.Nodes.Count.ShouldBe(3);
        build.PlacementNote.ShouldBeNull();
    }

    [Fact]
    public void SecondFinger_CancelsAPistonPreview_AndKeepsSelectionAndPickedLink()
    {
        var (build, gestures) = ThreeLooseJoints();
        build.ReplaceSelection([3]);

        gestures.Press(new Vector2D(0, 0), 0);
        gestures.Drag(new Vector2D(100, 0), 0);
        gestures.BeamStartNodeId.ShouldBe(1);
        gestures.Press(new Vector2D(200, 200), 1);
        gestures.BeamStartNodeId.ShouldBeNull();
        gestures.RefusedTargetNodeId.ShouldBeNull();
        gestures.Release(new Vector2D(100, 0), 0);
        gestures.Release(new Vector2D(200, 200), 1);

        build.Pistons.ShouldBeEmpty();
        build.SelectedNodeIds.ShouldBe([3]);
        build.PickedLink.ShouldBe(BuildLink.Piston);
    }

    [Fact]
    public void ABeam_BetweenTwoNodesAPistonJoins_IsRefused()
    {
        var (build, _) = ThreeLooseJoints();
        build.ConnectLink(BuildLink.Piston, 1, 2);

        build.ConnectLink(BuildLink.Beam, 1, 2);

        build.Beams.ShouldBeEmpty();
    }

    [Fact]
    public void TapOnAPiston_SelectsIt_WhereItIsDrawnOverABeam()
    {
        // The Piston reaches lower than the beam, so it draws over it there (#1107).
        var (build, gestures) = ThreeLooseJoints();
        var left = build.PlaceNode(new Vector2D(-60, 50));
        var right = build.PlaceNode(new Vector2D(60, 50));
        build.ConnectLink(BuildLink.Beam, left, right).ShouldNotBeNull();
        var link = build.ConnectLink(BuildLink.Piston, 1, 3)!.Value;
        build.ActiveTool = BuildTool.Parts;

        Tap(gestures, new Vector2D(0, 50));

        build.SingleSelectedPistonId.ShouldBe(link);
        build.SelectedBeamCount.ShouldBe(0);
    }

    // #1107: a selection rises to the selected surface, so a touch hits it where it now covers the Piston.
    [Fact]
    public void ASelectedBeamOrJoint_RaisesTheBeam_SoATouchHitsItOverAPiston()
    {
        var (build, _) = ThreeLooseJoints();
        var left = build.PlaceNode(new Vector2D(-60, 50));
        var right = build.PlaceNode(new Vector2D(60, 50));
        var beam = new CreatureElementSelection(CreatureElementKind.Beam, build.ConnectLink(BuildLink.Beam, left, right)!.Value);
        var piston = new CreatureElementSelection(CreatureElementKind.Piston, build.ConnectLink(BuildLink.Piston, 1, 3)!.Value);
        var crossing = new Vector2D(0, 50);
        build.DrawnPartAt(crossing, PlacingTargets.None).ShouldBe(piston);

        build.ReplaceSelection(PartSet.Of(beam));
        build.DrawnPartAt(crossing, PlacingTargets.None).ShouldBe(beam);

        build.ReplaceSelection(PartSet.Of(new CreatureElementSelection(CreatureElementKind.Node, right)));
        build.DrawnPartAt(crossing, PlacingTargets.None).ShouldBe(beam);

        var servo = build.PlacePart(BuildPart.Servo, new CreatureElementSelection(CreatureElementKind.Node, left)).ShouldNotBeNull();
        build.ReplaceSelection(PartSet.Of(new CreatureElementSelection(CreatureElementKind.Servo, servo)));
        build.DrawnPartAt(crossing, PlacingTargets.None).ShouldBe(beam);
        build.RaisedParts(build.Selection, PlacingTargets.None).Links.ShouldContain(beam.Id);
    }

    [Fact]
    public void DeletingANode_RemovesItsPistons()
    {
        var (build, _) = ThreeLooseJoints();
        build.ConnectLink(BuildLink.Piston, 1, 2);
        build.ToggleSelected(new(CreatureElementKind.Node, 2));

        build.DeleteSelectedParts();

        build.Pistons.ShouldBeEmpty();
    }

    [Fact]
    public void DeleteSelectedParts_RemovesTheSelectedPiston()
    {
        var (build, _) = ThreeLooseJoints();
        var piston = build.ConnectLink(BuildLink.Piston, 1, 2)!.Value;
        build.SelectOnly(CreatureElementKind.Piston, piston);

        build.DeleteSelectedParts();

        build.Pistons.ShouldBeEmpty();
        build.Nodes.Count.ShouldBe(3);
    }

    [Fact]
    public void SetParameter_ChangesAPiston_EvenWhenLocked()
    {
        var builder = new CreatureBuilder();
        builder.AddNode(new Vector2D(0, 0));
        builder.AddNode(new Vector2D(100, 0));
        var piston = builder.AddPiston(1, 2);
        var build = new BuildViewModel();
        build.Load(builder.Build(), locked: true);
        build.SelectOnly(CreatureElementKind.Piston, piston);
        var changes = 0;
        build.AnatomyChanged += (_, _) => changes++;

        build.SetParameter(PartParameterId.Strength, 20000);
        build.SetParameter(PartParameterId.Stroke, 0.7);
        build.SetParameter(PartParameterId.StartPosition, 0.25);
        build.SetParameter(PartParameterId.MaxSpeed, 100);
        build.SetParameter(PartParameterId.MaxSpeed, 100);

        build.Pistons.Single().ShouldBe(new PistonDef(piston, 1, 2, null, 20000, 0.7, 0.25, 100));
        changes.ShouldBe(4);
    }

    [Fact]
    public void PickLink_WhenLocked_KeepsBeamPicked()
    {
        var build = new BuildViewModel();
        build.Load(new CreatureBuilder().Build(), locked: true);
        build.ActiveTool = BuildTool.Beam;

        build.PickLink(BuildLink.Piston);

        build.PickedLink.ShouldBe(BuildLink.Beam);
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

        CreatureReadiness.Problems(builder.Build()).ShouldContain(UiText.Format("The piston between joint {0} and joint {1} is too short. Move one of the joints apart.", 1, 2));
    }

    private static (BuildViewModel Build, BuildGestures Gestures) ThreeLooseJoints()
    {
        var build = new BuildViewModel();
        build.PlaceNode(new Vector2D(0, 0));
        build.PlaceNode(new Vector2D(100, 0));
        build.PlaceNode(new Vector2D(0, 100));
        build.ActiveTool = BuildTool.Beam;
        build.PickLink(BuildLink.Piston);
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
