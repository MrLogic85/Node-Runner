using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.ViewModels;

/// <summary>
/// A locked Creation adds and deletes joints, beams and Springs, which have no brain ports, and
/// refuses every edit that would change its model (#896).
/// </summary>
public sealed class BuildLockedTests
{
    private static readonly UiText _lockedReason = UiText.Plain("Locked: the model is trained for these parts.");

    [Fact]
    public void AJointABeamAndASpring_AreAdded_AndUndoneAndRedoneOneByOne()
    {
        var build = Locked();
        var before = build.Snapshot();

        var joint = build.PlaceNode(new Vector2D(150, 200));
        build.ConnectLink(BuildLink.Beam, joint, 4).ShouldNotBeNull();
        build.ConnectLink(BuildLink.Spring, joint, 6).ShouldNotBeNull();
        var after = build.Snapshot();

        after.Nodes.Count.ShouldBe(before.Nodes.Count + 1);
        after.Beams.Count.ShouldBe(before.Beams.Count + 1);
        after.Springs.Count.ShouldBe(before.Springs.Count + 1);
        build.Undo();
        build.Undo();
        build.Undo();
        Parts(build.Snapshot()).ShouldBe(Parts(before));
        build.Redo();
        build.Redo();
        build.Redo();
        Parts(build.Snapshot()).ShouldBe(Parts(after));
        build.IsLocked.ShouldBeTrue();
    }

    [Theory]
    [InlineData(CreatureElementKind.Beam, 13)]
    [InlineData(CreatureElementKind.Spring, 50)]
    [InlineData(CreatureElementKind.Spring, 51)]
    [InlineData(CreatureElementKind.Node, 5)]
    [InlineData(CreatureElementKind.Node, 6)]
    public void Delete_APartWithoutPorts_DeletesIt_AndIsUndoable(CreatureElementKind kind, int id)
    {
        var build = Locked();
        var before = build.Snapshot();
        build.SelectOnly(kind, id);

        build.DeleteLockedReason.ShouldBeNull();
        build.DeleteSelectedParts();

        Parts(build.Snapshot()).ShouldNotBe(Parts(before));
        build.Undo();
        Parts(build.Snapshot()).ShouldBe(Parts(before));
    }

    [Theory]
    [InlineData(CreatureElementKind.Sensor, 20)]
    [InlineData(CreatureElementKind.Piston, 40)]
    [InlineData(CreatureElementKind.Servo, 30)]
    [InlineData(CreatureElementKind.Beam, 11)] // Its sensor goes with it.
    [InlineData(CreatureElementKind.Node, 7)] // Its Piston goes with it.
    [InlineData(CreatureElementKind.Node, 2)] // Its Servo goes with it.
    [InlineData(CreatureElementKind.Beam, 10)] // The Servo's Fixed link.
    [InlineData(CreatureElementKind.Beam, 12)] // The Servo's Target link.
    [InlineData(CreatureElementKind.Node, 1)] // Its beam is the Servo's Fixed link.
    public void Delete_ThatChangesTheModel_IsLocked_AndChangesNothing(CreatureElementKind kind, int id)
    {
        var build = Locked();
        var before = build.Snapshot();
        build.SelectOnly(kind, id);

        build.DeleteLockedReason.ShouldBe(_lockedReason);
        build.DeleteSelectedParts();

        Parts(build.Snapshot()).ShouldBe(Parts(before));
    }

    [Fact]
    public void Delete_OfASelectionWithOnePortedPart_IsLocked()
    {
        var build = Locked();
        build.ReplaceSelection(PartSet.None with { Beams = new HashSet<int> { 13 }, Sensors = new HashSet<int> { 20 } });

        build.DeleteLockedReason.ShouldBe(_lockedReason);
    }

    [Fact]
    public void Delete_OnAnUnlockedCreation_IsNeverLocked()
    {
        var build = Locked();
        build.Unlock();
        build.SelectOnly(CreatureElementKind.Sensor, 20);

        build.DeleteLockedReason.ShouldBeNull();
        build.DeleteSelectedParts();

        build.Sensors.ShouldBeEmpty();
    }

    [Fact]
    public void ConnectLink_Piston_IsRefused_AndSaysWhyAtTheJoint()
    {
        var build = Locked();

        build.CanConnectLink(BuildLink.Piston, 1, 3, out var reason).ShouldBeFalse();
        reason.ShouldBe(_lockedReason);
        build.ConnectLink(BuildLink.Piston, 1, 3).ShouldBeNull();

        build.Pistons.Count.ShouldBe(2);
        build.PlacementNote.ShouldBe(new CanvasNote(CanvasNoteKind.Danger, new CreatureElementSelection(CreatureElementKind.Node, 3), _lockedReason));
    }

    [Fact]
    public void ConnectLink_ASpringThatWouldReplaceABeam_IsRefused_NamingTheBeam()
    {
        var build = Locked();

        build.CanConnectLink(BuildLink.Spring, 3, 5, out var reason, out var replaced).ShouldBeFalse();
        reason.ShouldBe(UiText.Plain("A beam already joins these joints"));
        replaced.ShouldBeNull();
        build.ConnectLink(BuildLink.Spring, 3, 5).ShouldBeNull();

        build.Beams.ShouldContain(beam => beam.Id == 13);
        build.Springs.Count.ShouldBe(2);
    }

    [Fact]
    public void PickLink_Piston_IsRefused()
    {
        var build = Locked();
        build.ActiveTool = BuildTool.Beam;
        build.PickLink(BuildLink.Spring);

        build.PickLink(BuildLink.Piston);

        build.PickedLink.ShouldBe(BuildLink.Spring);
    }

    [Theory]
    [InlineData(BuildPart.Accelerometer, CreatureElementKind.Beam, 13)]
    [InlineData(BuildPart.Servo, CreatureElementKind.Node, 5)]
    public void CanPlacePart_APartWithPorts_IsRefused(BuildPart part, CreatureElementKind kind, int id)
    {
        var build = Locked();

        build.CanPlacePart(part, new CreatureElementSelection(kind, id), out var reason).ShouldBeFalse();

        reason.ShouldBe(_lockedReason);
    }

    // A Servo at joint 2 holds beams 10 and 12; a sensor sits on beam 11; Piston 40 joins 3 and 4,
    // and Piston 41 joins the otherwise free joints 7 and 8; beam 13 and Springs 50 and 51 have
    // nothing on them.
    private static BuildViewModel Locked()
    {
        var build = new BuildViewModel();
        build.Load(
            new CreatureDef(
                [
                    new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(100, 0)), new NodeDef(3, new Vector2D(200, 0)),
                    new NodeDef(4, new Vector2D(100, 100)), new NodeDef(5, new Vector2D(300, 0)), new NodeDef(6, new Vector2D(300, 100)),
                    new NodeDef(7, new Vector2D(400, 0)), new NodeDef(8, new Vector2D(400, 100)),
                ],
                [new BeamDef(10, 1, 2), new BeamDef(11, 2, 3), new BeamDef(12, 2, 4), new BeamDef(13, 3, 5)],
                [new SensorDef(20, 11, SensorKind.Accelerometer)],
                [new ServoDef(30, 2, 10, 12)],
                [new PistonDef(40, 3, 4), new PistonDef(41, 7, 8)],
                [new SpringDef(50, 5, 6), new SpringDef(51, 1, 4)],
                nextPartId: 52),
            locked: true);
        return build;
    }

    private static (int Nodes, int Beams, int Springs, int Pistons, int Sensors, int Servos) Parts(CreatureDef creature) =>
        (creature.Nodes.Count, creature.Beams.Count, creature.Springs.Count, creature.Pistons.Count, creature.Sensors.Count, creature.Servos.Count);
}
