using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class BuildPartNameTests
{
    [Fact]
    public void RenamePart_SetsTheTrimmedNameAndMarksTheDrawingChanged()
    {
        var build = Loaded();
        var changes = 0;
        build.AnatomyChanged += (_, _) => changes++;

        build.RenamePart(101, "  Thigh ", null);

        build.Snapshot().Beams[0].Name.ShouldBe("Thigh");
        build.PartDisplayName(101).ShouldBe(UiText.AsWritten("Thigh"));
        changes.ShouldBe(1);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void RenamePart_BlankName_ClearsTheName(string name)
    {
        var build = Loaded();
        build.RenamePart(2, "Hip", null);

        build.RenamePart(2, name, null);

        build.Snapshot().Nodes[1].Name.ShouldBeNull();
        build.PartDisplayName(2).ShouldBe(UiText.Format("Node {0}", 2));
    }

    [Fact]
    public void RenamePart_ShownDefaultLeftUnchanged_KeepsTheDefault()
    {
        var build = Loaded();
        var changes = 0;
        build.AnatomyChanged += (_, _) => changes++;

        build.RenamePart(2, " Nod 2 ", "Nod 2");

        build.Snapshot().Nodes[1].Name.ShouldBeNull();
        changes.ShouldBe(0);
    }

    // The default is compared in the language it was shown in, so the English default typed in
    // another language is the player's own name.
    [Fact]
    public void RenamePart_ToTheEnglishDefaultWhileAnotherIsShown_KeepsItAsWritten()
    {
        var build = Loaded();

        build.RenamePart(2, "Node 2", "Nod 2");

        build.PartDisplayName(2).ShouldBe(UiText.AsWritten("Node 2"));
    }

    [Fact]
    public void RenamePart_SameName_DoesNothing()
    {
        var build = Loaded();
        build.RenamePart(7, "Balance", null);
        var changes = 0;
        build.AnatomyChanged += (_, _) => changes++;

        build.RenamePart(7, "Balance", null);

        changes.ShouldBe(0);
    }

    [Fact]
    public void RenamePart_LandsOnThatPart_WhateverIsSelected()
    {
        var build = Loaded();
        build.ToggleSelected(new(CreatureElementKind.Node, 1));

        build.RenamePart(101, "Thigh", null);

        build.PartDisplayName(101).ShouldBe(UiText.AsWritten("Thigh"));
        build.PartDisplayName(1).ShouldBe(UiText.Format("Node {0}", 1));
    }

    [Fact]
    public void RenamePart_OfADeletedPart_DoesNothing()
    {
        var build = Loaded();
        build.SelectSensor(7);
        build.DeleteSelectedParts();
        var changes = 0;
        build.AnatomyChanged += (_, _) => changes++;

        build.RenamePart(7, "Balance", null);

        changes.ShouldBe(0);
    }

    [Fact]
    public void RenamePart_WorksOnALockedCreation()
    {
        var build = new BuildViewModel();
        build.LoadCreation(new CreationDef(
            Guid.NewGuid(),
            "Worm",
            Loaded().Snapshot(),
            TestTraining.State(3, 1, TestTraining.Run)));
        build.IsMoveOnly.ShouldBeTrue();

        build.RenamePart(7, "Balance", null);

        build.PartDisplayName(7).ShouldBe(UiText.AsWritten("Balance"));
    }

    private static BuildViewModel Loaded()
    {
        var build = new BuildViewModel();
        build.Load(new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(70, 0))],
            [new BeamDef(101, 1, 2)],
            [new SensorDef(7, 101, SensorKind.Accelerometer)]));
        return build;
    }
}
