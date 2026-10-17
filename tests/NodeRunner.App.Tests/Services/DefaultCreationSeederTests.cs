using NodeRunner.App.Repositories;
using NodeRunner.App.Services;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.Services;

public sealed class DefaultCreationSeederTests
{
    // The marker, not the list of creations, says whether this is the first start.
    [Fact]
    public void SeedIfNeeded_SeedsWalkerInThePlayersLanguageOnFirstStartOnly_EvenBesideACreation()
    {
        var creations = new InMemoryCreationRepository();
        creations.Save(new CreationDef(Guid.NewGuid(), "Player Build", CreationExamples.CreateWalkerCreature()));
        var progression = new InMemoryProgressionRepository();
        var seeder = new DefaultCreationSeeder(new ExampleCopyWorkflow(creations), progression);

        var seeded = seeder.SeedIfNeeded(text => text.Equals(UiText.Plain("Walker")) ? "Gångare" : "?");

        seeded.ShouldBeTrue();
        var walker = creations.List().Single(creation => creation.Name == "Gångare");
        walker.Id.ShouldNotBe(CreationExamples.WalkerId);
        progression.Load().DefaultCreationsSeeded.ShouldBeTrue();
        creations.Delete(walker.Id);
        seeder.SeedIfNeeded(TestLanguage.Untranslated).ShouldBeFalse();
        creations.List().Count.ShouldBe(1);
    }
}
