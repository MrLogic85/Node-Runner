using NodeRunner.App.Repositories;
using NodeRunner.App.Services;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.Services;

public sealed class ExampleCopyWorkflowTests
{
    [Fact]
    public void Copy_SavesAnIdenticalCreationUnderANewId()
    {
        var repository = new InMemoryCreationRepository();
        var example = CreateExample();
        var copyId = Guid.NewGuid();
        var workflow = new ExampleCopyWorkflow(repository, [example], () => copyId);

        var copy = workflow.Copy(example.Creation.Id);

        copy.Id.ShouldBe(copyId);
        copy.Name.ShouldBe("Walker");
        copy.Creature.ShouldBe(example.Creation.Creature);
        copy.BrainShape.ShouldBe(example.Creation.BrainShape);
        copy.Training.ShouldBe(example.Creation.Training);
        repository.Get(copyId).ShouldBe(copy);
    }

    [Fact]
    public void Copy_Twice_LeavesTheExampleAndGivesTwoCreations()
    {
        var repository = new InMemoryCreationRepository();
        var workflow = new ExampleCopyWorkflow(repository);

        workflow.Copy(CreationExamples.WormId);
        workflow.Copy(CreationExamples.WormId);

        repository.List().Count.ShouldBe(2);
        repository.List().ShouldAllBe(creation => creation.Id != CreationExamples.WormId);
    }

    [Fact]
    public void Copy_UnknownExample_Throws()
    {
        var workflow = new ExampleCopyWorkflow(new InMemoryCreationRepository());

        Should.Throw<KeyNotFoundException>(() => workflow.Copy(Guid.NewGuid()));
    }

    [Fact]
    public void Constructor_WithNullRepository_Throws()
    {
        Should.Throw<ArgumentNullException>(() => new ExampleCopyWorkflow(null!));
    }

    private static CreationExample CreateExample()
    {
        var creature = new CreatureDef(
            [new NodeDef(new Vector2D(0, 0), 1), new NodeDef(new Vector2D(2, 0), 1)],
            [new BeamDef(0, 1)],
            [new CoreDef(0)]);
        var training = new TrainingStateDef([2, 1], [0.1, -0.2, 0.3], 8, "Tanh");
        var creation = new CreationDef(Guid.NewGuid(), "Walker", creature, new BrainShapeDef(2, 6), training);
        return new CreationExample(creation, "Servos in the knees: the basic walk.");
    }
}
