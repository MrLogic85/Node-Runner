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
        build.ReplaceSelection(new PartSet(Ids(1, 2), Ids(11), Ids(), Ids(), Ids(), Ids()));
        build.CopySelectedParts();
        var copied = build.Snapshot();

        build.Undo();

        build.Snapshot().Nodes.ShouldBe(before.Nodes);
        build.Snapshot().Beams.ShouldBe(before.Beams);
        build.Selection.Nodes.ShouldBe([1, 2], ignoreOrder: true);
        build.Selection.Beams.ShouldBe([11]);

        build.Redo();

        build.Snapshot().Nodes.ShouldBe(copied.Nodes);
        build.Snapshot().Beams.ShouldBe(copied.Beams);
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

    public static TheoryData<PartSet, CreatureElementSelection[], string> Blocked => new()
    {
        { new PartSet(Ids(1), Ids(11), Ids(), Ids(), Ids(), Ids()), [new(CreatureElementKind.Beam, 11)], "Select both its joints" },
        { new PartSet(Ids(3), Ids(), Ids(), Ids(), Ids(), Ids(13)), [new(CreatureElementKind.Spring, 13)], "Select both its joints" },
        { new PartSet(Ids(1, 2), Ids(11), Ids(21), Ids(), Ids(), Ids()), [new(CreatureElementKind.Sensor, 21)], "Would change the brain" },
        { new PartSet(Ids(1, 2), Ids(), Ids(), Ids(31), Ids(), Ids()), [new(CreatureElementKind.Servo, 31)], "Would change the brain" },
        { new PartSet(Ids(4, 5), Ids(), Ids(), Ids(), Ids(14), Ids()), [new(CreatureElementKind.Piston, 14)], "Would change the brain" },
    };

    [Theory]
    [MemberData(nameof(Blocked))]
    public void Copy_OfASelectionItCannotTake_IsDimmed_AndATapMarksTheOffendingParts(PartSet selection, CreatureElementSelection[] offenders, string reason)
    {
        var build = Loaded();
        build.ReplaceSelection(selection);
        var before = build.Snapshot();

        build.CanCopySelection.ShouldBeFalse();
        build.CanvasNotes().ShouldBeEmpty();
        build.CopySelectedParts();

        build.Nodes.ShouldBe(before.Nodes);
        build.Beams.ShouldBe(before.Beams);
        build.CanUndo.ShouldBeFalse();
        build.CanvasNotes().ShouldBe([.. offenders.Select(part => new CanvasNote(CanvasNoteKind.Danger, part, UiText.Plain(reason)))]);
    }

    [Fact]
    public void ADimmedCopy_MarksEveryOffendingPart_BrainPortsFirst_ThenOpenLinksInIdOrder()
    {
        var build = Loaded();
        build.ReplaceSelection(new PartSet(Ids(3), Ids(12, 11), Ids(), Ids(31), Ids(), Ids()));

        build.CopySelectedParts();

        build.CanvasNotes().ShouldBe(
        [
            new CanvasNote(CanvasNoteKind.Danger, new(CreatureElementKind.Servo, 31), UiText.Plain("Would change the brain")),
            new CanvasNote(CanvasNoteKind.Danger, new(CreatureElementKind.Beam, 11), UiText.Plain("Select both its joints")),
            new CanvasNote(CanvasNoteKind.Danger, new(CreatureElementKind.Beam, 12), UiText.Plain("Select both its joints")),
        ]);
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
    public void Copy_IsNotOffered_ForOnePart_OrOnALockedCreation()
    {
        var build = Loaded();
        build.ReplaceSelection(Ids(1));
        build.CanOfferCopy.ShouldBeFalse();

        var locked = new BuildViewModel();
        locked.Load(Creature(), moveOnly: true);
        locked.ReplaceSelection(Ids(1, 2));
        locked.CanOfferCopy.ShouldBeFalse();
        locked.CopySelectedParts();

        locked.Nodes.Count.ShouldBe(5);
        locked.CanvasNotes().ShouldBeEmpty();
    }

    private static BuildViewModel Loaded()
    {
        var build = new BuildViewModel();
        build.Load(Creature());
        return build;
    }

    // A square of joints 1–4 with beams 11 (sensor 21) and 12, Servo 31 on joint 2, a tuned Spring 13
    // and Piston 14 out to joint 5.
    private static CreatureDef Creature() => new(
        [
            new NodeDef(1, new Vector2D(0, 0), "Hip"),
            new NodeDef(2, new Vector2D(100, 0)),
            new NodeDef(3, new Vector2D(100, 100)),
            new NodeDef(4, new Vector2D(0, 100)),
            new NodeDef(5, new Vector2D(-100, 100)),
        ],
        [new BeamDef(11, 1, 2), new BeamDef(12, 2, 3)],
        [new SensorDef(21, 11, SensorKind.Accelerometer)],
        [new ServoDef(31, 2, 11, 12)],
        [new PistonDef(14, 4, 5)],
        [new SpringDef(13, 3, 4, stiffness: 900, damping: 20, stroke: 0.5, coilLength: 0.4)],
        nextPartId: 40);

    private static HashSet<int> Ids(params int[] ids) => [.. ids];
}
