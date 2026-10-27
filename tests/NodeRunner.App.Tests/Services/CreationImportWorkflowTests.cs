using NodeRunner.App.Repositories;
using NodeRunner.App.Services;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.Services;

/// <summary>Adding a shared build to Creations.</summary>
public sealed class CreationImportWorkflowTests
{
    [Fact]
    public void Import_SavesTheBuildUntrainedUnderANewId_WithTheGivenNameTrimmedAndCut_OnTheGridDefault()
    {
        var repository = new InMemoryCreationRepository();
        var build = new CreationDef(Guid.NewGuid(), "Walker", CreationExamples.Walker.Creature) { Grid = new BuildGridDef(100, snap: true) };
        var newId = Guid.NewGuid();
        var defaultGrid = new BuildGridDef(25, snap: false);
        Should.Throw<ArgumentException>(() => new CreationImportWorkflow(repository, () => defaultGrid).Import(build, "   "));

        var creation = new CreationImportWorkflow(repository, () => defaultGrid, () => newId).Import(build, "  Strider ");
        var again = new CreationImportWorkflow(repository, () => defaultGrid).Import(build, new string('W', 100));

        creation.ShouldBe(new CreationDef(newId, "Strider", build.Creature) { Grid = defaultGrid });
        again.Name.ShouldBe(new string('W', NameLimits.Creation));
        repository.List().Select(saved => saved.Id).ShouldBe([newId, again.Id], ignoreOrder: true);
    }
}
