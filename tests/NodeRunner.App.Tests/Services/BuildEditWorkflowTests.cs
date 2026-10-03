using NodeRunner.App.Repositories;
using NodeRunner.App.Services;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.Services;

public sealed class BuildEditWorkflowTests
{
    [Fact]
    public void PersistEdit_WhenCreationExists_SavesAndReturnsIt()
    {
        var repository = new InMemoryCreationRepository();
        var coordinator = new CreationUpdateCoordinator(repository);
        var original = CreateCreation("Original", x: 0, generation: 6);
        var editedCreature = CreateCreature(x: 3);
        repository.Save(original);
        var workflow = new BuildEditWorkflow(coordinator);

        var result = workflow.PersistEdit(original.Id, editedCreature);

        result.ShouldNotBeNull();
        result.Creature.ShouldBe(editedCreature);
        result.Training.ShouldNotBeNull();
        result.Training.Generation.ShouldBe(original.Training!.Generation);
        result.Training.Brain.Neurons.ShouldBe(original.Training.Brain.Neurons);
        result.Training.Brain.Connections.ShouldBe(original.Training.Brain.Connections);
        repository.Get(original.Id)!.Creature.ShouldBe(editedCreature);
    }

    [Fact]
    public void PersistEdit_WhenCreationIsMissing_ReturnsNull()
    {
        var repository = new InMemoryCreationRepository();
        var coordinator = new CreationUpdateCoordinator(repository);
        var workflow = new BuildEditWorkflow(coordinator);

        var result = workflow.PersistEdit(Guid.NewGuid(), CreateCreature(x: 3));

        result.ShouldBeNull();
        repository.List().ShouldBeEmpty();
    }

    [Fact]
    public void PersistEdit_WhenCoordinatorThrows_DoesNotReturnSuccessShapedFallback()
    {
        var coordinator = Substitute.For<ICreationUpdateCoordinator>();
        var id = Guid.NewGuid();
        var editedCreature = CreateCreature(x: 3);
        coordinator.ApplyEdit(id, editedCreature).Returns(_ => throw new IOException("disk full"));
        var workflow = new BuildEditWorkflow(coordinator);

        Should.Throw<IOException>(() => workflow.PersistEdit(id, editedCreature));
    }

    [Fact]
    public void Constructor_WithNullCoordinator_Throws()
    {
        Should.Throw<ArgumentNullException>(() => new BuildEditWorkflow(null!));
    }

    [Fact]
    public void PersistEdit_WithNullCreature_Throws()
    {
        var workflow = new BuildEditWorkflow(Substitute.For<ICreationUpdateCoordinator>());

        Should.Throw<ArgumentNullException>(() => workflow.PersistEdit(Guid.NewGuid(), null!));
    }

    [Fact]
    public void PersistEdit_WithEmptyCreationId_Throws()
    {
        var workflow = new BuildEditWorkflow(Substitute.For<ICreationUpdateCoordinator>());

        Should.Throw<ArgumentException>(() => workflow.PersistEdit(Guid.Empty, CreateCreature(x: 3)));
    }

    private static CreationDef CreateCreation(string name, double x, int generation)
    {
        return new CreationDef(
            Guid.NewGuid(),
            name,
            CreateCreature(x),
            TestTraining.StateFor(CreateCreature(x), generation));
    }

    private static CreatureDef CreateCreature(double x)
    {
        return new CreatureDef(
            [new NodeDef(1, new Vector2D(x, 0)), new NodeDef(2, new Vector2D(x + 2, 0))],
            [new BeamDef(101, 1, 2)],
            [new SensorDef(201, 101, SensorKind.Accelerometer)],
            [new PistonDef(301, 1, 2)]);
    }
}
