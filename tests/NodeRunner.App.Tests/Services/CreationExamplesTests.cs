using NodeRunner.App.Builders;
using NodeRunner.App.Lifecycle;
using NodeRunner.App.Services;
using NodeRunner.App.ViewModels;

namespace NodeRunner.App.Tests.Services;

public sealed class CreationExamplesTests
{
    [Fact]
    public void Walker_IsTheOnlyExample_UntrainedAndBuildableInsideTheBuildArea()
    {
        var walker = CreationExamples.All.ShouldHaveSingleItem();

        walker.ShouldBeSameAs(CreationExamples.Walker);
        walker.Id.ShouldBe(CreationExamples.WalkerId);
        walker.Name.ShouldBe(UiText.Plain("Walker"));
        walker.WhatIsNew.Message.ShouldNotBeNullOrWhiteSpace();
        new CreatureBuilder(walker.Creature).TryBuild(out _, out var errors).ShouldBeTrue();
        errors.ShouldBeEmpty();
        walker.Creature.Nodes.ShouldAllBe(node => BuildViewModel.BuildArea.Contains(node.Position));
        walker.Creature.Pistons.Count.ShouldBe(2);
        CreatureReadiness.CanTrain(walker.Creature).ShouldBeTrue();
        walker.Training.ShouldBeNull();
    }
}
