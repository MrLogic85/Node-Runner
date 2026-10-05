using NodeRunner.App.Builders;
using NodeRunner.App.Services;
using NodeRunner.App.ViewModels;

namespace NodeRunner.App.Tests.Services;

public sealed class CreationExamplesTests
{
    [Fact]
    public void EveryExample_PassesBuildValidationAndSaysWhatIsNew()
    {
        foreach (var example in CreationExamples.All)
        {
            var canBuild = new CreatureBuilder(example.Creature).TryBuild(out var built, out var errors);

            canBuild.ShouldBeTrue(example.Name.Message);
            errors.ShouldBeEmpty();
            built.ShouldNotBeNull();
            example.WhatIsNew.Message.ShouldNotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void Walker_IsReadyToTrainWithTwoPistonsInsideTheBuildArea()
    {
        var walker = CreationExamples.All.ShouldHaveSingleItem();

        walker.ShouldBeSameAs(CreationExamples.Walker);
        walker.Id.ShouldBe(CreationExamples.WalkerId);
        walker.Name.ShouldBe(UiText.Plain("Walker"));
        walker.Creature.Nodes.Select(node => node.Position).ShouldBe(
        [
            new NodeRunner.Domain.Vector2D(0, -120),
            new NodeRunner.Domain.Vector2D(200, -120),
            new NodeRunner.Domain.Vector2D(-40, 0),
            new NodeRunner.Domain.Vector2D(240, 0),
        ]);
        walker.Creature.Beams.ShouldBe([new NodeRunner.Domain.BeamDef(5, 1, 2), new NodeRunner.Domain.BeamDef(6, 1, 3), new NodeRunner.Domain.BeamDef(7, 2, 4)]);
        walker.Creature.Pistons.ShouldBe([new NodeRunner.Domain.PistonDef(9, 2, 3), new NodeRunner.Domain.PistonDef(10, 1, 4)]);
        walker.Creature.Sensors.ShouldBe([new NodeRunner.Domain.SensorDef(8, 5, NodeRunner.Domain.SensorKind.Accelerometer)]);
        walker.Creature.Springs.ShouldBeEmpty();
        walker.Creature.Nodes.ShouldAllBe(node => BuildViewModel.BuildArea.Contains(node.Position));
        NodeRunner.App.Lifecycle.CreatureReadiness.CanTrain(walker.Creature).ShouldBeTrue();
        walker.Training.ShouldBeNull();
    }

    [Fact]
    public void Examples_HaveDistinctIds() =>
        CreationExamples.All.Select(example => example.Id).ShouldBeUnique();
}
