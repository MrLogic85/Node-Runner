using NodeRunner.App.Repositories;
using NodeRunner.App.Services;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.Services;

public sealed class ConstructionEditWorkflowTests
{
    [Fact]
    public void PersistMoveOnlyEdit_WhenCreationExists_PersistsBeforeAllowingLiveApply()
    {
        var repository = new InMemoryCreationRepository();
        var coordinator = new CreationUpdateCoordinator(repository);
        var original = CreateCreation("Original", x: 0, generation: 6);
        var editedCreature = CreateCreature(x: 3);
        repository.Save(original);
        var workflow = new ConstructionEditWorkflow(coordinator);

        var result = workflow.PersistMoveOnlyEdit(original.Id, editedCreature);

        result.ShouldApplyLive.ShouldBeTrue();
        result.UpdatedCreation.ShouldNotBeNull();
        result.UpdatedCreation.Creature.ShouldBe(editedCreature);
        result.UpdatedCreation.Training.ShouldBe(original.Training);
        result.StatusMessage.ShouldBe("Saved edits to Original.");
        repository.Get(original.Id)!.Creature.ShouldBe(editedCreature);
    }

    [Fact]
    public void PersistMoveOnlyEdit_WhenCreationIsMissing_DiscardsLiveEdit()
    {
        var repository = new InMemoryCreationRepository();
        var coordinator = new CreationUpdateCoordinator(repository);
        var workflow = new ConstructionEditWorkflow(coordinator);

        var result = workflow.PersistMoveOnlyEdit(Guid.NewGuid(), CreateCreature(x: 3));

        result.ShouldApplyLive.ShouldBeFalse();
        result.UpdatedCreation.ShouldBeNull();
        result.StatusMessage.ShouldBe("Could not save the edited creature; your edit was discarded.");
        repository.List().ShouldBeEmpty();
    }

    [Fact]
    public void PersistMoveOnlyEdit_WhenCoordinatorThrows_DoesNotReturnSuccessShapedFallback()
    {
        var coordinator = Substitute.For<ICreationUpdateCoordinator>();
        var id = Guid.NewGuid();
        var editedCreature = CreateCreature(x: 3);
        coordinator.ApplyCreatureEdit(id, editedCreature).Returns(_ => throw new IOException("disk full"));
        var workflow = new ConstructionEditWorkflow(coordinator);

        Should.Throw<IOException>(() => workflow.PersistMoveOnlyEdit(id, editedCreature));
    }

    [Fact]
    public void Constructor_WithNullCoordinator_Throws()
    {
        Should.Throw<ArgumentNullException>(() => new ConstructionEditWorkflow(null!));
    }

    [Fact]
    public void PersistMoveOnlyEdit_WithNullCreature_Throws()
    {
        var workflow = new ConstructionEditWorkflow(Substitute.For<ICreationUpdateCoordinator>());

        Should.Throw<ArgumentNullException>(() => workflow.PersistMoveOnlyEdit(Guid.NewGuid(), null!));
    }

    [Fact]
    public void PersistMoveOnlyEdit_WithEmptyCreationId_Throws()
    {
        var workflow = new ConstructionEditWorkflow(Substitute.For<ICreationUpdateCoordinator>());

        Should.Throw<ArgumentException>(() => workflow.PersistMoveOnlyEdit(Guid.Empty, CreateCreature(x: 3)));
    }

    private static CreationDef CreateCreation(string name, double x, int generation)
    {
        return new CreationDef(
            Guid.NewGuid(),
            name,
            CreateCreature(x),
            new TrainingStateDef([2, 1], [0.1, -0.2, 0.3], generation, "Tanh"));
    }

    private static CreatureDef CreateCreature(double x)
    {
        return new CreatureDef(
            [new NodeDef(new Vector2D(x, 0), 1), new NodeDef(new Vector2D(x + 2, 0), 1)],
            [new BeamDef(0, 1)],
            [new CoreDef(0)]);
    }
}
