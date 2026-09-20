using NodeRunner.App.Builders;
using NodeRunner.App.Repositories;
using NodeRunner.App.Services;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.Services;

public sealed class DefaultCreationSeederTests
{
    [Fact]
    public void SeedIfNeeded_WithEmptyInstall_SavesStarterCreationAndMarker()
    {
        var creations = new InMemoryCreationRepository();
        var progression = new InMemoryProgressionRepository();
        var seeder = new DefaultCreationSeeder(creations, progression);

        var seeded = seeder.SeedIfNeeded();

        seeded.ShouldBeTrue();
        var creation = creations.Get(DefaultCreationTemplates.StarterWormId);
        creation.ShouldNotBeNull();
        creation.Name.ShouldBe("Example: Worm");
        creation.Training.ShouldBeNull();
        progression.Load().DefaultCreationsSeeded.ShouldBeTrue();
    }

    [Fact]
    public void SeedIfNeeded_WhenRunTwice_DoesNotDuplicateStarterCreation()
    {
        var creations = new InMemoryCreationRepository();
        var progression = new InMemoryProgressionRepository();
        var seeder = new DefaultCreationSeeder(creations, progression);

        seeder.SeedIfNeeded();
        var seededAgain = seeder.SeedIfNeeded();

        seededAgain.ShouldBeFalse();
        creations.List().Count.ShouldBe(1);
        creations.List()[0].Id.ShouldBe(DefaultCreationTemplates.StarterWormId);
    }

    [Fact]
    public void SeedIfNeeded_AfterStarterDeleted_DoesNotRestoreIt()
    {
        var creations = new InMemoryCreationRepository();
        var progression = new InMemoryProgressionRepository();
        var seeder = new DefaultCreationSeeder(creations, progression);
        seeder.SeedIfNeeded();
        creations.Delete(DefaultCreationTemplates.StarterWormId);

        var seededAgain = seeder.SeedIfNeeded();

        seededAgain.ShouldBeFalse();
        creations.List().ShouldBeEmpty();
    }

    [Fact]
    public void SeedIfNeeded_AfterStarterEdited_DoesNotOverwriteIt()
    {
        var creations = new InMemoryCreationRepository();
        var progression = new InMemoryProgressionRepository();
        var seeder = new DefaultCreationSeeder(creations, progression);
        seeder.SeedIfNeeded();
        var edited = new CreationDef(
            DefaultCreationTemplates.StarterWormId,
            "My Worm",
            DefaultCreationTemplates.CreateStarterWormCreature());
        creations.Save(edited);

        seeder.SeedIfNeeded();

        creations.Get(DefaultCreationTemplates.StarterWormId)!.Name.ShouldBe("My Worm");
    }

    [Fact]
    public void SeedIfNeeded_WithExistingCreations_TreatsInstallAsAlreadySeeded()
    {
        var creations = new InMemoryCreationRepository();
        var progression = new InMemoryProgressionRepository();
        var existing = new CreationDef(Guid.NewGuid(), "Player Build", DefaultCreationTemplates.CreateStarterWormCreature());
        creations.Save(existing);
        var seeder = new DefaultCreationSeeder(creations, progression);

        var seeded = seeder.SeedIfNeeded();

        seeded.ShouldBeFalse();
        creations.List().ShouldBe([existing]);
        progression.Load().DefaultCreationsSeeded.ShouldBeTrue();
    }

    [Fact]
    public void SeedIfNeeded_WithMissingMarkerAndExistingStarter_DoesNotOverwriteIt()
    {
        var creations = new InMemoryCreationRepository();
        var progression = new InMemoryProgressionRepository();
        var edited = new CreationDef(
            DefaultCreationTemplates.StarterWormId,
            "Renamed Starter",
            DefaultCreationTemplates.CreateStarterWormCreature());
        creations.Save(edited);
        var seeder = new DefaultCreationSeeder(creations, progression);

        seeder.SeedIfNeeded();

        creations.Get(DefaultCreationTemplates.StarterWormId)!.Name.ShouldBe("Renamed Starter");
        progression.Load().DefaultCreationsSeeded.ShouldBeTrue();
    }

    [Fact]
    public void StarterWorm_UsesOnlyStartingComponentsAndPassesConstructionValidation()
    {
        var creation = DefaultCreationTemplates.CreateStarterWorm();
        var builder = new CreatureBuilder(creation.Creature);

        var canBuild = builder.TryBuild(out var built, out var errors);

        canBuild.ShouldBeTrue();
        errors.ShouldBeEmpty();
        built.ShouldNotBeNull();
        built.Cores.Count.ShouldBeLessThanOrEqualTo(1);
        creation.Training.ShouldBeNull();
    }

    [Fact]
    public void Constructor_RequiresDependencies()
    {
        var creations = new InMemoryCreationRepository();
        var progression = new InMemoryProgressionRepository();

        Should.Throw<ArgumentNullException>(() => new DefaultCreationSeeder(null!, progression));
        Should.Throw<ArgumentNullException>(() => new DefaultCreationSeeder(creations, null!));
    }
}
