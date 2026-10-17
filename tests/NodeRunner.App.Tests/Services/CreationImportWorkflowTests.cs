using NodeRunner.App.Repositories;
using NodeRunner.App.Services;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.Services;

/// <summary>Adding a shared build to Creations.</summary>
public sealed class CreationImportWorkflowTests
{
    [Fact]
    public void Import_SavesTheBuildUntrainedUnderANewId_WithTheGivenNameTrimmedAndCut()
    {
        var repository = new InMemoryCreationRepository();
        var build = new CreationDef(Guid.NewGuid(), "Walker", CreationExamples.Walker.Creature);
        var newId = Guid.NewGuid();
        Should.Throw<ArgumentException>(() => new CreationImportWorkflow(repository).Import(build, "   "));

        var creation = new CreationImportWorkflow(repository, () => newId).Import(build, "  Strider ");
        var again = new CreationImportWorkflow(repository).Import(build, new string('W', 100));

        creation.ShouldBe(new CreationDef(newId, "Strider", build.Creature));
        again.Name.ShouldBe(new string('W', NameLimits.Creation));
        repository.List().Select(saved => saved.Id).ShouldBe([newId, again.Id], ignoreOrder: true);
    }
}
