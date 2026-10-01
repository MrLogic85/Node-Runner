using NodeRunner.App.Builders;
using NodeRunner.App.Services;

namespace NodeRunner.App.Tests.Services;

public sealed class CreationExamplesTests
{
    [Fact]
    public void EveryExample_PassesBuildValidationAndSaysWhatIsNew()
    {
        foreach (var example in CreationExamples.All)
        {
            var canBuild = new CreatureBuilder(example.Creation.Creature).TryBuild(out var built, out var errors);

            canBuild.ShouldBeTrue(example.Creation.Name);
            errors.ShouldBeEmpty();
            built.ShouldNotBeNull();
            example.WhatIsNew.ShouldNotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void Worm_UsesOnlyStartingComponents()
    {
        var worm = CreationExamples.All.Single(example => example.Creation.Id == CreationExamples.WormId).Creation;

        worm.Creature.Sensors.ShouldBe(
        [
            new NodeRunner.Domain.SensorDef(10, 6, NodeRunner.Domain.SensorKind.Accelerometer),
            new NodeRunner.Domain.SensorDef(11, 9, NodeRunner.Domain.SensorKind.Camera),
        ]);
        NodeRunner.App.Lifecycle.CreatureReadiness.CanTrain(worm.Creature).ShouldBeTrue();
        worm.Training.ShouldBeNull();
    }

    [Fact]
    public void Examples_HaveDistinctIds() =>
        CreationExamples.All.Select(example => example.Creation.Id).ShouldBeUnique();
}
