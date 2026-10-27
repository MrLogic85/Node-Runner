using NodeRunner.App.Repositories;
using NodeRunner.App.Services;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.Services;

public sealed class NewCreationWorkflowTests
{
    [Fact]
    public void Create_SavesAnEmptyCreationNamedUntitledInThePlayersLanguage_OnTheGridDefault()
    {
        var repository = new InMemoryCreationRepository();
        var id = Guid.NewGuid();
        UiText? asked = null;
        var defaultGrid = new BuildGridDef(25, snap: true);

        var created = new NewCreationWorkflow(repository, () => defaultGrid, () => id).Create(text =>
        {
            asked = text;
            return "Namnlös skapelse";
        });

        asked.ShouldBe(UiText.Plain("Untitled Creation"));
        created.Id.ShouldBe(id);
        created.Name.ShouldBe("Namnlös skapelse");
        created.Creature.Nodes.ShouldBeEmpty();
        created.Training.ShouldBeNull();
        created.Grid.ShouldBe(defaultGrid);
        repository.Get(id).ShouldBe(created);

        // The default is read for each creation, and a later change leaves a saved one as it is.
        defaultGrid = new BuildGridDef(100, snap: false);
        var second = new NewCreationWorkflow(repository, () => defaultGrid).Create(_ => "Second");
        second.Id.ShouldNotBe(id);
        second.Grid.ShouldBe(defaultGrid);
        repository.Get(id)!.Grid.ShouldBe(new BuildGridDef(25, snap: true));
    }
}
