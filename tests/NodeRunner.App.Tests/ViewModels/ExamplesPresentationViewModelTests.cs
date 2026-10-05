using NodeRunner.App.Services;
using NodeRunner.App.ViewModels;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class ExamplesPresentationViewModelTests
{
    [Fact]
    public void Cards_ShowEachExampleWithWhatIsNewAndCopyOnly()
    {
        var example = CreateExample();
        var viewModel = new ExamplesPresentationViewModel([example]);

        var card = viewModel.Cards.ShouldHaveSingleItem();
        card.Id.ShouldBe(example.Id);
        card.Name.ShouldBe(example.Name);
        card.Creature.ShouldBe(example.Creature);
        card.SummaryText.ShouldBe(example.WhatIsNew);
        card.Training.ShouldBeNull();
        card.CanOpen.ShouldBeFalse();
        card.CanDuplicate.ShouldBeTrue();
        card.CanDelete.ShouldBeFalse();
    }

    [Fact]
    public void Cards_ByDefault_ShowTheShippedExamples() =>
        new ExamplesPresentationViewModel().Cards.Select(card => card.Id)
            .ShouldBe(CreationExamples.All.Select(example => example.Id));

    [Fact]
    public void RequestCopy_ReturnsTheExampleOnlyWhenItIsShown()
    {
        var example = CreateExample();
        var viewModel = new ExamplesPresentationViewModel([example]);

        viewModel.RequestCopy(example.Id).ShouldBe(example.Id);
        viewModel.RequestCopy(Guid.NewGuid()).ShouldBeNull();
    }

    private static CreationExample CreateExample() =>
        new(
            Guid.NewGuid(),
            UiText.Plain("Walker"),
            UiText.Plain("Servos in the knees: the basic walk."),
            CreationExamples.CreateWalkerCreature());
}
