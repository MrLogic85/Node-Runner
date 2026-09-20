using NodeRunner.App.Repositories;
using NodeRunner.App.Services;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.Services;

public sealed class CreationDuplicateWorkflowTests
{
    [Fact]
    public void Duplicate_CopyTraining_CreatesNewCreationWithTraining()
    {
        var repository = new InMemoryCreationRepository();
        var source = CreateCreation("Walker", generation: 8);
        var copyId = Guid.NewGuid();
        repository.Save(source);
        var workflow = new CreationDuplicateWorkflow(repository, () => copyId);

        var copy = workflow.Duplicate(source.Id, CreationDuplicateMode.CopyTraining);

        copy.Id.ShouldBe(copyId);
        copy.Name.ShouldBe("Walker Copy");
        copy.Creature.ShouldBe(source.Creature);
        copy.Training.ShouldBe(source.Training);
        repository.Get(copyId).ShouldBe(copy);
    }

    [Fact]
    public void Duplicate_StartFresh_CreatesNewCreationWithoutTraining()
    {
        var repository = new InMemoryCreationRepository();
        var source = CreateCreation("Walker", generation: 8);
        var copyId = Guid.NewGuid();
        repository.Save(source);
        var workflow = new CreationDuplicateWorkflow(repository, () => copyId);

        var copy = workflow.Duplicate(source.Id, CreationDuplicateMode.StartFresh);

        copy.Id.ShouldBe(copyId);
        copy.Name.ShouldBe("Walker Copy");
        copy.Creature.ShouldBe(source.Creature);
        copy.Training.ShouldBeNull();
        repository.Get(copyId)!.Training.ShouldBeNull();
    }

    [Fact]
    public void Duplicate_MissingCreation_Throws()
    {
        var workflow = new CreationDuplicateWorkflow(new InMemoryCreationRepository());

        Should.Throw<KeyNotFoundException>(() => workflow.Duplicate(Guid.NewGuid(), CreationDuplicateMode.CopyTraining));
    }

    [Fact]
    public void Duplicate_InvalidMode_Throws()
    {
        var workflow = new CreationDuplicateWorkflow(new InMemoryCreationRepository());

        Should.Throw<ArgumentOutOfRangeException>(() => workflow.Duplicate(Guid.NewGuid(), (CreationDuplicateMode)999));
    }

    [Fact]
    public void Constructor_WithNullRepository_Throws()
    {
        Should.Throw<ArgumentNullException>(() => new CreationDuplicateWorkflow(null!));
    }

    private static CreationDef CreateCreation(string name, int generation) =>
        new(
            Guid.NewGuid(),
            name,
            new CreatureDef(
                [new NodeDef(new Vector2D(0, 0), 1), new NodeDef(new Vector2D(2, 0), 1)],
                [new BeamDef(0, 1)],
                [new CoreDef(0)]),
            new TrainingStateDef([2, 1], [0.1, -0.2, 0.3], generation, "Tanh"));
}
