using NodeRunner.App.Services;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class ExamplesPresentationViewModelTests
{
    [Fact]
    public void Cards_ShowEachExampleWithWhatIsNewAndCopyOnly()
    {
        var example = CreateExample();
        var viewModel = new ExamplesPresentationViewModel([example]);

        var card = viewModel.Cards.ShouldHaveSingleItem();
        card.Id.ShouldBe(example.Creation.Id);
        card.Name.ShouldBe("Walker");
        card.Creature.ShouldBe(example.Creation.Creature);
        card.SummaryText.ShouldBe(example.WhatIsNew);
        card.AchievementProgress.ShouldBe(0f);
        card.UnlockCreditText.ShouldBeEmpty();
        card.CanOpen.ShouldBeFalse();
        card.CanDuplicate.ShouldBeTrue();
        card.CanDelete.ShouldBeFalse();
    }

    [Fact]
    public void Cards_ByDefault_ShowTheShippedExamples() =>
        new ExamplesPresentationViewModel().Cards.Select(card => card.Id)
            .ShouldBe(CreationExamples.All.Select(example => example.Creation.Id));

    [Fact]
    public void RequestCopy_ReturnsTheExampleOnlyWhenItIsShown()
    {
        var example = CreateExample();
        var viewModel = new ExamplesPresentationViewModel([example]);

        viewModel.RequestCopy(example.Creation.Id).ShouldBe(example.Creation.Id);
        viewModel.RequestCopy(Guid.NewGuid()).ShouldBeNull();
    }

    private static CreationExample CreateExample() =>
        new(
            new CreationDef(Guid.NewGuid(), "Walker", CreationExamples.CreateWormCreature()),
            "Servos in the knees: the basic walk.");
}
