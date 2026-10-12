using NodeRunner.App.Builders;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.ViewModels;

/// <summary>
/// A Brake (#1039) in a Creation Build opens: Build cannot place or select one yet (#1041), but its
/// edits, deletes and undo steps keep it.
/// </summary>
public sealed class BuildBrakeTests
{
    [Fact]
    public void UndoAndRedo_OfAnUnrelatedEdit_KeepTheBrake()
    {
        var build = WithABrakeAndARubber(out var brakes);

        build.MoveNode(1, new Vector2D(0, 40));
        build.Undo();

        build.Nodes[0].Position.ShouldBe(new Vector2D(0, 0));
        build.Snapshot().Brakes.ShouldBe(brakes);

        build.Redo();

        build.Nodes[0].Position.ShouldBe(new Vector2D(0, 40));
        build.Snapshot().Brakes.ShouldBe(brakes);
    }

    // A joint holding a Brake is not bare, so a delete that takes its links keeps it (#973).
    [Fact]
    public void Delete_OfTheLinksOfAJointWithABrake_KeepsTheJointAndTheBrake()
    {
        var build = WithABrakeAndARubber(out _);
        build.ReplaceSelection(PartSet.None with { Beams = new HashSet<int> { 4, 5 }, Rubbers = new HashSet<int>(build.Rubbers.Select(rubber => rubber.Id)) });

        build.DeleteSelectedParts();

        build.Nodes.ShouldContain(node => node.Id == 2);
        build.Brakes.ShouldHaveSingleItem().FixedLinkId.ShouldBeNull();
        build.Rubbers.ShouldBeEmpty();
    }

    // Joint 2 between beams 4 and 5 holds a Locked Brake and a Rubber.
    private static BuildViewModel WithABrakeAndARubber(out IReadOnlyList<BrakeDef> brakes)
    {
        var builder = new CreatureBuilder(new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(100, 0)), new NodeDef(3, new Vector2D(200, 0))],
            [new BeamDef(4, 1, 2), new BeamDef(5, 2, 3)],
            [],
            nextPartId: 6));
        var brake = builder.AddBrake(2);
        builder.SetBrakeStrength(brake, BrakeDef.LockedStrength);
        builder.AddRubber(2);
        var build = new BuildViewModel();
        build.Load(builder.Build());
        brakes = [.. build.Brakes];
        return build;
    }
}
