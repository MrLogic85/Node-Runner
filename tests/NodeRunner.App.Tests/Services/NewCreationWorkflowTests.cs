using NodeRunner.App.Repositories;
using NodeRunner.App.Services;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.Services;

public sealed class NewCreationWorkflowTests
{
    [Fact]
    public void Create_SavesAnEmptyUntitledCreationWithTheDefaultBrain()
    {
        var repository = new InMemoryCreationRepository();
        var id = Guid.NewGuid();

        var created = new NewCreationWorkflow(repository, () => id).Create();

        created.Id.ShouldBe(id);
        created.Name.ShouldBe("Untitled Creation");
        created.Creature.Nodes.ShouldBeEmpty();
        created.BrainShape.ShouldBe(BrainShapeDef.Default);
        created.Training.ShouldBeNull();
        repository.Get(id).ShouldBe(created);
    }

    [Fact]
    public void Create_EachTimeMakesAnotherCreation()
    {
        var repository = new InMemoryCreationRepository();
        var workflow = new NewCreationWorkflow(repository);

        workflow.Create();
        workflow.Create();

        repository.List().Count.ShouldBe(2);
    }
}
