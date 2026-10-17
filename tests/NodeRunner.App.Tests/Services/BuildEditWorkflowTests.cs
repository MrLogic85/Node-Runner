using NodeRunner.App.Repositories;
using NodeRunner.App.Services;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.Services;

public sealed class BuildEditWorkflowTests
{
    [Fact]
    public void PersistEdit_SavesTheEditKeepingTraining_AndReturnsNullForAMissingCreation()
    {
        var repository = new InMemoryCreationRepository();
        var original = new CreationDef(Guid.NewGuid(), "Original", Creature(x: 0), TestTraining.StateFor(Creature(x: 0), 6));
        repository.Save(original);
        var workflow = new BuildEditWorkflow(new CreationUpdateCoordinator(repository));
        var edited = Creature(x: 3);

        var result = workflow.PersistEdit(original.Id, edited);

        result.ShouldNotBeNull();
        result.Creature.ShouldBe(edited);
        result.Training!.Generation.ShouldBe(original.Training!.Generation);
        result.Training.Network.Connections.ShouldBe(original.Training.Network.Connections);
        repository.Get(original.Id)!.Creature.ShouldBe(edited);
        workflow.PersistEdit(Guid.NewGuid(), edited).ShouldBeNull();
        repository.List().Count.ShouldBe(1);
        Should.Throw<ArgumentException>(() => workflow.PersistEdit(Guid.Empty, edited));
    }

    private static CreatureDef Creature(double x) =>
        new(
            [new NodeDef(1, new Vector2D(x, 0)), new NodeDef(2, new Vector2D(x + 2, 0))],
            [new BeamDef(101, 1, 2)],
            [new SensorDef(201, 101, SensorKind.Accelerometer)],
            [new PistonDef(301, 1, 2)]);
}
