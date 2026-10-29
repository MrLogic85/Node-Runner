using NodeRunner.App.Builders;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;
using static NodeRunner.App.Tests.ViewModels.BuildFixtures;

namespace NodeRunner.App.Tests.ViewModels;

/// <summary>
/// Build's delete, copy, undo and note targets on <see cref="PortedCreature"/>, which has a part of
/// every kind, so a new kind that one of them misses fails here.
/// </summary>
public sealed class BuildEveryPartTests
{
    // What deleting each part alone takes from a locked Creation's network: the parts whose
    // ports go or whose sides change, cascades included (BuildViewModel.DeleteRefusalNotes).
    public static TheoryData<int, int[]> LockedDeleteBlockers => new()
    {
        { 1, [8, 10, 11, 16, 18] }, // Beams 5 and 7 with Accelerometer 8, Piston 11 and Servo 10; sides of 16 and 18.
        { 2, [8, 9, 10, 14, 16, 18, 22] },
        { 3, [9, 11, 16] }, // Wheel 13, Rubber 15 and Brake 17 go without ports.
        { 4, [10, 18, 22] },
        { 19, [22] },
        { 5, [8, 10, 16] },
        { 6, [9, 16] }, // Brake 17 loses its Fixed side.
        { 7, [10, 18] },
        { PortedCreature.Accelerometer, [8] },
        { PortedCreature.Camera, [9] },
        { PortedCreature.Servo, [10] },
        { PortedCreature.Piston, [11] },
        { PortedCreature.Spring, [18] },
        { PortedCreature.Wheel, [] },
        { PortedCreature.Touch, [14] },
        { PortedCreature.Rubber, [] },
        { PortedCreature.VelocityMotor, [16] },
        { PortedCreature.Brake, [] },
        { PortedCreature.AngleSensor, [18] },
        { 20, [22] },
        { 21, [22] },
        { PortedCreature.Stepper, [22] },
    };

    [Fact]
    public void LockedDeleteBlockers_ListEveryPart()
    {
        LockedDeleteBlockers.Select(row => (int)row[0]).ShouldBe(PortedCreature.Def.Parts.Select(part => part.Id), ignoreOrder: true);
    }

    [Theory]
    [MemberData(nameof(LockedDeleteBlockers))]
    public void Delete_OfAnyPart_GoesAsOneUndoStep_OrIsRefusedOnALockedCreation_WithANoteOnEachBlocker(int partId, int[] blockers)
    {
        var part = Selected(partId);
        var open = Loaded(PortedCreature.Def);
        var locked = Loaded(PortedCreature.Def, locked: true);

        open.ReplaceSelection(PartSet.Of(part));
        open.DeleteSelectedParts();
        open.FindPart(partId).ShouldBeNull();
        open.Undo();
        open.Snapshot().Parts.ShouldBe(PortedCreature.Def.Parts);
        open.Selection.SetEquals(PartSet.Of(part)).ShouldBeTrue();

        locked.ReplaceSelection(PartSet.Of(part));
        locked.DeleteSelectedParts();
        // The fixture is small, so its links are also too short to train.
        locked.CanvasNotes().Where(note => note.Text.Equals(UiText.Plain(LockedText))).ShouldBe([.. blockers.Select(id => new CanvasNote(CanvasNoteKind.Danger, Selected(id), UiText.Plain(LockedText)))]);
        if (blockers.Length == 0)
        {
            locked.FindPart(partId).ShouldBeNull();
        }
        else
        {
            locked.Snapshot().Parts.ShouldBe(PortedCreature.Def.Parts);
            locked.CanUndo.ShouldBeFalse();
        }
    }

    [Fact]
    public void Delete_OfEveryPart_LeavesNothing_AndUndoBringsItAllBack()
    {
        var build = Loaded(PortedCreature.Def);
        build.ReplaceSelection(new PartSet(PortedCreature.Def.Parts.Select(CreatureElementSelection.Of)));

        build.DeleteSelectedParts();
        build.Parts.ShouldBeEmpty();

        build.Undo();
        build.Snapshot().Parts.ShouldBe(PortedCreature.Def.Parts);
    }

    [Fact]
    public void Copy_OfEveryPart_CopiesEachWithItsSettings_OnTheCopiesOfWhatItUses_AndSelectsTheCopies()
    {
        var build = Loaded(PortedCreature.Def);
        build.ReplaceSelection(new PartSet(PortedCreature.Def.Parts.Select(CreatureElementSelection.Of)));

        build.CopySelectedParts();

        // Pair each part with its copy: the copies of a kind follow its originals in the same order.
        var copies = PortedCreature.Def.Parts
            .GroupBy(part => part.PartKind)
            .SelectMany(kind => kind.Zip(build.Parts.Where(part => part.PartKind == kind.Key).Skip(kind.Count())))
            .ToDictionary(pair => pair.First.Id, pair => pair.Second);
        copies.Count.ShouldBe(PortedCreature.Def.Parts.Count);
        var body = new CreatureBuilder(build.Snapshot());
        foreach (var original in PortedCreature.Def.Parts)
        {
            var copy = copies[original.Id];
            copy.Name.ShouldBeNull();
            Uses(copy).ShouldBe(Uses(original).Select(id => id is { } used ? copies[used].Id : (int?)null));
            body.ParametersOf(copy.Id).Select(parameter => body.ParameterValue(copy.Id, parameter))
                .ShouldBe(body.ParametersOf(original.Id).Select(parameter => body.ParameterValue(original.Id, parameter)));
        }

        build.Selection.SetEquals(new PartSet(copies.Values.Select(CreatureElementSelection.Of))).ShouldBeTrue();
        build.Undo();
        build.Snapshot().Parts.ShouldBe(PortedCreature.Def.Parts);
    }

    [Fact]
    public void Copy_OnALockedCreation_NotesEveryPartWithNetworkPorts_InIdOrder()
    {
        var build = Loaded(PortedCreature.Def, locked: true);
        build.ReplaceSelection(new PartSet(PortedCreature.Def.Parts.Select(CreatureElementSelection.Of)));

        build.CopyBlockers().Select(note => (note.Target, note.Text)).ShouldBe(
            NetworkPorts.Of(PortedCreature.Def).Inputs.Concat(NetworkPorts.Of(PortedCreature.Def).Outputs)
                .Select(port => port.PartId).Distinct().Order()
                .Select(id => (Selected(id), UiText.Plain(LockedText))));
    }

    [Fact]
    public void Undo_SeesAChangeToAnyPart()
    {
        var build = Loaded(PortedCreature.Def);

        foreach (var part in PortedCreature.Def.Parts)
        {
            build.RenamePart(part.Id, "Renamed", shownDefault: null);
            build.CanUndo.ShouldBeTrue();
            build.Undo();
            build.CanUndo.ShouldBeFalse();
            build.Snapshot().Parts.ShouldBe(PortedCreature.Def.Parts);
        }
    }

    [Fact]
    public void NoteTargets_StandByAJoint_AJointPartsJoint_OrBothEndsOfALinkOrASensorsBeam()
    {
        var build = Loaded(PortedCreature.Def);

        var expected = new Dictionary<int, int[]>
        {
            [1] = [1], [2] = [2], [3] = [3], [4] = [4], [19] = [19],
            [5] = [1, 2], [6] = [2, 3], [7] = [1, 4], [20] = [4, 19], [21] = [2, 19],
            [PortedCreature.Accelerometer] = [1, 2],
            [PortedCreature.Camera] = [2, 3],
            [PortedCreature.Servo] = [1],
            [PortedCreature.Piston] = [1, 3],
            [PortedCreature.Spring] = [2, 4],
            [PortedCreature.Wheel] = [3],
            [PortedCreature.Touch] = [2],
            [PortedCreature.Rubber] = [3],
            [PortedCreature.VelocityMotor] = [2],
            [PortedCreature.Brake] = [3],
            [PortedCreature.AngleSensor] = [4],
            [PortedCreature.Stepper] = [19],
        };

        expected.Keys.ShouldBe(PortedCreature.Def.Parts.Select(part => part.Id), ignoreOrder: true);
        foreach (var (partId, joints) in expected)
        {
            CanvasNoteTargets.JointIds(Selected(partId), build).ShouldBe(joints);
        }
    }

    private static CreatureElementSelection Selected(int partId) => CreatureElementSelection.Of(PortedCreature.Def.FindPart(partId)!);

    // The parts a part sits on or takes as a side, in a fixed order per kind.
    private static int?[] Uses(IPartDef part) => part switch
    {
        BeamDef beam => [beam.NodeA, beam.NodeB],
        PistonDef piston => [piston.NodeA, piston.NodeB],
        SpringDef spring => [spring.NodeA, spring.NodeB],
        SensorDef sensor => [sensor.BeamId],
        IHingePartDef hinge => [hinge.NodeId, hinge.FixedLinkId, hinge.TargetLinkId],
        IJointPartDef jointPart => [jointPart.NodeId],
        _ => [],
    };
}
