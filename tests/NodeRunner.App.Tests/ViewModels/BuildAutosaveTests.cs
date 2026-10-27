using NodeRunner.App.Repositories;
using NodeRunner.App.Services;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class BuildAutosaveTests
{
    [Fact]
    public void Save_WritesOnlyAfterAnEdit_AndAnUnlockAloneIsNoEdit()
    {
        var edits = Substitute.For<IBuildEditWorkflow>();
        var build = new BuildViewModel();
        var drawing = TwoNodeCreation();
        build.LoadCreation(new CreationDef(drawing.Id, drawing.Name, drawing.Creature, TestTraining.State(3)));
        using var autosave = new BuildAutosave(build, edits, drawing.Id, openedAsNew: false);
        var changes = 0;
        autosave.Changed += (_, _) => changes++;

        autosave.Save().ShouldBeTrue();
        build.Unlock();
        autosave.Save().ShouldBeTrue();
        autosave.HasUnsavedEdits.ShouldBeFalse();
        edits.DidNotReceiveWithAnyArgs().PersistEdit(default, default!);

        build.MoveNode(1, new Vector2D(5, 5));
        changes.ShouldBe(1);
        autosave.HasUnsavedEdits.ShouldBeTrue();

        edits.PersistEdit(default, default!).ReturnsForAnyArgs(_ => throw new IOException("disk full"));
        Should.Throw<IOException>(() => autosave.Save());
        autosave.HasUnsavedEdits.ShouldBeTrue();
    }

    [Fact]
    public void Save_WritesTheEdit_KeepsTheTraining_AndFailsWhenTheCreationIsGone()
    {
        var pair = new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(20, 0))], [], [], [new PistonDef(3, 1, 2)]);
        var training = TestTraining.StateFor(pair, 4);
        var trained = new CreationDef(Guid.NewGuid(), "Pair", pair, training);
        var (repository, build, autosave) = Open(trained);

        build.MoveNode(1, new Vector2D(5, 5));
        autosave.Save().ShouldBeTrue();

        autosave.HasUnsavedEdits.ShouldBeFalse();
        var saved = repository.Get(trained.Id).ShouldNotBeNull();
        saved.Creature.Nodes[0].Position.ShouldBe(new Vector2D(5, 5));
        saved.Training.ShouldNotBeNull().Generation.ShouldBe(training.Generation);
        saved.Training.Best.ShouldBe(training.Best);
        saved.Training.Network.Neurons.ShouldBe(training.Network.Neurons);
        saved.Training.Network.Connections.ShouldBe(training.Network.Connections);

        repository.Delete(trained.Id);
        build.MoveNode(1, new Vector2D(6, 6));
        autosave.Save().ShouldBeFalse();
        autosave.HasUnsavedEdits.ShouldBeTrue();
    }

    // A grid change saves the grid alone: the creature and its training stay as they were saved.
    [Fact]
    public void Save_WritesAGridChange_WithoutTheCreature_AndFailsWhenTheCreationIsGone()
    {
        var pair = new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(20, 0))], [], [], [new PistonDef(3, 1, 2)]);
        var trained = new CreationDef(Guid.NewGuid(), "Pair", pair, TestTraining.StateFor(pair, 4));
        var (repository, build, autosave) = Open(trained);
        var changes = 0;
        autosave.Changed += (_, _) => changes++;

        build.UseGrid(new BuildGridDef(25, snap: true));
        changes.ShouldBe(1);
        autosave.HasUnsavedEdits.ShouldBeTrue();
        autosave.Save().ShouldBeTrue();

        autosave.HasUnsavedEdits.ShouldBeFalse();
        repository.Get(trained.Id).ShouldBe(trained with { Grid = new BuildGridDef(25, snap: true) });

        repository.Delete(trained.Id);
        build.UseGrid(new BuildGridDef(100, snap: false));
        autosave.Save().ShouldBeFalse();
        autosave.HasUnsavedEdits.ShouldBeTrue();
    }

    [Fact]
    public void Save_WritesAnEditAndAGridChange_BothTogether()
    {
        var (repository, build, autosave) = Open(TwoNodeCreation());

        build.MoveNode(1, new Vector2D(5, 5));
        build.UseGrid(new BuildGridDef(100, snap: true));
        autosave.Save().ShouldBeTrue();

        var saved = repository.Get(autosave.CreationId).ShouldNotBeNull();
        saved.Creature.Nodes[0].Position.ShouldBe(new Vector2D(5, 5));
        saved.Grid.ShouldBe(new BuildGridDef(100, snap: true));
    }

    [Fact]
    public void ShouldDiscardOnLeave_OnlyForANewCreationWithNothingDrawn()
    {
        var empty = new CreationDef(Guid.NewGuid(), "Untitled Creation", new CreatureDef([], [], []));

        Open(empty, openedAsNew: true).Autosave.ShouldDiscardOnLeave.ShouldBeTrue();
        Open(empty, openedAsNew: false).Autosave.ShouldDiscardOnLeave.ShouldBeFalse();

        var (_, build, drawnOn) = Open(empty, openedAsNew: true);
        build.PlaceNode(new Vector2D(0, 0));
        drawnOn.ShouldDiscardOnLeave.ShouldBeFalse();
    }

    [Fact]
    public void Dispose_StopsWatchingEdits()
    {
        var (_, build, autosave) = Open(TwoNodeCreation());
        autosave.Dispose();

        build.MoveNode(1, new Vector2D(5, 5));
        build.UseGrid(new BuildGridDef(100, snap: true));

        autosave.HasUnsavedEdits.ShouldBeFalse();
    }

    private static (InMemoryCreationRepository Repository, BuildViewModel Build, BuildAutosave Autosave) Open(
        CreationDef creation,
        bool openedAsNew = false)
    {
        var repository = new InMemoryCreationRepository();
        repository.Save(creation);
        var build = new BuildViewModel();
        build.LoadCreation(creation);
        var autosave = new BuildAutosave(
            build, new BuildEditWorkflow(new CreationUpdateCoordinator(repository)), creation.Id, openedAsNew);
        return (repository, build, autosave);
    }

    private static CreationDef TwoNodeCreation() => new(
        Guid.NewGuid(),
        "Worm",
        new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(20, 0))],
            [new BeamDef(101, 1, 2)],
            []));
}
