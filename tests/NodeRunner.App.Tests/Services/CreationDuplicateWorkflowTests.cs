using NodeRunner.App.Repositories;
using NodeRunner.App.Services;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.Services;

public sealed class CreationDuplicateWorkflowTests
{
    [Fact]
    public void Duplicate_CreatesNewCreationWithTraining()
    {
        var repository = new InMemoryCreationRepository();
        var source = CreateCreation("Walker", generation: 8);
        var copyId = Guid.NewGuid();
        repository.Save(source);
        var workflow = new CreationDuplicateWorkflow(repository, () => copyId);

        var copy = workflow.Duplicate(source.Id);

        copy.Id.ShouldBe(copyId);
        copy.Name.ShouldBe("Copy of Walker");
        copy.Creature.ShouldBe(source.Creature);
        copy.Creature.NextPartId.ShouldBe(source.Creature.NextPartId);
        copy.Training.ShouldBe(source.Training);
        repository.Get(copyId).ShouldBe(copy);
    }

    [Fact]
    public void Duplicate_MissingCreation_Throws()
    {
        var workflow = new CreationDuplicateWorkflow(new InMemoryCreationRepository());

        Should.Throw<KeyNotFoundException>(() => workflow.Duplicate(Guid.NewGuid()));
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
                [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(2, 0))],
                [new BeamDef(101, 1, 2)],
                [new SensorDef(201, 101, SensorKind.Accelerometer)]),
            TestTraining.State(generation, 1, TestTraining.Run));
}
