using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class BuildCopyTests
{
    private const double _step = BuildViewModel.BuildGridStep;

    [Fact]
    public void Copy_DuplicatesJointsBeamsAndSpringsAStepAside_KeepsTheirSettings_AndSelectsTheCopy()
    {
        var build = Loaded();
        build.ReplaceSelection(new PartSet(Ids(1, 2, 3, 4), Ids(11, 12), Ids(), Ids(), Ids(), Ids(13)));
        build.CanCopySelection.ShouldBeTrue();

        build.CopySelectedParts();

        build.Nodes.Skip(5).Select(node => (node.Position, node.Name)).ShouldBe(
        [
            (new Vector2D(_step, _step), null),
            (new Vector2D(100 + _step, _step), null),
            (new Vector2D(100 + _step, 100 + _step), null),
            (new Vector2D(_step, 100 + _step), null),
        ]);
        var copies = build.Nodes.Skip(5).Select(node => node.Id).ToArray();
        build.Beams.Skip(2).Select(beam => (beam.NodeA, beam.NodeB)).ShouldBe([(copies[0], copies[1]), (copies[1], copies[2])]);
        var spring = build.Springs.Where(spring => spring.Id != 13).ShouldHaveSingleItem();
        (spring.NodeA, spring.NodeB, spring.Stiffness, spring.Damping, spring.Stroke, spring.CoilLength).ShouldBe((copies[2], copies[3], 900, 20, 0.5, 0.4));
        build.Sensors.Count.ShouldBe(1);
        build.Servos.Count.ShouldBe(1);
        build.Selection.Nodes.ShouldBe(copies, ignoreOrder: true);
        build.Selection.Beams.ShouldBe(build.Beams.Skip(2).Select(beam => beam.Id), ignoreOrder: true);
        build.Selection.Springs.ShouldBe([spring.Id]);
    }

    [Fact]
    public void Copy_OfEveryPart_TakesThePistonSensorAndServoToo_WithTheirSettings_AndTheServoOnTheCopiedLinks()
    {
        var build = Loaded();
        build.ReplaceSelection(new PartSet(Ids(1, 2, 3, 4, 5), Ids(11, 12), Ids(21), Ids(31), Ids(14), Ids(13)));
        build.CanCopySelection.ShouldBeTrue();

        build.CopySelectedParts();

        var nodes = build.Nodes.Skip(5).Select(node => node.Id).ToArray();
        var beams = build.Beams.Skip(2).Select(beam => beam.Id).ToArray();
        var piston = build.Pistons.Where(piston => piston.Id != 14).ShouldHaveSingleItem();
        (piston.NodeA, piston.NodeB, piston.Name, piston.Strength, piston.Stroke, piston.Start, piston.MaxSpeed, piston.RiseTime)
            .ShouldBe((nodes[3], nodes[4], null, 20000, 0.8, 0.25, 300, 0.5));
        var sensor = build.Sensors.Where(sensor => sensor.Id != 21).ShouldHaveSingleItem();
        (sensor.BeamId, sensor.Kind, sensor.Name, sensor.Aim).ShouldBe((beams[0], SensorKind.Camera, null, 1.0));
        var servo = build.Servos.Where(servo => servo.Id != 31).ShouldHaveSingleItem();
        (servo.NodeId, servo.FixedLinkId, servo.TargetLinkId, servo.Name, servo.Strength, servo.Range)
            .ShouldBe((nodes[1], beams[1], beams[0], null, 800000, Math.PI / 2));
        build.Selection.Nodes.ShouldBe(nodes, ignoreOrder: true);
        build.Selection.Beams.ShouldBe(beams, ignoreOrder: true);
        (build.Selection.Sensors.Single(), build.Selection.Servos.Single(), build.Selection.Pistons.Single()).ShouldBe((sensor.Id, servo.Id, piston.Id));
        build.CanvasNotes().ShouldBeEmpty();
    }

    [Fact]
    public void Copy_OfAServoBetweenASpringAndAPiston_UsesTheirCopies()
    {
        var build = new BuildViewModel();
        build.Load(new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(100, 0)), new NodeDef(3, new Vector2D(100, 100))],
            [],
            [],
            [new ServoDef(31, 2, 13, 14)],
            [new PistonDef(14, 2, 3)],
            [new SpringDef(13, 1, 2)],
            nextPartId: 40));
        build.ReplaceSelection(new PartSet(Ids(1, 2, 3), Ids(), Ids(), Ids(31), Ids(14), Ids(13)));

        build.CopySelectedParts();

        var copy = build.Servos.Where(servo => servo.Id != 31).ShouldHaveSingleItem();
        (copy.FixedLinkId, copy.TargetLinkId).ShouldBe((build.Springs.Last().Id, build.Pistons.Last().Id));
        build.CanvasNotes().ShouldBeEmpty();
    }

    [Fact]
    public void Copy_OfAServoWithOneOfItsLinks_LeavesTheOtherRoleEmpty()
    {
        var build = Loaded();
        build.ReplaceSelection(new PartSet(Ids(1, 2), Ids(11), Ids(), Ids(31), Ids(), Ids()));

        build.CopySelectedParts();

        var copy = build.Servos.Where(servo => servo.Id != 31).ShouldHaveSingleItem();
        (copy.FixedLinkId, copy.TargetLinkId).ShouldBe((null, build.Beams.Last().Id));
        build.CanvasNotes().ShouldContain(note => note.Target == new CreatureElementSelection(CreatureElementKind.Servo, copy.Id));
    }

    [Fact]
    public void Copy_OfAJointUnderAServo_CopiesOnlyThePlainJoint()
    {
        var build = Loaded();
        build.ReplaceSelection(Ids(1, 2));

        build.CopySelectedParts();

        build.Nodes.Count.ShouldBe(7);
        build.Servos.ShouldHaveSingleItem().NodeId.ShouldBe(2);
    }

    [Fact]
    public void Undo_RemovesTheCopy_AndSelectsTheOriginalsAgain_AndRedoBringsItBack()
    {
        var build = Loaded();
        var before = build.Snapshot();
        var selection = new PartSet(Ids(1, 2, 3, 4, 5), Ids(11, 12), Ids(21), Ids(31), Ids(14), Ids(13));
        build.ReplaceSelection(selection);
        build.CopySelectedParts();
        var copied = build.Snapshot();

        build.Undo();

        ShouldHaveTheSameParts(build.Snapshot(), before);
        build.Selection.ShouldBeEquivalentTo(selection);

        build.Redo();

        ShouldHaveTheSameParts(build.Snapshot(), copied);
    }

    [Fact]
    public void Copy_NearTheBuildAreasCorner_GoesTheOtherWay()
    {
        var corner = BuildViewModel.BuildArea.Max;
        var build = new BuildViewModel();
        build.Load(new CreatureDef(
            [new NodeDef(1, new Vector2D(corner.X - 20, corner.Y - 20)), new NodeDef(2, new Vector2D(corner.X - 120, corner.Y - 20))],
            [new BeamDef(3, 1, 2)],
            []));
        build.ReplaceSelection(new PartSet(Ids(1, 2), Ids(3), Ids(), Ids(), Ids(), Ids()));

        build.CopySelectedParts();

        build.Nodes.Skip(2).Select(node => node.Position).ShouldBe(
            [new Vector2D(corner.X - 20 - _step, corner.Y - 20 - _step), new Vector2D(corner.X - 120 - _step, corner.Y - 20 - _step)]);
    }

    public static TheoryData<PartSet, CreatureElementSelection[], string, bool> Blocked => new()
    {
        { new PartSet(Ids(1), Ids(11), Ids(), Ids(), Ids(), Ids()), [new(CreatureElementKind.Beam, 11)], "Select both its joints", false },
        { new PartSet(Ids(3), Ids(), Ids(), Ids(), Ids(), Ids(13)), [new(CreatureElementKind.Spring, 13)], "Select both its joints", false },
        { new PartSet(Ids(4), Ids(), Ids(), Ids(), Ids(14), Ids()), [new(CreatureElementKind.Piston, 14)], "Select both its joints", false },
        { new PartSet(Ids(1, 2), Ids(), Ids(21), Ids(), Ids(), Ids()), [new(CreatureElementKind.Sensor, 21)], "Select its beam", false },
        { new PartSet(Ids(1, 2), Ids(11), Ids(21), Ids(), Ids(), Ids()), [new(CreatureElementKind.Sensor, 21)], "Locked: would change the model", true },
        { new PartSet(Ids(1, 2), Ids(), Ids(), Ids(31), Ids(), Ids()), [new(CreatureElementKind.Servo, 31)], "Locked: would change the model", true },
        { new PartSet(Ids(4), Ids(), Ids(), Ids(), Ids(14), Ids()), [new(CreatureElementKind.Piston, 14)], "Locked: would change the model", true },
    };

    [Theory]
    [MemberData(nameof(Blocked))]
    public void Copy_OfASelectionItCannotTake_IsDimmed_AndATapMarksTheOffendingParts(PartSet selection, CreatureElementSelection[] offenders, string reason, bool locked)
    {
        var build = new BuildViewModel();
        build.Load(Creature(), locked);
        build.ReplaceSelection(selection);
        var before = build.Snapshot();

        build.CanOfferCopy.ShouldBeTrue();
        build.CanCopySelection.ShouldBeFalse();
        build.CanvasNotes().ShouldBeEmpty();
        build.CopySelectedParts();

        ShouldHaveTheSameParts(build.Snapshot(), before);
        build.CanUndo.ShouldBeFalse();
        build.CanvasNotes().ShouldBe([.. offenders.Select(part => new CanvasNote(CanvasNoteKind.Danger, part, UiText.Plain(reason)))]);
    }

    [Fact]
    public void ADimmedCopy_MarksEveryOffendingPart_KindByKind_InIdOrder()
    {
        var build = Loaded();
        build.ReplaceSelection(new PartSet(Ids(5), Ids(12, 11), Ids(), Ids(), Ids(), Ids(13)));

        build.CopySelectedParts();

        build.CanvasNotes().ShouldBe(
        [
            new CanvasNote(CanvasNoteKind.Danger, new(CreatureElementKind.Beam, 11), UiText.Plain("Select both its joints")),
            new CanvasNote(CanvasNoteKind.Danger, new(CreatureElementKind.Beam, 12), UiText.Plain("Select both its joints")),
            new CanvasNote(CanvasNoteKind.Danger, new(CreatureElementKind.Spring, 13), UiText.Plain("Select both its joints")),
        ]);
    }

    [Fact]
    public void ASelectedServo_BringsItsJoint_SoItsLinksCopy()
    {
        var build = Loaded();
        build.ReplaceSelection(new PartSet(Ids(3), Ids(12), Ids(), Ids(31), Ids(), Ids()));
        build.CanCopySelection.ShouldBeTrue();

        build.CopySelectedParts();

        var (joint, other) = (build.Nodes[^2], build.Nodes[^1]);
        (joint.Position, other.Position).ShouldBe((new Vector2D(100 + _step, _step), new Vector2D(100 + _step, 100 + _step)));
        var beam = build.Beams.Last();
        (beam.NodeA, beam.NodeB).ShouldBe((joint.Id, other.Id));
        var servo = build.Servos.Last();
        (servo.NodeId, servo.FixedLinkId, servo.TargetLinkId).ShouldBe((joint.Id, beam.Id, null));
        build.Selection.Nodes.ShouldBe([joint.Id, other.Id], ignoreOrder: true);
        build.Selection.Beams.ShouldBe([beam.Id]);
        build.Selection.Servos.ShouldBe([servo.Id]);
    }

    [Fact]
    public void OnALockedCreation_ADimmedCopy_MarksBrainPortsFirst_AndOnlyOnce()
    {
        var build = new BuildViewModel();
        build.Load(Creature(), locked: true);
        build.ReplaceSelection(new PartSet(Ids(3), Ids(12, 11), Ids(), Ids(31), Ids(), Ids()));

        build.CopySelectedParts();

        // The Servo still brings its joint 2, so beam 12 has both of its joints.
        build.CanvasNotes().ShouldBe(
        [
            new CanvasNote(CanvasNoteKind.Danger, new(CreatureElementKind.Servo, 31), UiText.Plain("Locked: would change the model")),
            new CanvasNote(CanvasNoteKind.Danger, new(CreatureElementKind.Beam, 11), UiText.Plain("Select both its joints")),
        ]);
    }

    [Fact]
    public void Unlocking_HidesTheLockedCopysNotes()
    {
        var build = new BuildViewModel();
        build.Load(Creature(), locked: true);
        build.ReplaceSelection(new PartSet(Ids(1, 2), Ids(11), Ids(21), Ids(), Ids(), Ids()));
        build.CopySelectedParts();
        build.CanvasNotes().ShouldNotBeEmpty();
        var changed = new List<string?>();
        build.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        build.Unlock();

        build.CanvasNotes().ShouldBeEmpty();
        changed.ShouldContain(nameof(BuildViewModel.CanvasNotes));
        build.CanCopySelection.ShouldBeTrue();
    }

    [Fact]
    public void Copy_OfASelectionAsTallAsTheBuildArea_KeepsItsShape()
    {
        var area = BuildViewModel.BuildArea;
        var r = NodeDef.PlainJointRadius;
        var build = new BuildViewModel();
        build.Load(new CreatureDef(
            [
                new NodeDef(1, new Vector2D(0, area.Min.Y + r)),
                new NodeDef(2, new Vector2D(0, area.Max.Y - r)),
                new NodeDef(3, new Vector2D(0, area.Max.Y - r - (_step / 2))),
            ],
            [new BeamDef(4, 1, 2), new BeamDef(5, 2, 3)],
            []));
        build.ReplaceSelection(new PartSet(Ids(1, 2, 3), Ids(4, 5), Ids(), Ids(), Ids(), Ids()));

        build.CopySelectedParts();

        build.Nodes.Skip(3).Select(node => node.Position).ShouldBe(
        [
            new Vector2D(_step, area.Min.Y + r),
            new Vector2D(_step, area.Max.Y - r),
            new Vector2D(_step, area.Max.Y - r - (_step / 2)),
        ]);
    }

    [Fact]
    public void ADimmedCopysNotes_GoWhenTheSelectionChanges()
    {
        var build = Loaded();
        build.ReplaceSelection(new PartSet(Ids(1), Ids(11), Ids(), Ids(), Ids(), Ids()));
        build.CopySelectedParts();
        build.CanvasNotes().ShouldNotBeEmpty();

        build.ToggleSelected(new CreatureElementSelection(CreatureElementKind.Node, 2));

        build.CanvasNotes().ShouldBeEmpty();
        build.CanCopySelection.ShouldBeTrue();
    }

    [Fact]
    public void ADimmedCopysNotes_GoOnTheNextCanvasTouch()
    {
        var build = Loaded();
        build.ReplaceSelection(new PartSet(Ids(1), Ids(11), Ids(), Ids(), Ids(), Ids()));
        build.CopySelectedParts();
        build.CanvasNotes().ShouldNotBeEmpty();

        new BuildGestures(build).Press(new Vector2D(2000, 2000));

        build.CanvasNotes().ShouldBeEmpty();
    }

    [Fact]
    public void Copy_IsNotOffered_ForOnePart()
    {
        var build = Loaded();
        build.ReplaceSelection(Ids(1));

        build.CanOfferCopy.ShouldBeFalse();
    }

    [Fact]
    public void OnALockedCreation_CopyIsOffered_AndCopiesJointsBeamsAndSprings()
    {
        var build = new BuildViewModel();
        build.Load(Creature(), locked: true);
        build.ReplaceSelection(new PartSet(Ids(1, 2, 3, 4), Ids(11, 12), Ids(), Ids(), Ids(), Ids(13)));

        build.CanOfferCopy.ShouldBeTrue();
        build.CanCopySelection.ShouldBeTrue();
        build.CopySelectedParts();

        (build.Nodes.Count, build.Beams.Count, build.Springs.Count).ShouldBe((9, 4, 2));
        (build.Sensors.Count, build.Servos.Count, build.Pistons.Count).ShouldBe((1, 1, 1));
    }

    private static void ShouldHaveTheSameParts(CreatureDef actual, CreatureDef expected)
    {
        actual.Nodes.ShouldBe(expected.Nodes);
        actual.Beams.ShouldBe(expected.Beams);
        actual.Sensors.ShouldBe(expected.Sensors);
        actual.Servos.ShouldBe(expected.Servos);
        actual.Pistons.ShouldBe(expected.Pistons);
        actual.Springs.ShouldBe(expected.Springs);
    }

    private static BuildViewModel Loaded()
    {
        var build = new BuildViewModel();
        build.Load(Creature());
        return build;
    }

    // A square of joints 1–4 with beams 11 (Camera 21, aimed) and 12, Servo 31 on joint 2, a tuned Spring 13
    // and a tuned Piston 14 out to joint 5.
    private static CreatureDef Creature() => new(
        [
            new NodeDef(1, new Vector2D(0, 0), "Hip"),
            new NodeDef(2, new Vector2D(100, 0)),
            new NodeDef(3, new Vector2D(100, 100)),
            new NodeDef(4, new Vector2D(0, 100)),
            new NodeDef(5, new Vector2D(-100, 100)),
        ],
        [new BeamDef(11, 1, 2), new BeamDef(12, 2, 3)],
        [new SensorDef(21, 11, SensorKind.Camera, aim: 1.0)],
        [new ServoDef(31, 2, 12, 11, strength: 800000, range: Math.PI / 2)],
        [new PistonDef(14, 4, 5, "Kick", strength: 20000, stroke: 0.8, start: 0.25, maxSpeed: 300, riseTime: 0.5)],
        [new SpringDef(13, 3, 4, stiffness: 900, damping: 20, stroke: 0.5, coilLength: 0.4)],
        nextPartId: 40);

    private static HashSet<int> Ids(params int[] ids) => [.. ids];
}
