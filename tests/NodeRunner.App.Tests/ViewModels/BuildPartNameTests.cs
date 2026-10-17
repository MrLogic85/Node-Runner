using NodeRunner.App.ViewModels;
using NodeRunner.Domain;
using static NodeRunner.App.Tests.ViewModels.BuildFixtures;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class BuildPartNameTests
{
    [Fact]
    public void RenamePart_SetsATrimmedName_ClearsABlankOne_AndKeepsAShownDefault()
    {
        var build = Loaded(Creature());
        build.ToggleSelected(Joint(1));
        var changes = build.CountAnatomyChanges();

        build.RenamePart(101, "  Thigh ", null);
        build.Snapshot().Beams[0].Name.ShouldBe("Thigh");
        build.PartDisplayName(101).ShouldBe(UiText.AsWritten("Thigh"));
        build.PartDisplayName(1).ShouldBe(UiText.Format("Joint {0}", 1));
        changes().ShouldBe(1);
        build.RenamePart(101, "Thigh", null);
        changes().ShouldBe(1);

        foreach (var blank in new[] { "", "   " })
        {
            build.RenamePart(2, "Hip", null);
            build.RenamePart(2, blank, null);
            build.Snapshot().Nodes[1].Name.ShouldBeNull();
            build.PartDisplayName(2).ShouldBe(UiText.Format("Joint {0}", 2));
        }

        changes().ShouldBe(5);
        build.RenamePart(2, " [Joint 2] ", "[Joint 2]");
        build.Snapshot().Nodes[1].Name.ShouldBeNull();
        changes().ShouldBe(5);

        // The default is compared in the language it was shown in, so the English default typed in
        // another language is the player's own name.
        build.RenamePart(2, "Joint 2", "[Joint 2]");
        build.PartDisplayName(2).ShouldBe(UiText.AsWritten("Joint 2"));
    }

    [Fact]
    public void RenamePart_WorksOnALockedCreation_AndNotOnADeletedPart()
    {
        var build = new BuildViewModel();
        build.LoadCreation(new CreationDef(Guid.NewGuid(), "Worm", Creature(), TestTraining.State(3, 1, TestTraining.Run)));
        build.IsLocked.ShouldBeTrue();
        build.RenamePart(7, "Balance", null);
        build.PartDisplayName(7).ShouldBe(UiText.AsWritten("Balance"));

        build.Unlock();
        build.SelectOnly(CreatureElementKind.Sensor, 7);
        build.DeleteSelectedParts();
        var changes = build.CountAnatomyChanges();
        build.RenamePart(7, "Balance", null);
        changes().ShouldBe(0);
    }

    private static CreatureDef Creature() => new(
        [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(70, 0)), new BeamDef(101, 1, 2), new SensorDef(7, 101, SensorKind.Accelerometer)]);
}
