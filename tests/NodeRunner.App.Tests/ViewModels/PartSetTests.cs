using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class PartSetTests
{
    public static TheoryData<CreatureElementKind> EveryKind => [.. Enum.GetValues<CreatureElementKind>()];

    [Fact]
    public void None_AndAnEmptySequence_HoldNothing()
    {
        foreach (var set in new[] { PartSet.None, new PartSet([]) })
        {
            set.Count.ShouldBe(0);
            set.Parts.ShouldBeEmpty();
            set.SetEquals(PartSet.None).ShouldBeTrue();
        }
    }

    [Theory]
    [MemberData(nameof(EveryKind))]
    public void OnePartOfEveryKind_IsCountedListedAndLookedUp(CreatureElementKind kind)
    {
        var part = new CreatureElementSelection(kind, 7);

        var set = PartSet.Of(part);

        set.Count.ShouldBe(1);
        set.Parts.ShouldBe([part]);
        (set.Contains(part), set.Contains(kind, 7)).ShouldBe((true, true));
        set.IdsOf(kind).ShouldBe([7]);
        Enum.GetValues<CreatureElementKind>().Where(other => other != kind).ShouldAllBe(other => set.IdsOf(other).Count == 0 && !set.Contains(other, 7));
    }

    [Fact]
    public void AMixedSet_HoldsEachPartOnce_ListsThemKindByKind_AndComparesAsASet()
    {
        var stepper = new CreatureElementSelection(CreatureElementKind.Stepper, 9);
        var joint = new CreatureElementSelection(CreatureElementKind.Node, 3);
        var beam = new CreatureElementSelection(CreatureElementKind.Beam, 4);
        var otherJoint = new CreatureElementSelection(CreatureElementKind.Node, 1);

        var set = new PartSet([stepper, joint, beam, otherJoint, joint]);

        set.Count.ShouldBe(4);
        set.Parts.ShouldBe([otherJoint, joint, beam, stepper]);
        set.IdsOf(CreatureElementKind.Node).ShouldBe([3, 1], ignoreOrder: true);
        set.SetEquals(new PartSet([beam, otherJoint, stepper, joint])).ShouldBeTrue();
        set.SetEquals(new PartSet([beam, otherJoint, stepper])).ShouldBeFalse();

        // The same id as another kind is another part.
        set.SetEquals(new PartSet([beam, otherJoint, stepper, new CreatureElementSelection(CreatureElementKind.Beam, 3)])).ShouldBeFalse();
    }

    [Fact]
    public void ANullPart_IsRefused()
    {
        Should.Throw<ArgumentException>(() => new PartSet([null!]));
    }
}
