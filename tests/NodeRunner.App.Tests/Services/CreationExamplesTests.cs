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
    public void Worm_UsesOnlyStartingComponents()
    {
        var worm = CreationExamples.All.Single(example => example.Id == CreationExamples.WormId);

        worm.ShouldBeSameAs(CreationExamples.Worm);
        worm.Name.ShouldBe(UiText.Plain("Worm"));
        worm.Creature.Sensors.ShouldBe(
        [
            new NodeRunner.Domain.SensorDef(8, 5, NodeRunner.Domain.SensorKind.Accelerometer),
            new NodeRunner.Domain.SensorDef(9, 7, NodeRunner.Domain.SensorKind.Camera, aim: -Math.PI / 4),
        ]);
        worm.Creature.Pistons.ShouldBe([new NodeRunner.Domain.PistonDef(10, 2, 4)]);
        NodeRunner.App.Lifecycle.CreatureReadiness.CanTrain(worm.Creature).ShouldBeTrue();
        worm.Training.ShouldBeNull();
    }

    [Fact]
    public void Frog_IsReadyToTrainWithThreePistonsInsideTheBuildArea()
    {
        var frog = CreationExamples.All.Single(example => example.Id == CreationExamples.FrogId);

        frog.ShouldBeSameAs(CreationExamples.Frog);
        frog.Name.ShouldBe(UiText.Plain("Frog"));
        frog.Creature.Pistons.Count.ShouldBe(3);
        frog.Creature.Sensors.ShouldBe([new NodeRunner.Domain.SensorDef(13, 7, NodeRunner.Domain.SensorKind.Accelerometer)]);
        frog.Creature.Nodes.ShouldAllBe(node => BuildViewModel.BuildArea.Contains(node.Position));
        NodeRunner.App.Lifecycle.CreatureReadiness.CanTrain(frog.Creature).ShouldBeTrue();
        frog.Training.ShouldBeNull();
    }

    [Fact]
    public void Examples_HaveDistinctIds() =>
        CreationExamples.All.Select(example => example.Id).ShouldBeUnique();
}
