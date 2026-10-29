using NodeRunner.App.Tests.Repositories;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;
using static NodeRunner.App.Tests.ViewModels.BuildFixtures;

namespace NodeRunner.App.Tests.ViewModels;

/// <summary>
/// Build's selection over a creature with every part kind (<see cref="EveryPartPinTests.Creature"/>):
/// joint 1 stacks a Servo and a Touch sensor, joint 4 a Brake and a Wheel.
/// </summary>
public sealed class BuildSelectionTests
{
    private const int _stackedServo = 21;
    private const int _stackedTouchSensor = 27;
    private const int _stackedWheel = 23;
    private const int _stackedBrake = 24;

    public static TheoryData<CreatureElementKind> EveryKind => [.. Enum.GetValues<CreatureElementKind>()];

    [Theory]
    [MemberData(nameof(EveryKind))]
    public void APartOfEveryKind_IsToggledReplacedAndCleared_WithItsNotifications(CreatureElementKind kind)
    {
        var build = Loaded(EveryPartPinTests.Creature());
        var part = AllParts(build).First(part => part.Kind == kind);
        var raised = new List<string?>();
        build.PropertyChanged += (_, args) => raised.Add(args.PropertyName);
        string[] selectionNames = [nameof(BuildViewModel.SelectedNodeCount), nameof(BuildViewModel.SelectedPartCount), nameof(BuildViewModel.SingleSelected)];

        build.ToggleSelected(part);
        (build.SelectedPartCount, build.SingleSelected, build.SingleSelectedId(kind)).ShouldBe((1, part, part.Id));
        Enum.GetValues<CreatureElementKind>().Where(other => other != kind).ShouldAllBe(other => build.SingleSelectedId(other) == null);
        selectionNames.ShouldBeSubsetOf(raised);

        raised.Clear();
        build.ToggleSelected(part);
        (build.SelectedPartCount, build.SingleSelected).ShouldBe((0, null));
        selectionNames.ShouldBeSubsetOf(raised);

        raised.Clear();
        build.ReplaceSelection(PartSet.Of(part));
        build.SingleSelected.ShouldBe(part);
        selectionNames.ShouldBeSubsetOf(raised);

        raised.Clear();
        build.ClearSelection();
        build.SelectedPartCount.ShouldBe(0);
        selectionNames.ShouldBeSubsetOf(raised);

        // Clearing nothing tells nobody.
        raised.Clear();
        build.ClearSelection();
        raised.ShouldBeEmpty();
    }

    [Fact]
    public void EveryPart_IsSelectedTogether_DroppingPartsThatDoNotExist()
    {
        var build = Loaded(EveryPartPinTests.Creature());
        var all = AllParts(build);

        // Servo 21 exists, but not as a beam.
        build.ReplaceSelection(new PartSet([.. all, new(CreatureElementKind.Servo, 99), Beam(_stackedServo)]));

        build.Selection.SetEquals(new PartSet(all)).ShouldBeTrue();
        (build.SelectedPartCount, build.SingleSelected).ShouldBe((all.Count, null));
        build.SelectedNodeIds.ShouldBe(build.Nodes.Select(node => node.Id), ignoreOrder: true);

        // A tap can only toggle a part that exists.
        build.ClearSelection();
        Should.Throw<ArgumentException>(() => build.ToggleSelected(new CreatureElementSelection(CreatureElementKind.Servo, 99)));
        build.SelectedPartCount.ShouldBe(0);
    }

    [Fact]
    public void Undo_SelectsEveryDeletedPartAgain_AndDropsAPartItTakesAway()
    {
        var build = Loaded(EveryPartPinTests.Creature());
        var all = AllParts(build);

        build.ReplaceSelection(new PartSet(all));
        build.DeleteSelectedParts();
        build.Snapshot().Parts.ShouldBeEmpty();
        build.SelectedPartCount.ShouldBe(0);

        build.Undo();
        build.Selection.SetEquals(new PartSet(all)).ShouldBeTrue();

        // Undoing a placed joint takes it out of the selection too. That undo has no selection of
        // its own, so each joint with parts gives way to them, and every joint here has parts.
        var bare = build.PlaceNode(new Vector2D(600, -200));
        build.ReplaceSelection(new PartSet([.. all, Joint(bare)]));
        build.Undo();
        build.Selection.SetEquals(new PartSet(all.Where(part => part.Kind != CreatureElementKind.Node))).ShouldBeTrue();
    }

    [Fact]
    public void AStackedJoint_IsMovedOnce_AndUndoSelectsItsTopPartInsteadOfIt()
    {
        var build = Loaded(EveryPartPinTests.Creature());

        build.ReplaceSelection(Set(Joint(1), new(CreatureElementKind.Servo, _stackedServo), new(CreatureElementKind.TouchSensor, _stackedTouchSensor)));
        build.SelectedNodeIds.ShouldBe([1]);
        var start = build.SnapshotSelection();
        start.Positions.Keys.ShouldBe([1]);
        build.TranslateSelection(start, new Vector2D(0, 30));
        build.Nodes.Single(node => node.Id == 1).Position.ShouldBe(new Vector2D(0, 30));

        // An undo without its own selection keeps a selected joint as the top of its stack.
        build.PlaceNode(new Vector2D(600, -200));
        build.ReplaceSelection([4]);
        build.Undo();
        build.Selection.Parts.ShouldBe([new CreatureElementSelection(CreatureElementKind.Brake, _stackedBrake)]);
        build.SelectedNodeIds.ShouldBe([4]);
    }

    [Fact]
    public void TheSelectBox_CatchesEveryKind_AndAStackedJointAsItsParts()
    {
        var build = Loaded(EveryPartPinTests.Creature());
        var bare = build.PlaceNode(new Vector2D(600, -200));
        build.ActiveTool = BuildTool.Select;
        var gestures = new BuildGestures(build);

        gestures.DragAcross(new Vector2D(-150, -350), new Vector2D(750, 150));
        build.Selection.SetEquals(new PartSet([.. AllParts(build).Where(part => part.Kind != CreatureElementKind.Node), Joint(bare)])).ShouldBeTrue();

        gestures.DragAcross(new Vector2D(680, -60), new Vector2D(540, 60));
        build.Selection.Parts.ShouldBe([new(CreatureElementKind.Wheel, _stackedWheel), new(CreatureElementKind.Brake, _stackedBrake)]);
    }

    private static List<CreatureElementSelection> AllParts(BuildViewModel build) => [.. build.Snapshot().Parts.Select(CreatureElementSelection.Of)];
}
