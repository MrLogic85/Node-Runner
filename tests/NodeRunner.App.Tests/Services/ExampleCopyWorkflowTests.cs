using NodeRunner.App.Repositories;
using NodeRunner.App.Services;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.Services;

public sealed class ExampleCopyWorkflowTests
{
    [Fact]
    public void Copy_SavesAnIdenticalCreationUnderANewId_AndTheNameInThePlayersLanguage()
    {
        var repository = new InMemoryCreationRepository();
        var example = CreateExample();
        var copyId = Guid.NewGuid();
        var workflow = new ExampleCopyWorkflow(repository, [example], () => copyId);
        UiText? asked = null;

        var copy = workflow.Copy(example.Id, text =>
        {
            asked = text;
            return "Gångare";
        });

        asked.ShouldBe(example.Name);
        copy.Id.ShouldBe(copyId);
        copy.Name.ShouldBe("Gångare");
        copy.Creature.ShouldBe(example.Creature);
        copy.Creature.NextPartId.ShouldBe(example.Creature.NextPartId);
        copy.Training.ShouldBe(example.Training);
        repository.Get(copyId).ShouldBe(copy);
    }

    [Fact]
    public void Copy_Twice_LeavesTheExampleAndGivesTwoCreations()
    {
        var repository = new InMemoryCreationRepository();
        var workflow = new ExampleCopyWorkflow(repository);

        workflow.Copy(CreationExamples.WormId, TestLanguage.Untranslated);
        workflow.Copy(CreationExamples.WormId, TestLanguage.Untranslated);

        repository.List().Count.ShouldBe(2);
        repository.List().ShouldAllBe(creation => creation.Id != CreationExamples.WormId);
    }

    [Fact]
    public void Copy_UnknownExample_Throws()
    {
        var workflow = new ExampleCopyWorkflow(new InMemoryCreationRepository());

        Should.Throw<KeyNotFoundException>(() => workflow.Copy(Guid.NewGuid(), TestLanguage.Untranslated));
    }

    [Fact]
    public void Constructor_WithNullRepository_Throws()
    {
        Should.Throw<ArgumentNullException>(() => new ExampleCopyWorkflow(null!));
    }

    private static CreationExample CreateExample()
    {
        var creature = new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(2, 0))],
            [new BeamDef(101, 1, 2)],
            [new SensorDef(201, 101, SensorKind.Accelerometer)]);
        return new CreationExample(
            Guid.NewGuid(),
            UiText.Plain("Walker"),
            UiText.Plain("Servos in the knees: the basic walk."),
            creature,
            TestTraining.State(8, 1, TestTraining.Run));
    }
}
