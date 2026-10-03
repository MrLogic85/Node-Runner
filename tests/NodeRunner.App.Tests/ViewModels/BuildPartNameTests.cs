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

        build.RenamePart(101, "  Thigh ");

        build.Snapshot().Beams[0].Name.ShouldBe("Thigh");
        build.PartDisplayName(101).ShouldBe("Thigh");
        changes.ShouldBe(1);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Node 2")]
    public void RenamePart_BlankOrDefaultName_ClearsTheName(string name)
    {
        var build = Loaded();
        build.RenamePart(2, "Hip");

        build.RenamePart(2, name);

        build.Snapshot().Nodes[1].Name.ShouldBeNull();
        build.PartDisplayName(2).ShouldBe("Node 2");
    }

    [Fact]
    public void RenamePart_SameName_DoesNothing()
    {
        var build = Loaded();
        var changes = 0;
        build.AnatomyChanged += (_, _) => changes++;

        build.RenamePart(7, "Accelerometer");

        changes.ShouldBe(0);
    }

    [Fact]
    public void RenamePart_LandsOnThatPart_WhateverIsSelected()
    {
        var build = Loaded();
        build.ToggleSelected(new(CreatureElementKind.Node, 1));

        build.RenamePart(101, "Thigh");

        build.PartDisplayName(101).ShouldBe("Thigh");
        build.PartDisplayName(1).ShouldBe("Node 1");
    }

    [Fact]
    public void RenamePart_OfADeletedPart_DoesNothing()
    {
        var build = Loaded();
        build.SelectSensor(7);
        build.DeleteSelectedParts();
        var changes = 0;
        build.AnatomyChanged += (_, _) => changes++;

        build.RenamePart(7, "Balance");

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

        build.RenamePart(7, "Balance");

        build.PartDisplayName(7).ShouldBe("Balance");
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
