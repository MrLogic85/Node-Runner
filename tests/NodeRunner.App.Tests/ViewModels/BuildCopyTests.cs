using NodeRunner.App.ViewModels;
using NodeRunner.Domain;
using static NodeRunner.App.Tests.ViewModels.BuildFixtures;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class BuildCopyTests
{
    private const double _step = BuildViewModel.BuildAreaCell;
    private static readonly CreatureElementSelection _camera = new(CreatureElementKind.Sensor, 21);
    private static readonly CreatureElementSelection _servo = new(CreatureElementKind.Servo, 31);
    private static readonly CreatureElementSelection _spring = new(CreatureElementKind.Spring, 13);
    private static readonly CreatureElementSelection _piston = new(CreatureElementKind.Piston, 14);

    [Fact]
    public void Copy_OfEveryPart_GoesAStepAside_WithTheirSettings_OnTheCopiedLinks_SelectedAndUndoable()
    {
        var build = Loaded(Creature());
        var before = build.Snapshot();
        var selection = Set(Joint(1), Joint(2), Joint(3), Joint(4), Joint(5), Beam(11), Beam(12), _camera, _servo, _piston, _spring);
        build.ReplaceSelection(selection);
        build.CanCopySelection.ShouldBeTrue();

        build.CopySelectedParts();

        build.Nodes.Skip(5).Select(node => (node.Position, node.Name)).ShouldBe(
        [
            (new Vector2D(_step, _step), null),
            (new Vector2D(100 + _step, _step), null),
            (new Vector2D(100 + _step, 100 + _step), null),
            (new Vector2D(_step, 100 + _step), null),
            (new Vector2D(-100 + _step, 100 + _step), null),
        ]);
        var nodes = build.Nodes.Skip(5).Select(node => node.Id).ToArray();
        var beams = build.Beams.Skip(2).ToArray();
        beams.Select(beam => (beam.NodeA, beam.NodeB)).ShouldBe([(nodes[0], nodes[1]), (nodes[1], nodes[2])]);
        var spring = build.Springs.Last();
        (spring.NodeA, spring.NodeB, spring.Stiffness, spring.Damping, spring.Stroke, spring.CoilLength).ShouldBe((nodes[2], nodes[3], 900, 20, 0.5, 0.4));
        var piston = build.Pistons.Last();
        (piston.NodeA, piston.NodeB, piston.Name, piston.Strength, piston.Stroke, piston.Start, piston.MaxSpeed, piston.RiseTime)
            .ShouldBe((nodes[3], nodes[4], null, 20000, 0.8, 0.25, 300, 0.5));
        var sensor = build.Sensors.Last();
        (sensor.BeamId, sensor.Kind, sensor.Name, sensor.Aim).ShouldBe((beams[0].Id, SensorKind.Camera, null, 1.0));
        var servo = build.Servos.Last();
        (servo.NodeId, servo.FixedLinkId, servo.TargetLinkId, servo.Name, servo.Strength, servo.Range)
            .ShouldBe((nodes[1], beams[1].Id, beams[0].Id, null, 800000, Math.PI / 2));
        build.Selection.Parts.ShouldBe(
            [.. nodes.Select(Joint), .. beams.Select(beam => Beam(beam.Id)), new(CreatureElementKind.Sensor, sensor.Id), new(CreatureElementKind.Servo, servo.Id),
             new(CreatureElementKind.Piston, piston.Id), new(CreatureElementKind.Spring, spring.Id)],
            ignoreOrder: true);
        build.CanvasNotes().ShouldBeEmpty();
        var copied = build.Snapshot();

        build.Undo();
        ShouldHaveTheSameParts(build.Snapshot(), before);
        build.Selection.ShouldBeEquivalentTo(selection);
        build.Redo();
        ShouldHaveTheSameParts(build.Snapshot(), copied);
    }

    [Fact]
    public void AServo_BringsItsJoint_AndItsCopyTakesWhatOfItsLinksCame()
    {
        var build = Loaded(Creature());
        build.ReplaceSelection(Set(Joint(3), Beam(12), _servo));
        build.CopySelectedParts();
        var (joint, other) = (build.Nodes[^2], build.Nodes[^1]);
        (joint.Position, other.Position).ShouldBe((new Vector2D(100 + _step, _step), new Vector2D(100 + _step, 100 + _step)));
        var beam = build.Beams.Last();
        (beam.NodeA, beam.NodeB).ShouldBe((joint.Id, other.Id));
        var servo = build.Servos.Last();
        (servo.NodeId, servo.FixedLinkId, servo.TargetLinkId).ShouldBe((joint.Id, beam.Id, null));
        build.Selection.Parts.ShouldBe([Joint(other.Id), Beam(beam.Id), new(CreatureElementKind.Servo, servo.Id)], ignoreOrder: true);
        build.SelectedNodeIds.ShouldBe([joint.Id, other.Id], ignoreOrder: true);
        build.ShowTrainingBlockers();
        build.CanvasNotes().ShouldContain(note => note.Target == new CreatureElementSelection(CreatureElementKind.Servo, servo.Id));

        build = Loaded(Creature());
        build.ReplaceSelection(Set(Joint(1), Joint(2)));
        build.CopySelectedParts();
        build.Nodes.Count.ShouldBe(7);
        build.Servos.ShouldHaveSingleItem().NodeId.ShouldBe(2);

        build = Loaded(new CreatureDef(
            [
                new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(100, 0)), new NodeDef(3, new Vector2D(100, 100)),
                new ServoDef(31, 2, 13, 14), new PistonDef(14, 2, 3), new SpringDef(13, 1, 2),
            ],
            nextPartId: 40));
        build.ReplaceSelection(Set(Joint(1), Joint(2), Joint(3), _servo, _piston, _spring));
        build.CopySelectedParts();
        var copy = build.Servos.Last();
        (copy.FixedLinkId, copy.TargetLinkId).ShouldBe((build.Springs.Last().Id, build.Pistons.Last().Id));
        build.CanvasNotes().ShouldBeEmpty();
    }

    [Fact]
    public void Copy_StaysInTheBuildArea_GoingTheOtherWayAtItsCorner_AndKeepingAFullHeightShape()
    {
        var corner = BuildViewModel.BuildArea.Max;
        var build = Loaded(new CreatureDef(
            [new NodeDef(1, new Vector2D(corner.X - 20, corner.Y - 20)), new NodeDef(2, new Vector2D(corner.X - 120, corner.Y - 20)), new BeamDef(3, 1, 2)]));
        build.ReplaceSelection(Set(Joint(1), Joint(2), Beam(3)));
        build.CopySelectedParts();
        build.Nodes.Skip(2).Select(node => node.Position).ShouldBe(
            [new Vector2D(corner.X - 20 - _step, corner.Y - 20 - _step), new Vector2D(corner.X - 120 - _step, corner.Y - 20 - _step)]);

        var area = BuildViewModel.BuildArea;
        var r = NodeDef.PlainJointRadius;
        Vector2D[] tall = [new(0, area.Min.Y + r), new(0, area.Max.Y - r), new(0, area.Max.Y - r - (_step / 2))];
        build = Loaded(new CreatureDef(
            [new NodeDef(1, tall[0]), new NodeDef(2, tall[1]), new NodeDef(3, tall[2]), new BeamDef(4, 1, 2), new BeamDef(5, 2, 3)]));
        build.ReplaceSelection(Set(Joint(1), Joint(2), Joint(3), Beam(4), Beam(5)));
        build.CopySelectedParts();
        build.Nodes.Skip(3).Select(node => node.Position).ShouldBe(tall.Select(position => new Vector2D(position.X + _step, position.Y)));
    }

    public static TheoryData<int[], int[], CreatureElementSelection, string, bool> Blocked => new()
    {
        { [1], [11], Beam(11), "Select both its joints", false },
        { [4], [], _piston, "Select both its joints", false },
        { [1, 2], [], _camera, "Select its beam", false },
        { [1, 2], [11], _camera, LockedText, true },
        { [4, 5], [], _piston, LockedText, true },
    };

    [Theory]
    [MemberData(nameof(Blocked))]
    public void Copy_OfASelectionItCannotTake_IsDimmed_AndATapMarksTheOffendingPart(int[] joints, int[] beams, CreatureElementSelection offender, string reason, bool locked)
    {
        var build = Loaded(Creature(), locked);
        build.ReplaceSelection(Set([.. joints.Select(Joint), .. beams.Select(Beam), offender]));
        var before = build.Snapshot();

        build.CanOfferCopy.ShouldBeTrue();
        build.CanCopySelection.ShouldBeFalse();
        build.CanvasNotes().ShouldBeEmpty();
        build.CopySelectedParts();

        ShouldHaveTheSameParts(build.Snapshot(), before);
        build.CanUndo.ShouldBeFalse();
        build.CanvasNotes().ShouldBe([new CanvasNote(CanvasNoteKind.Danger, offender, UiText.Plain(reason))]);
    }

    [Fact]
    public void ADimmedCopysNotes_MarkEachOffenderKindByKind_PortsFirst_AndGoOnASelectionTouchOrUnlock()
    {
        var build = Loaded(Creature());
        build.ReplaceSelection(Set(Joint(5), Beam(12), Beam(11), _spring));
        build.CopySelectedParts();
        build.CanvasNotes().Select(note => (note.Target, note.Text)).ShouldBe(
            [(Beam(11), UiText.Plain("Select both its joints")), (Beam(12), UiText.Plain("Select both its joints")), (_spring, UiText.Plain("Select both its joints"))]);
        build.ReplaceSelection(Set(Joint(1), Beam(11)));
        build.CopySelectedParts();
        build.CanvasNotes().ShouldNotBeEmpty();
        build.ToggleSelected(Joint(2));
        build.CanvasNotes().ShouldBeEmpty();
        build.CanCopySelection.ShouldBeTrue();
        build.ToggleSelected(Joint(2));
        build.CopySelectedParts();
        new BuildGestures(build).Press(new Vector2D(2000, 2000));
        build.CanvasNotes().ShouldBeEmpty();
        build.ReplaceSelection(Set(Joint(1)));
        build.CanOfferCopy.ShouldBeFalse();

        build = Loaded(Creature(), locked: true);
        // The Servo still brings its joint 2, so beam 12 has both of its joints.
        build.ReplaceSelection(Set(Joint(3), Beam(12), Beam(11), _servo));
        build.CopySelectedParts();
        build.CanvasNotes().Select(note => (note.Target, note.Text)).ShouldBe([(_servo, UiText.Plain(LockedText)), (Beam(11), UiText.Plain("Select both its joints"))]);
        var changed = new List<string?>();
        build.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        build.ReplaceSelection(Set(Joint(1), Joint(2), Beam(11), _camera));
        build.CopySelectedParts();
        build.Unlock();
        build.CanvasNotes().ShouldBeEmpty();
        changed.ShouldContain(nameof(BuildViewModel.CanvasNotes));
        build.CanCopySelection.ShouldBeTrue();
    }

    [Fact]
    public void OnALockedCreation_CopyIsOffered_AndCopiesJointsBeamsAndSprings()
    {
        var build = Loaded(Creature(), locked: true);
        build.ReplaceSelection(Set(Joint(1), Joint(2), Joint(3), Joint(4), Beam(11), Beam(12), _spring));

        build.CanOfferCopy.ShouldBeTrue();
        build.CanCopySelection.ShouldBeTrue();
        build.CopySelectedParts();

        (build.Nodes.Count, build.Beams.Count, build.Springs.Count).ShouldBe((9, 4, 2));
        (build.Sensors.Count, build.Servos.Count, build.Pistons.Count).ShouldBe((1, 1, 1));
    }

    private static void ShouldHaveTheSameParts(CreatureDef actual, CreatureDef expected) => actual.Parts.ShouldBe(expected.Parts);

    // A square of joints 1–4 with beams 11 (Camera 21, aimed) and 12, Servo 31 on joint 2, a tuned Spring 13
    // and a tuned Piston 14 out to joint 5.
    private static CreatureDef Creature() => new(
        [
            new NodeDef(1, new Vector2D(0, 0), "Hip"), new NodeDef(2, new Vector2D(100, 0)), new NodeDef(3, new Vector2D(100, 100)),
            new NodeDef(4, new Vector2D(0, 100)), new NodeDef(5, new Vector2D(-100, 100)),
            new BeamDef(11, 1, 2), new BeamDef(12, 2, 3),
            new SensorDef(21, 11, SensorKind.Camera, aim: 1.0),
            new ServoDef(31, 2, 12, 11, strength: 800000, range: Math.PI / 2),
            new PistonDef(14, 4, 5, "Kick", strength: 20000, stroke: 0.8, start: 0.25, maxSpeed: 300, riseTime: 0.5),
            new SpringDef(13, 3, 4, stiffness: 900, damping: 20, stroke: 0.5, coilLength: 0.4),
        ],
        nextPartId: 40);
}
