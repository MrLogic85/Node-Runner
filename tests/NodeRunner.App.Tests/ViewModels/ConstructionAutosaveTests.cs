using NodeRunner.App.Repositories;
using NodeRunner.App.Services;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class ConstructionAutosaveTests
{
    [Fact]
    public void Save_AfterAnEdit_WritesTheDrawing()
    {
        var creation = TwoNodeCreation();
        var (repository, construction, autosave) = Open(creation);

        construction.PlaceNode(new Vector2D(40, 0), 18);
        autosave.Save().ShouldBeTrue();

        repository.Get(creation.Id).ShouldNotBeNull().Creature.Nodes.Count.ShouldBe(3);
        autosave.HasUnsavedEdits.ShouldBeFalse();
    }

    [Fact]
    public void Save_WithoutAnEdit_DoesNotWrite()
    {
        var edits = Substitute.For<IConstructionEditWorkflow>();
        var construction = new ConstructionViewModel();
        var creation = TwoNodeCreation();
        construction.LoadCreation(creation);
        using var autosave = new ConstructionAutosave(construction, edits, creation.Id, openedAsNew: false);

        autosave.Save().ShouldBeTrue();

        edits.DidNotReceiveWithAnyArgs().PersistEdit(default, default!, default!, default);
    }

    [Fact]
    public void Edit_RaisesChangedAndMarksTheDrawingUnsaved()
    {
        var (_, construction, autosave) = Open(TwoNodeCreation());
        var changes = 0;
        autosave.Changed += (_, _) => changes++;

        construction.MoveNode(0, new Vector2D(5, 5));
        construction.SetBrainShape(new BrainShapeDef(2, 6));

        changes.ShouldBe(2);
        autosave.HasUnsavedEdits.ShouldBeTrue();
    }

    [Fact]
    public void Save_WhenTheCreationIsGone_KeepsTheEditsUnsaved()
    {
        var creation = TwoNodeCreation();
        var (repository, construction, autosave) = Open(creation);
        repository.Delete(creation.Id);

        construction.MoveNode(0, new Vector2D(5, 5));
        autosave.Save().ShouldBeFalse();

        autosave.HasUnsavedEdits.ShouldBeTrue();
    }

    [Fact]
    public void Save_WhenWritingThrows_KeepsTheEditsUnsaved()
    {
        var edits = Substitute.For<IConstructionEditWorkflow>();
        edits.PersistEdit(default, default!, default!, default).ReturnsForAnyArgs(_ => throw new IOException("disk full"));
        var construction = new ConstructionViewModel();
        var creation = TwoNodeCreation();
        construction.LoadCreation(creation);
        using var autosave = new ConstructionAutosave(construction, edits, creation.Id, openedAsNew: false);

        construction.MoveNode(0, new Vector2D(5, 5));

        Should.Throw<IOException>(() => autosave.Save());
        autosave.HasUnsavedEdits.ShouldBeTrue();
    }

    [Fact]
    public void Save_AfterABrainShapeChange_StoresTheNewShape()
    {
        var creation = TwoNodeCreation();
        var (repository, construction, autosave) = Open(creation);

        construction.SetBrainShape(new BrainShapeDef(2, 6));
        autosave.Save();

        repository.Get(creation.Id).ShouldNotBeNull().BrainShape.ShouldBe(new BrainShapeDef(2, 6));
    }

    [Fact]
    public void Save_OnALockedCreation_KeepsItsTrainingAndBrainShape()
    {
        var training = new TrainingStateDef([6, 3, 1], Enumerable.Repeat(0.1, 25).ToArray(), 4, "Tanh");
        var drawn = TwoNodeCreation();
        var trained = new CreationDef(drawn.Id, drawn.Name, drawn.Creature, new BrainShapeDef(1, 3), training);
        var (repository, construction, autosave) = Open(trained);

        construction.MoveNode(0, new Vector2D(5, 5));
        autosave.Save();

        var saved = repository.Get(trained.Id).ShouldNotBeNull();
        saved.Creature.Nodes[0].Position.ShouldBe(new Vector2D(5, 5));
        saved.Training.ShouldBe(training);
        saved.BrainShape.ShouldBe(new BrainShapeDef(1, 3));
    }

    [Fact]
    public void Save_SurvivesARestart()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"node-runner-{Guid.NewGuid():N}");
        try
        {
            var creation = TwoNodeCreation();
            var repository = new FileCreationRepository(new TempStorageLocation(directory));
            repository.Save(creation);
            var construction = new ConstructionViewModel();
            construction.LoadCreation(creation);
            using (var autosave = new ConstructionAutosave(
                construction, new ConstructionEditWorkflow(new CreationUpdateCoordinator(repository)), creation.Id, openedAsNew: false))
            {
                construction.MoveNode(1, new Vector2D(60, 0));
                autosave.Save();
            }

            var reopened = new FileCreationRepository(new TempStorageLocation(directory)).Get(creation.Id);

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

        var (_, construction, drawnOn) = Open(empty, openedAsNew: true);
        construction.PlaceNode(new Vector2D(0, 0), 18);
        drawnOn.ShouldDiscardOnLeave.ShouldBeFalse();
    }

    [Fact]
    public void Dispose_StopsWatchingEdits()
    {
        var (_, construction, autosave) = Open(TwoNodeCreation());
        autosave.Dispose();

        construction.MoveNode(0, new Vector2D(5, 5));

        autosave.HasUnsavedEdits.ShouldBeFalse();
    }

    private static (InMemoryCreationRepository Repository, ConstructionViewModel Construction, ConstructionAutosave Autosave) Open(
        CreationDef creation,
        bool openedAsNew = false)
    {
        var repository = new InMemoryCreationRepository();
        repository.Save(creation);
        var construction = new ConstructionViewModel();
        construction.LoadCreation(creation);
        var autosave = new ConstructionAutosave(
            construction, new ConstructionEditWorkflow(new CreationUpdateCoordinator(repository)), creation.Id, openedAsNew);
        return (repository, construction, autosave);
    }

    private static CreationDef TwoNodeCreation() => new(
        Guid.NewGuid(),
        "Worm",
        new CreatureDef(
            [new NodeDef(new Vector2D(0, 0), 18), new NodeDef(new Vector2D(20, 0), 18)],
            [new BeamDef(0, 1)],
            []));

    private sealed class TempStorageLocation(string directoryPath) : IStorageLocation
    {
        public string DirectoryPath { get; } = directoryPath;
    }
}
