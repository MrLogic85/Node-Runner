using NodeRunner.App.Repositories;
using NodeRunner.App.Services;
using NodeRunner.App.ViewModels;

namespace NodeRunner.App.Tests.Services;

public sealed class NewCreationWorkflowTests
{
    [Fact]
    public void Create_SavesAnEmptyCreationNamedUntitledInThePlayersLanguage()
    {
        var repository = new InMemoryCreationRepository();
        var id = Guid.NewGuid();
        UiText? asked = null;

        var created = new NewCreationWorkflow(repository, () => id).Create(text =>
        {
            asked = text;
            return "Namnlös skapelse";
        });

        asked.ShouldBe(UiText.Plain("Untitled Creation"));
        created.Id.ShouldBe(id);
        created.Name.ShouldBe("Namnlös skapelse");
        created.Creature.Nodes.ShouldBeEmpty();
        created.Training.ShouldBeNull();
        repository.Get(id).ShouldBe(created);
        new NewCreationWorkflow(repository).Create(_ => "Second").Id.ShouldNotBe(id);
    }
}
