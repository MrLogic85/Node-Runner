using NodeRunner.App.Builders;
using NodeRunner.App.Lifecycle;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;
using static NodeRunner.App.Tests.ViewModels.BuildFixtures;

namespace NodeRunner.App.Tests.ViewModels;

/// <summary>Drawing, refusing, selecting, tuning and deleting a Piston (#451) or a Spring (#453).</summary>
public sealed class BuildLinkTests
{
    [Theory]
    [InlineData(BuildLink.Piston)]
    [InlineData(BuildLink.Spring)]
    public void ALink_IsDrawnJointToJoint_ReplacesABeam_RefusesASecond_AndIsSelectedAndDeleted(BuildLink kind)
    {
        var (build, gestures) = ThreeLooseJoints(BuildTool.Beam);
        build.PickLink(kind);
        IReadOnlyList<(int Id, int A, int B)> Links() => kind == BuildLink.Piston
            ? [.. build.Pistons.Select(link => (link.Id, link.NodeA, link.NodeB))]
            : [.. build.Springs.Select(link => (link.Id, link.NodeA, link.NodeB))];

        gestures.DragAcross(new Vector2D(0, 0), new Vector2D(100, 0));
        Links().ShouldBe([(4, 1, 2)]);
        (build.ActiveTool, build.PickedLink, build.SelectedPartCount).ShouldBe((BuildTool.Beam, kind, 0));

        gestures.DragAcross(new Vector2D(100, 0), new Vector2D(0, 0));
        Links().Count.ShouldBe(1);
        var joins = kind == BuildLink.Piston ? CreatureBuilder.PistonJoinsTheseNodesReason : CreatureBuilder.SpringJoinsTheseNodesReason;
        build.PlacementNote.ShouldBe(new CanvasNote(CanvasNoteKind.Danger, Joint(1), joins));

        gestures.DragAcross(new Vector2D(0, 0), new Vector2D(300, 300));
        (Links().Count, build.Nodes.Count, build.PlacementNote).ShouldBe((1, 3, null));

        var beam = build.ConnectLink(BuildLink.Beam, 1, 3)!.Value;
        gestures.Press(new Vector2D(0, 0));
        gestures.Drag(new Vector2D(2, 98));
        (gestures.BeamTargetNodeId, gestures.RefusedTargetNodeId, gestures.ReplacedLinkId).ShouldBe((3, null, beam));
        gestures.Release(new Vector2D(2, 98));
        gestures.ReplacedLinkId.ShouldBeNull();
        build.Beams.ShouldBeEmpty();
        Links().Last().ShouldBe((beam + 1, 1, 3));

        build.ConnectLink(BuildLink.Beam, 2, 3);
        build.PlacePart(BuildPart.Accelerometer, Beam(build.Beams.Single().Id));
        gestures.Press(new Vector2D(100, 0));
        gestures.Drag(new Vector2D(2, 98));
        (gestures.RefusedTargetNodeId, gestures.ReplacedLinkId).ShouldBe((3, null));
        gestures.Release(new Vector2D(2, 98));
        build.PlacementNote.ShouldBe(new CanvasNote(CanvasNoteKind.Danger, Joint(3), CreatureBuilder.SensorSitsOnThisBeamReason));

        build.ReplaceSelection([3]);
        gestures.Press(new Vector2D(0, 0), 0);
        gestures.Drag(new Vector2D(100, 0), 0);
        gestures.BeamStartNodeId.ShouldBe(1);
        gestures.Press(new Vector2D(200, 200), 1);
        (gestures.BeamStartNodeId, gestures.RefusedTargetNodeId).ShouldBe((null, null));
        gestures.Release(new Vector2D(100, 0), 0);
        gestures.Release(new Vector2D(200, 200), 1);
        (Links().Count, build.PickedLink).ShouldBe((2, kind));
        build.SelectedNodeIds.ShouldBe([3]);

        // The link reaches lower than the beam, so it draws over it there (#1107), until the beam is selected.
        var left = build.PlaceNode(new Vector2D(-60, 50));
        var crossing = Beam(build.ConnectLink(BuildLink.Beam, left, build.PlaceNode(new Vector2D(60, 50)))!.Value);
        var link = Links().Last().Id;
        build.ActiveTool = BuildTool.Parts;
        build.ClearSelection();
        gestures.Tap(new Vector2D(0, 50));
        build.Selection.Parts.ShouldBe([new CreatureElementSelection(Enum.Parse<CreatureElementKind>(kind.ToString()), link)]);
        build.ReplaceSelection(PartSet.Of(crossing));
        build.DrawnPartAt(new Vector2D(0, 50), PlacingTargets.None).ShouldBe(crossing);
        build.SelectOnly(CreatureElementKind.Node, left);
        build.DrawnPartAt(new Vector2D(0, 50), PlacingTargets.None).ShouldBe(crossing);

        build.SelectOnly(Enum.Parse<CreatureElementKind>(kind.ToString()), link);
        build.DeleteSelectedParts();
        Links().Count.ShouldBe(1);
        build.Nodes.Count.ShouldBe(5);
        build.Undo();
        Links().Count.ShouldBe(2);
        build.SelectOnly(CreatureElementKind.Node, 1);
        build.DeleteSelectedParts();
        Links().ShouldBeEmpty();
    }

    [Fact]
    public void ABeam_ReplacesATunedPiston_ASpringReplacesAPiston_AndOneUndoBringsItBack()
    {
        var (build, _) = ThreeLooseJoints(BuildTool.Beam);
        var piston = build.ConnectLink(BuildLink.Piston, 1, 2)!.Value;
        build.SelectOnly(CreatureElementKind.Piston, piston);
        build.SetParameter(PartParameterId.Strength, 20000);
        var tuned = build.Pistons.Single();

        var beam = build.ConnectLink(BuildLink.Beam, 2, 1)!.Value;
        build.Pistons.ShouldBeEmpty();
        build.Beams.Single().Id.ShouldBe(beam);
        build.Undo();
        build.Pistons.ShouldBe([tuned]);
        build.Beams.ShouldBeEmpty();

        build.ConnectLink(BuildLink.Spring, 1, 2).ShouldNotBeNull();
        build.Pistons.ShouldBeEmpty();
        build.Springs.Single().NodeB.ShouldBe(2);
    }

    [Fact]
    public void Locked_KeepsPistonsAndTheirPick_ButTunesThemAndSprings_AndDrawsSprings()
    {
        var build = Loaded(TwoBeamsDef(new PistonDef(6, 1, 3), new NodeDef(8, new Vector2D(0, 100)), new SpringDef(7, 1, 8)), locked: true);
        build.ActiveTool = BuildTool.Beam;
        build.PickLink(BuildLink.Piston);
        build.PickedLink.ShouldBe(BuildLink.Beam);
        build.PickLink(BuildLink.Spring);
        build.PickedLink.ShouldBe(BuildLink.Spring);
        build.ActiveTool = BuildTool.Select;

        build.SelectOnly(CreatureElementKind.Piston, 6);
        var changes = build.CountAnatomyChanges();
        build.SetParameter(PartParameterId.Strength, 20000);
        build.SetParameter(PartParameterId.Stroke, 0.7);
        build.SetParameter(PartParameterId.StartPosition, 0.25);
        build.SetParameter(PartParameterId.MaxSpeed, 100);
        build.SetParameter(PartParameterId.MaxSpeed, 100);
        build.Pistons.Single().ShouldBe(new PistonDef(6, 1, 3, null, 20000, 0.7, 0.25, 100));
        changes().ShouldBe(4);

        build.SelectOnly(CreatureElementKind.Spring, 7);
        build.SetParameter(PartParameterId.Stiffness, 1200);
        build.SetParameter(PartParameterId.Damping, 0.8);
        build.Springs.Single().ShouldBe(new SpringDef(7, 1, 8, null, 1200, 0.8));
    }

    [Theory]
    [InlineData(BuildLink.Piston, "piston")]
    [InlineData(BuildLink.Spring, "spring")]
    public void ALink_AttachesItsJointsForTraining_UnlessTooShort(BuildLink kind, string name)
    {
        CreatureDef Joined(double length)
        {
            var builder = new CreatureBuilder();
            builder.AddNode(new Vector2D(0, 0));
            builder.AddNode(new Vector2D(length, 0));
            _ = kind == BuildLink.Piston ? builder.AddPiston(1, 2) : builder.AddSpring(1, 2);
            return builder.Build();
        }

        CreatureReadiness.Problems(Joined(100)).ShouldBeEmpty();
        CreatureReadiness.CanTrain(Joined(100)).ShouldBeTrue();
        NetworkPorts.Of(Joined(100)).Outputs.Count.ShouldBe(kind == BuildLink.Piston ? 2 : 0);
        CreatureReadiness.Problems(Joined(20)).ShouldContain(
            UiText.Format($"The {name} between joint {{0}} and joint {{1}} is too short. Move one of the joints apart.", 1, 2));
    }
}
