using NodeRunner.App.Repositories;
using NodeRunner.App.Services;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class BuildAutosaveTests
{
    [Fact]
    public void Save_AfterAnEdit_WritesTheDrawing()
    {
        var creation = TwoNodeCreation();
        var (repository, build, autosave) = Open(creation);

        build.PlaceNode(new Vector2D(40, 0), 18);
        autosave.Save().ShouldBeTrue();

        repository.Get(creation.Id).ShouldNotBeNull().Creature.Nodes.Count.ShouldBe(3);
        autosave.HasUnsavedEdits.ShouldBeFalse();
    }

    [Fact]
    public void Save_WithoutAnEdit_DoesNotWrite()
    {
        var edits = Substitute.For<IBuildEditWorkflow>();
        var build = new BuildViewModel();
        var creation = TwoNodeCreation();
        build.LoadCreation(creation);
        using var autosave = new BuildAutosave(build, edits, creation.Id, openedAsNew: false);

        autosave.Save().ShouldBeTrue();

        edits.DidNotReceiveWithAnyArgs().PersistEdit(default, default!);
    }

    [Fact]
    public void Edit_RaisesChangedAndMarksTheDrawingUnsaved()
    {
        var (_, build, autosave) = Open(TwoNodeCreation());
        var changes = 0;
        autosave.Changed += (_, _) => changes++;

        build.MoveNode(1, new Vector2D(5, 5));

        changes.ShouldBe(1);
        autosave.HasUnsavedEdits.ShouldBeTrue();
    }

    [Fact]
    public void Save_WhenTheCreationIsGone_KeepsTheEditsUnsaved()
    {
        var creation = TwoNodeCreation();
        var (repository, build, autosave) = Open(creation);
        repository.Delete(creation.Id);

        build.MoveNode(1, new Vector2D(5, 5));
        autosave.Save().ShouldBeFalse();

        autosave.HasUnsavedEdits.ShouldBeTrue();
    }

    [Fact]
    public void Save_WhenWritingThrows_KeepsTheEditsUnsaved()
    {
        var edits = Substitute.For<IBuildEditWorkflow>();
        edits.PersistEdit(default, default!).ReturnsForAnyArgs(_ => throw new IOException("disk full"));
        var build = new BuildViewModel();
        var creation = TwoNodeCreation();
        build.LoadCreation(creation);
        using var autosave = new BuildAutosave(build, edits, creation.Id, openedAsNew: false);

        build.MoveNode(1, new Vector2D(5, 5));

        Should.Throw<IOException>(() => autosave.Save());
        autosave.HasUnsavedEdits.ShouldBeTrue();
    }

    [Fact]
    public void Save_OnALockedCreation_KeepsItsTraining()
    {
        var pair = new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0), 18), new NodeDef(2, new Vector2D(20, 0), 18)],
            [],
            [],
            [new PistonDef(3, 1, 2)]);
        var training = TestTraining.StateFor(pair, 4);
        var trained = new CreationDef(Guid.NewGuid(), "Pair", pair, training);
        var (repository, build, autosave) = Open(trained);

        build.MoveNode(1, new Vector2D(5, 5));
        autosave.Save();

        var saved = repository.Get(trained.Id).ShouldNotBeNull();
        saved.Creature.Nodes[0].Position.ShouldBe(new Vector2D(5, 5));
        saved.Training.ShouldNotBeNull();
        saved.Training.Generation.ShouldBe(training.Generation);
        saved.Training.Best.ShouldBe(training.Best);
        saved.Training.Brain.Neurons.ShouldBe(training.Brain.Neurons);
        saved.Training.Brain.Connections.ShouldBe(training.Brain.Connections);
    }

    [Fact]
    public void Save_SurvivesARestart()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"node-runner-{Guid.NewGuid():N}");
        try
        {
            var creation = TwoNodeCreation();
            var repository = new FileCreationRepository(new TestStorageLocation(directory));
            repository.Save(creation);
            var build = new BuildViewModel();
            build.LoadCreation(creation);
            using (var autosave = new BuildAutosave(
                build, new BuildEditWorkflow(new CreationUpdateCoordinator(repository)), creation.Id, openedAsNew: false))
            {
                build.MoveNode(2, new Vector2D(60, 0));
                autosave.Save();
            }

            var reopened = new FileCreationRepository(new TestStorageLocation(directory)).Get(creation.Id);

            reopened.ShouldNotBeNull().Creature.Nodes[1].Position.ShouldBe(new Vector2D(60, 0));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void ShouldDiscardOnLeave_OnlyForANewCreationWithNothingDrawn()
    {
        var empty = new CreationDef(Guid.NewGuid(), "Untitled Creation", new CreatureDef([], [], []));

        Open(empty, openedAsNew: true).Autosave.ShouldDiscardOnLeave.ShouldBeTrue();
        Open(empty, openedAsNew: false).Autosave.ShouldDiscardOnLeave.ShouldBeFalse();

        var (_, build, drawnOn) = Open(empty, openedAsNew: true);
        build.PlaceNode(new Vector2D(0, 0), 18);
        drawnOn.ShouldDiscardOnLeave.ShouldBeFalse();
    }

    [Fact]
    public void Dispose_StopsWatchingEdits()
    {
        var (_, build, autosave) = Open(TwoNodeCreation());
        autosave.Dispose();

        build.MoveNode(1, new Vector2D(5, 5));

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
            [new NodeDef(1, new Vector2D(0, 0), 18), new NodeDef(2, new Vector2D(20, 0), 18)],
            [new BeamDef(101, 1, 2)],
            []));
}
