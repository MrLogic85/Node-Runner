using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.ViewModels;

/// <summary>
/// A locked Creation adds and deletes joints, beams and Springs, which have no brain ports, and
/// refuses every edit that would change its model (#896).
/// </summary>
public sealed class BuildLockedTests
{
    private static readonly UiText _lockedReason = UiText.Plain("The creation is locked to avoid changes to the model, unlock to enable.");

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
    [InlineData(CreatureElementKind.Beam, 11)]
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
        build.CanvasNotes().ShouldBeEmpty();
        build.Undo();
        Parts(build.Snapshot()).ShouldBe(Parts(before));
    }

    [Theory]
    [InlineData(CreatureElementKind.Sensor, 20)]
    [InlineData(CreatureElementKind.Piston, 40)]
    [InlineData(CreatureElementKind.Servo, 30)]
    [InlineData(CreatureElementKind.Beam, 14)] // Its sensor goes with it.
    [InlineData(CreatureElementKind.Node, 7)] // Its Piston goes with it.
    [InlineData(CreatureElementKind.Node, 2)] // Its Servo goes with it.
    [InlineData(CreatureElementKind.Beam, 10)] // The Servo's Fixed link.
    [InlineData(CreatureElementKind.Beam, 12)] // The Servo's Target link.
    [InlineData(CreatureElementKind.Node, 1)] // Its beam is the Servo's Fixed link.
    [InlineData(CreatureElementKind.VelocityMotor, 65)]
    [InlineData(CreatureElementKind.Node, 61)] // Its Velocity motor goes with it.
    [InlineData(CreatureElementKind.Beam, 63)] // The Velocity motor's Fixed link.
    [InlineData(CreatureElementKind.Beam, 64)] // The Velocity motor's Target link.
    [InlineData(CreatureElementKind.Node, 60)] // Its beam is the Velocity motor's Fixed link.
    public void Delete_ThatChangesTheModel_IsLocked_AndChangesNothing(CreatureElementKind kind, int id)
    {
        var build = Locked();
        var before = build.Snapshot();
        build.SelectOnly(kind, id);

        build.DeleteLockedReason.ShouldBe(_lockedReason);
        build.DeleteSelectedParts();

        Parts(build.Snapshot()).ShouldBe(Parts(before));
    }

    [Theory]
    [InlineData(CreatureElementKind.Sensor, 20, CreatureElementKind.Sensor, 20)]
    [InlineData(CreatureElementKind.Piston, 40, CreatureElementKind.Piston, 40)]
    [InlineData(CreatureElementKind.Servo, 30, CreatureElementKind.Servo, 30)]
    [InlineData(CreatureElementKind.Beam, 14, CreatureElementKind.Sensor, 20)] // Its sensor goes with it.
    [InlineData(CreatureElementKind.Node, 7, CreatureElementKind.Piston, 41)] // Its Piston goes with it.
    [InlineData(CreatureElementKind.Node, 2, CreatureElementKind.Servo, 30)] // Its Servo goes with it.
    [InlineData(CreatureElementKind.Beam, 12, CreatureElementKind.Servo, 30)] // The kept Servo's Target link.
    [InlineData(CreatureElementKind.Node, 1, CreatureElementKind.Servo, 30)] // Its beam is the kept Servo's Fixed link.
    [InlineData(CreatureElementKind.VelocityMotor, 65, CreatureElementKind.VelocityMotor, 65)]
    [InlineData(CreatureElementKind.Node, 61, CreatureElementKind.VelocityMotor, 65)] // Its Velocity motor goes with it.
    [InlineData(CreatureElementKind.Beam, 63, CreatureElementKind.VelocityMotor, 65)] // The kept Velocity motor's Fixed link.
    [InlineData(CreatureElementKind.Beam, 64, CreatureElementKind.VelocityMotor, 65)] // The kept Velocity motor's Target link.
    [InlineData(CreatureElementKind.Node, 62, CreatureElementKind.VelocityMotor, 65)] // Its beam is the kept Velocity motor's Target link.
    public void LockedDelete_PutsANoteOnThePartThatBlocksIt_AndChangesNothing(CreatureElementKind kind, int id, CreatureElementKind blockerKind, int blockerId)
    {
        var build = Locked();
        var before = build.Snapshot();
        build.SelectOnly(kind, id);

        build.DeleteSelectedParts();

        build.CanvasNotes().ShouldBe([WouldChangeModel(blockerKind, blockerId)]);
        Parts(build.Snapshot()).ShouldBe(Parts(before));
        build.CanUndo.ShouldBeFalse();
    }

    [Fact]
    public void LockedDelete_NotesEveryBlockerOnce()
    {
        var build = Locked();
        // Joint 8 takes beam 14 with its sensor and Piston 41; joint 3 takes Piston 40. The Servo
        // goes itself and loses its Fixed link too, and the sensor goes itself and with its beam.
        build.ReplaceSelection(PartSet.None with
        {
            Nodes = new HashSet<int> { 3, 8 },
            Beams = new HashSet<int> { 10, 14 },
            Sensors = new HashSet<int> { 20 },
            Servos = new HashSet<int> { 30 },
        });

        build.DeleteSelectedParts();

        build.CanvasNotes().ShouldBe(
        [
            WouldChangeModel(CreatureElementKind.Sensor, 20),
            WouldChangeModel(CreatureElementKind.Servo, 30),
            WouldChangeModel(CreatureElementKind.Piston, 40),
            WouldChangeModel(CreatureElementKind.Piston, 41),
        ]);
    }

    [Fact]
    public void LockedDeleteNotes_ClearOnSelectionChange_AndComeBackOnTheNextTap()
    {
        var build = Locked();
        build.SelectOnly(CreatureElementKind.Sensor, 20);
        build.DeleteSelectedParts();

        build.SelectOnly(CreatureElementKind.Piston, 40);
        build.CanvasNotes().ShouldBeEmpty();
        build.DeleteSelectedParts();

        build.CanvasNotes().ShouldBe([WouldChangeModel(CreatureElementKind.Piston, 40)]);
    }

    [Fact]
    public void LockedDeleteNotes_ClearOnTheNextCanvasTouch()
    {
        var build = Locked();
        build.SelectOnly(CreatureElementKind.Sensor, 20);
        build.DeleteSelectedParts();

        build.DismissTapNotes();

        build.CanvasNotes().ShouldBeEmpty();
    }

    [Fact]
    public void LockedDeleteNotes_ClearOnUnlock_AndDeleteThenDeletes()
    {
        var build = Locked();
        build.SelectOnly(CreatureElementKind.Sensor, 20);
        build.DeleteSelectedParts();

        build.Unlock();
        build.CanvasNotes().ShouldBeEmpty();
        build.DeleteSelectedParts();

        build.Sensors.ShouldBeEmpty();
        build.CanvasNotes().ShouldBeEmpty();
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
    public void ConnectLink_ABeamAndASpring_ReplaceEachOther_AndTheServoKeepsItsRole()
    {
        // No brain ports change: the Servo holding the beam keeps its id (#1176, #1177).
        var build = Locked();

        build.CanConnectLink(BuildLink.Spring, 2, 1, out _, out var replaced).ShouldBeTrue();
        replaced.ShouldBe(10);
        var spring = build.ConnectLink(BuildLink.Spring, 2, 1)!.Value;
        var beam = build.ConnectLink(BuildLink.Beam, 4, 1)!.Value;

        build.Beams.ShouldNotContain(link => link.Id == 10);
        build.Beams.ShouldContain(link => link.Id == beam);
        build.Springs.Select(link => link.Id).ShouldBe([50, spring], ignoreOrder: true);
        build.Servos.Single().ShouldBe(new ServoDef(30, 2, spring, 12));
    }

    [Fact]
    public void ConnectLink_ThatWouldRemoveAPiston_IsRefused()
    {
        var build = Locked();

        foreach (var link in new[] { BuildLink.Beam, BuildLink.Spring })
        {
            build.CanConnectLink(link, 3, 4, out var reason, out var replaced).ShouldBeFalse();
            reason.ShouldBe(BuildViewModel.LockedReason);
            replaced.ShouldBeNull();
            build.ConnectLink(link, 4, 3).ShouldBeNull();
        }

        build.Pistons.Count.ShouldBe(2);
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

    // A Servo at joint 2 holds beams 10 and 12; Piston 40 joins 3 and 4, and Piston 41 joins 7 and
    // 8; a sensor sits on beam 14, from 8 to 9. Beams 11 and 13 and Springs 50 and 51 have nothing
    // on them, so deleting joint 2 takes only the Servo and its own links, and joint 7 only Piston 41.
    // Apart from them, a Velocity motor at joint 61 turns beam 64 (to joint 62) against beam 63 (to 60).
    private static BuildViewModel Locked()
    {
        var build = new BuildViewModel();
        build.Load(
            new CreatureDef(
                [
                    new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(100, 0)), new NodeDef(3, new Vector2D(200, 0)),
                    new NodeDef(4, new Vector2D(100, 100)), new NodeDef(5, new Vector2D(300, 0)), new NodeDef(6, new Vector2D(300, 100)),
                    new NodeDef(7, new Vector2D(400, 0)), new NodeDef(8, new Vector2D(400, 100)), new NodeDef(9, new Vector2D(500, 100)),
                    new NodeDef(60, new Vector2D(600, 0)), new NodeDef(61, new Vector2D(700, 0)), new NodeDef(62, new Vector2D(800, 0)),
                ],
                [
                    new BeamDef(10, 1, 2), new BeamDef(11, 2, 3), new BeamDef(12, 2, 4), new BeamDef(13, 3, 5), new BeamDef(14, 8, 9),
                    new BeamDef(63, 60, 61), new BeamDef(64, 61, 62),
                ],
                [new SensorDef(20, 14, SensorKind.Accelerometer)],
                [new ServoDef(30, 2, 10, 12)],
                [new PistonDef(40, 3, 4), new PistonDef(41, 7, 8)],
                [new SpringDef(50, 5, 6), new SpringDef(51, 1, 4)],
                [],
                [],
                [],
                [new VelocityMotorDef(65, 61, fixedLinkId: 63, targetLinkId: 64)],
                [],
                nextPartId: 66),
            locked: true);
        return build;
    }

    private static CanvasNote WouldChangeModel(CreatureElementKind kind, int id) =>
        new(CanvasNoteKind.Danger, new CreatureElementSelection(kind, id), UiText.Plain("Locked: would change the model"));

    private static (int Nodes, int Beams, int Springs, int Pistons, int Sensors, int Servos, int VelocityMotors) Parts(CreatureDef creature) =>
        (creature.Nodes.Count, creature.Beams.Count, creature.Springs.Count, creature.Pistons.Count, creature.Sensors.Count, creature.Servos.Count, creature.VelocityMotors.Count);
}
