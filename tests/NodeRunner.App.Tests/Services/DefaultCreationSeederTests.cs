using NodeRunner.App.Repositories;
using NodeRunner.App.Services;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.Services;

public sealed class DefaultCreationSeederTests
{
    [Fact]
    public void SeedIfNeeded_OnFirstStart_SavesACopyOfWormInThePlayersLanguageAndTheMarker()
    {
        var creations = new InMemoryCreationRepository();
        var progression = new InMemoryProgressionRepository();

        var seeded = CreateSeeder(creations, progression).SeedIfNeeded(text => text.Equals(UiText.Plain("Worm")) ? "Mask" : "?");

        seeded.ShouldBeTrue();
        var worm = creations.List().ShouldHaveSingleItem();
        worm.Id.ShouldNotBe(CreationExamples.WormId);
        worm.Name.ShouldBe("Mask");
        progression.Load().DefaultCreationsSeeded.ShouldBeTrue();
    }

    [Fact]
    public void SeedIfNeeded_OnFirstStartWithCreations_StillSeeds()
    {
        var creations = new InMemoryCreationRepository();
        creations.Save(new CreationDef(Guid.NewGuid(), "Player Build", CreationExamples.CreateWormCreature()));

        var seeded = CreateSeeder(creations, new InMemoryProgressionRepository()).SeedIfNeeded(TestLanguage.English);

        seeded.ShouldBeTrue();
        creations.List().Count.ShouldBe(2);
    }

    [Fact]
    public void SeedIfNeeded_AfterFirstStart_DoesNothing()
    {
        var creations = new InMemoryCreationRepository();
        var progression = new InMemoryProgressionRepository();
        var seeder = CreateSeeder(creations, progression);
        seeder.SeedIfNeeded(TestLanguage.English);
        creations.Delete(creations.List().Single().Id);

        var seededAgain = seeder.SeedIfNeeded(TestLanguage.English);

        seededAgain.ShouldBeFalse();
        creations.List().ShouldBeEmpty();
    }

    [Fact]
    public void Constructor_RequiresDependencies()
    {
        var examples = new ExampleCopyWorkflow(new InMemoryCreationRepository());
        var progression = new InMemoryProgressionRepository();

        Should.Throw<ArgumentNullException>(() => new DefaultCreationSeeder(null!, progression));
        Should.Throw<ArgumentNullException>(() => new DefaultCreationSeeder(examples, null!));
    }

    private static DefaultCreationSeeder CreateSeeder(ICreationRepository creations, IProgressionRepository progression) =>
        new(new ExampleCopyWorkflow(creations), progression);
}
