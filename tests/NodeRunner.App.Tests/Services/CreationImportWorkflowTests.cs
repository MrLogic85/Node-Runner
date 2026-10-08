using NodeRunner.App.Repositories;
using NodeRunner.App.Services;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.Services;

/// <summary>Adding a shared build to Creations (#899).</summary>
public sealed class CreationImportWorkflowTests
{
    [Fact]
    public void Import_SavesTheBuildUnderANewId_Untrained_WithTheGivenName()
    {
        var repository = new InMemoryCreationRepository();
        var build = Walker("Walker");
        var newId = Guid.NewGuid();

        var creation = new CreationImportWorkflow(repository, () => newId).Import(build, "  Strider ");

        creation.Id.ShouldBe(newId);
        creation.Name.ShouldBe("Strider");
        creation.Creature.ShouldBe(build.Creature);
        creation.Training.ShouldBeNull();
        creation.TrainSettings.ShouldBeNull();
        repository.Get(newId).ShouldBe(creation);
    }

    [Fact]
    public void Import_TheSameBuildTwice_GivesTwoCreations_WithTheSameName()
    {
        var repository = new InMemoryCreationRepository();
        var workflow = new CreationImportWorkflow(repository);

        workflow.Import(Walker("Walker"), "Walker");
        workflow.Import(Walker("Walker"), "Walker");

        repository.List().Select(creation => creation.Name).ShouldBe(["Walker", "Walker"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Import_ABlankName_IsRefused(string name)
    {
        var repository = new InMemoryCreationRepository();

        Should.Throw<ArgumentException>(() => new CreationImportWorkflow(repository).Import(Walker("Walker"), name));
        repository.List().ShouldBeEmpty();
    }

    [Fact]
    public void Import_ANameLongerThanTheLimit_IsCut()
    {
        var repository = new InMemoryCreationRepository();

        var creation = new CreationImportWorkflow(repository).Import(Walker("Walker"), new string('W', 100));

        creation.Name.ShouldBe(new string('W', NameLimits.Creation));
    }

    private static CreationDef Walker(string name) => new(Guid.NewGuid(), name, CreationExamples.Walker.Creature);
}
