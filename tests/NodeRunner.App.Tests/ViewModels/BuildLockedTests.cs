using NodeRunner.App.Builders;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;
using static NodeRunner.App.Tests.ViewModels.BuildFixtures;

namespace NodeRunner.App.Tests.ViewModels;

/// <summary>
/// A locked Creation adds and deletes joints, beams and Springs, which have no network ports, and
/// refuses every edit that would change its network (#896).
/// </summary>
public sealed class BuildLockedTests
{
    [Fact]
    public void JointsBeamsAndSprings_AreAddedAndDeleted_EachAnUndoStep()
    {
        var build = Locked();
        var before = Parts(build.Snapshot());

        var joint = build.PlaceNode(new Vector2D(150, 200));
        build.ConnectLink(BuildLink.Beam, joint, 4).ShouldNotBeNull();
        build.ConnectLink(BuildLink.Spring, joint, 6).ShouldNotBeNull();
        var after = Parts(build.Snapshot());
        after.ShouldBe(before with { Nodes = before.Nodes + 1, Beams = before.Beams + 1, Springs = before.Springs + 1 });
        build.Undo();
        build.Undo();
        build.Undo();
        Parts(build.Snapshot()).ShouldBe(before);
        build.Redo();
        build.Redo();
        build.Redo();
        Parts(build.Snapshot()).ShouldBe(after);
        build.IsLocked.ShouldBeTrue();

        foreach (var (kind, id) in new[] { (CreatureElementKind.Beam, 11), (CreatureElementKind.Spring, 50), (CreatureElementKind.Node, 5) })
        {
            build.SelectOnly(kind, id);
            build.DeleteLockedReason.ShouldBeNull();
            build.DeleteSelectedParts();
            Parts(build.Snapshot()).ShouldNotBe(after);
            build.CanvasNotes().ShouldBeEmpty();
            build.Undo();
            Parts(build.Snapshot()).ShouldBe(after);
        }
    }

    [Theory]
    [InlineData(CreatureElementKind.Sensor, 20, CreatureElementKind.Sensor, 20)]
    [InlineData(CreatureElementKind.Piston, 40, CreatureElementKind.Piston, 40)]
    [InlineData(CreatureElementKind.Beam, 14, CreatureElementKind.Sensor, 20)] // Its sensor goes with it.
    [InlineData(CreatureElementKind.Node, 7, CreatureElementKind.Piston, 41)] // Its Piston goes with it.
    [InlineData(CreatureElementKind.Node, 2, CreatureElementKind.Servo, 30)] // Its Servo goes with it.
    [InlineData(CreatureElementKind.Beam, 12, CreatureElementKind.Servo, 30)] // The kept Servo's Target link.
    [InlineData(CreatureElementKind.Node, 1, CreatureElementKind.Servo, 30)] // Its beam is the kept Servo's Fixed link.
    [InlineData(CreatureElementKind.Node, 62, CreatureElementKind.VelocityMotor, 65)] // Its beam is the kept Velocity motor's Target link.
    public void ADeleteThatChangesTheNetwork_IsLocked_AndNotesThePartThatBlocksIt(CreatureElementKind kind, int id, CreatureElementKind blockerKind, int blockerId)
    {
        var build = Locked();
        var before = Parts(build.Snapshot());
        build.SelectOnly(kind, id);

        build.DeleteLockedReason.ShouldBe(BuildViewModel.LockedReason);
        build.DeleteSelectedParts();

        build.CanvasNotes().ShouldBe([WouldChangeModel(blockerKind, blockerId)]);
        Parts(build.Snapshot()).ShouldBe(before);
        build.CanUndo.ShouldBeFalse();
    }

    [Fact]
    public void LockedDeleteNotes_MarkEveryBlockerOnce_AndClearOnSelectionTouchOrUnlock()
    {
        var build = Locked();
        // Joint 8 takes beam 14 with its sensor and Piston 41; joint 3 takes Piston 40. The Servo
        // goes itself and loses its Fixed link too, and the sensor goes itself and with its beam.
        build.ReplaceSelection(Set(Joint(3), Joint(8), Beam(10), Beam(14), new(CreatureElementKind.Sensor, 20), new(CreatureElementKind.Servo, 30)));
        build.DeleteSelectedParts();
        build.CanvasNotes().ShouldBe(
        [
            WouldChangeModel(CreatureElementKind.Sensor, 20),
            WouldChangeModel(CreatureElementKind.Servo, 30),
            WouldChangeModel(CreatureElementKind.Piston, 40),
            WouldChangeModel(CreatureElementKind.Piston, 41),
        ]);

        build.SelectOnly(CreatureElementKind.Piston, 40);
        build.CanvasNotes().ShouldBeEmpty();
        build.DeleteSelectedParts();
        build.CanvasNotes().ShouldBe([WouldChangeModel(CreatureElementKind.Piston, 40)]);
        build.DismissTapNotes();
        build.CanvasNotes().ShouldBeEmpty();

        build.ReplaceSelection(Set(Beam(13), new(CreatureElementKind.Sensor, 20)));
        build.DeleteLockedReason.ShouldBe(BuildViewModel.LockedReason);
        build.DeleteSelectedParts();
        build.Unlock();
        build.CanvasNotes().ShouldBeEmpty();
        build.DeleteLockedReason.ShouldBeNull();
        build.DeleteSelectedParts();
        build.Sensors.ShouldBeEmpty();
        build.CanvasNotes().ShouldBeEmpty();
    }

    [Fact]
    public void Links_APistonIsRefused_ABeamAndASpringReplaceEachOther_ButNotAPiston()
    {
        var build = Locked();

        build.CanConnectLink(BuildLink.Piston, 1, 3, out var reason).ShouldBeFalse();
        reason.ShouldBe(BuildViewModel.LockedReason);
        build.ConnectLink(BuildLink.Piston, 1, 3).ShouldBeNull();
        build.PlacementNote.ShouldBe(new CanvasNote(CanvasNoteKind.Danger, Joint(3), BuildViewModel.LockedReason));
        build.CanPlacePart(BuildPart.Accelerometer, Beam(13), out reason).ShouldBeFalse();
        reason.ShouldBe(BuildViewModel.LockedReason);

        foreach (var link in new[] { BuildLink.Beam, BuildLink.Spring })
        {
            build.CanConnectLink(link, 3, 4, out reason, out var replacedPiston).ShouldBeFalse();
            reason.ShouldBe(BuildViewModel.LockedReason);
            replacedPiston.ShouldBeNull();
            build.ConnectLink(link, 4, 3).ShouldBeNull();
        }

        build.Pistons.Count.ShouldBe(2);

        // No network ports change: the Servo holding the beam keeps its id (#1176, #1177).
        build.CanConnectLink(BuildLink.Spring, 2, 1, out _, out var replaced).ShouldBeTrue();
        replaced.ShouldBe(10);
        var spring = build.ConnectLink(BuildLink.Spring, 2, 1)!.Value;
        var beam = build.ConnectLink(BuildLink.Beam, 4, 1)!.Value;
        build.Beams.Select(link => link.Id).ShouldNotContain(10);
        build.Beams.Select(link => link.Id).ShouldContain(beam);
        build.Springs.Select(link => link.Id).ShouldBe([50, spring], ignoreOrder: true);
        build.Servos.Single().ShouldBe(new ServoDef(30, 2, spring, 12));
    }

    // A Servo's ports do not depend on its sides, which keep its id (#1177, #1179).
    [Fact]
    public void AServosSides_ArePickedAsOnAnUnlockedCreation()
    {
        var build = Locked();
        build.SetHingePartLink(30, fixedRole: true, linkId: 11);
        build.Servos.Single().ShouldBe(new ServoDef(30, 2, 11, 12));
        build.Undo();
        build.Servos.Single().ShouldBe(new ServoDef(30, 2, 10, 12));

        build = Locked(new ServoDef(30, 2, fixedLinkId: 10));
        build.SetHingePartLink(30, fixedRole: false, linkId: 10);
        build.Servos.Single().ShouldBe(new ServoDef(30, 2, targetLinkId: 10));

        // Joint 9 has only beam 14, so its Servo's empty Target picker notes it needs two links.
        foreach (var servo in new[] { new ServoDef(30, 2, 10, 12), new ServoDef(30, 2, 10), new ServoDef(30, 9, 14) })
        {
            var locked = Locked(servo);
            var unlocked = Locked(servo);
            unlocked.Unlock();
            locked.SelectOnly(CreatureElementKind.Servo, 30);
            unlocked.SelectOnly(CreatureElementKind.Servo, 30);
            var pickers = new BuildPresentationViewModel(locked).SinglePart!.Pickers!;
            pickers.ShouldBeEquivalentTo(new BuildPresentationViewModel(unlocked).SinglePart!.Pickers);
            pickers[1].Note.ShouldBe(servo.NodeId == 9 ? CreatureBuilder.ServoNeedsTwoSidesReason : null);
        }
    }

    // A Servo at joint 2 holds beams 10 and 12; Piston 40 joins 3 and 4, and Piston 41 joins 7 and
    // 8; a sensor sits on beam 14, from 8 to 9. Beams 11 and 13 and Springs 50 and 51 have nothing
    // on them, so deleting joint 2 takes only the Servo and its own links, and joint 7 only Piston 41.
    // Apart from them, a Velocity motor at joint 61 turns beam 64 (to joint 62) against beam 63 (to 60).
    private static BuildViewModel Locked(ServoDef? servo = null) => Loaded(
        new CreatureDef(
            [
                new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(100, 0)), new NodeDef(3, new Vector2D(200, 0)),
                new NodeDef(4, new Vector2D(100, 100)), new NodeDef(5, new Vector2D(300, 0)), new NodeDef(6, new Vector2D(300, 100)),
                new NodeDef(7, new Vector2D(400, 0)), new NodeDef(8, new Vector2D(400, 100)), new NodeDef(9, new Vector2D(500, 100)),
                new NodeDef(60, new Vector2D(600, 0)), new NodeDef(61, new Vector2D(700, 0)), new NodeDef(62, new Vector2D(800, 0)),
                new BeamDef(10, 1, 2), new BeamDef(11, 2, 3), new BeamDef(12, 2, 4), new BeamDef(13, 3, 5), new BeamDef(14, 8, 9),
                new BeamDef(63, 60, 61), new BeamDef(64, 61, 62),
                new SensorDef(20, 14, SensorKind.Accelerometer),
                servo ?? new ServoDef(30, 2, 10, 12),
                new PistonDef(40, 3, 4), new PistonDef(41, 7, 8),
                new SpringDef(50, 5, 6), new SpringDef(51, 1, 4),
                new VelocityMotorDef(65, 61, fixedLinkId: 63, targetLinkId: 64),
            ],
            nextPartId: 66),
        locked: true);

    private static CanvasNote WouldChangeModel(CreatureElementKind kind, int id) =>
        new(CanvasNoteKind.Danger, new CreatureElementSelection(kind, id), UiText.Plain(LockedText));

    private static PartCounts Parts(CreatureDef creature) =>
        new(creature.Nodes.Count, creature.Beams.Count, creature.Springs.Count, creature.Pistons.Count, creature.Sensors.Count, creature.Servos.Count, creature.VelocityMotors.Count);

    private sealed record PartCounts(int Nodes, int Beams, int Springs, int Pistons, int Sensors, int Servos, int VelocityMotors);
}
