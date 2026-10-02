using NodeRunner.App.Repositories;
using NodeRunner.App.Services;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class CreationsPresentationViewModelTests
{
    [Fact]
    public void Refresh_WithNoCreations_ShowsEmptyState()
    {
        var viewModel = new CreationsPresentationViewModel(new InMemoryCreationRepository());

        viewModel.Refresh();

        viewModel.Cards.ShouldBeEmpty();
        viewModel.HasCards.ShouldBeFalse();
        viewModel.EmptyText.ShouldBe("No saved Creations yet.");
        viewModel.HasError.ShouldBeFalse();
    }

    [Fact]
    public void Refresh_WithCreations_FormatsCardsFromRepository()
    {
        var repository = new InMemoryCreationRepository();
        var trained = CreateCreation("Walker", generation: 12, new TrainingRunDef(1844, 306, 120, MapIds.Flat));
        var untrained = new CreationDef(Guid.NewGuid(), "Draft", trained.Creature);
        repository.Save(trained);
        repository.Save(untrained);
        var viewModel = new CreationsPresentationViewModel(repository);

        viewModel.Refresh();

        viewModel.HasCards.ShouldBeTrue();
        var walker = viewModel.Cards.Single(card => card.Id == trained.Id);
        walker.Name.ShouldBe("Walker");
        walker.Creature.ShouldBe(trained.Creature);
        walker.SummaryText.ShouldBeEmpty();
        walker.ThumbnailText.ShouldBe("2 nodes · 1 beam · 1 sensor");
        walker.Training.ShouldBe(new CreationCardTraining("18.4", "3.1", "1.2", MapIds.Flat, "12 generations"));
        walker.CanOpen.ShouldBeTrue();
        walker.CanDuplicate.ShouldBeTrue();
        walker.CanDelete.ShouldBeTrue();

        var draft = viewModel.Cards.Single(card => card.Id == untrained.Id);
        draft.SummaryText.ShouldBe("Not trained yet. Tap to build.");
        draft.Training.ShouldBeNull();
    }

    [Fact]
    public void Refresh_WithOneGeneration_SaysGenerationInTheSingular()
    {
        var repository = new InMemoryCreationRepository();
        repository.Save(CreateCreation("Walker", generation: 1));
        var viewModel = new CreationsPresentationViewModel(repository);

        viewModel.Refresh();

        viewModel.Cards.Single().Training!.GenerationsText.ShouldBe("1 generation");
    }

    [Fact]
    public void Refresh_ListsCardsByName()
    {
        var repository = new ConfigurableCreationRepository(
            [CreateCreation("Worm", generation: 1), CreateCreation("Ant", generation: 1), CreateCreation("Spider", generation: 1)]);
        var viewModel = new CreationsPresentationViewModel(repository);

        viewModel.Refresh();

        viewModel.Cards.Select(card => card.Name).ShouldBe(["Ant", "Spider", "Worm"]);
    }

    [Fact]
    public void Refresh_WithCopiedExample_ShowsAnOrdinaryCreation()
    {
        var repository = new InMemoryCreationRepository();
        var copy = new ExampleCopyWorkflow(repository).Copy(CreationExamples.WormId);
        var viewModel = new CreationsPresentationViewModel(repository);

        viewModel.Refresh();

        var card = viewModel.Cards.Single();
        card.Id.ShouldBe(copy.Id);
        card.Name.ShouldBe("Worm");
        card.CanDuplicate.ShouldBeTrue();
        card.CanDelete.ShouldBeTrue();
    }

    [Fact]
    public void Refresh_WhenRepositoryThrowsRecoverableError_ShowsLoadError()
    {
        var viewModel = new CreationsPresentationViewModel(new ThrowingCreationRepository(new IOException("disk unavailable")));

        viewModel.Refresh();

        viewModel.Cards.ShouldBeEmpty();
        viewModel.HasError.ShouldBeTrue();
        viewModel.ErrorText.ShouldBe("Could not load Creations.");
        viewModel.LoadError.ShouldBeOfType<IOException>();
    }

    [Fact]
    public void Refresh_WhenEnumerationThrowsRecoverableError_PreservesLastGoodCards()
    {
        var repository = new ConfigurableCreationRepository([CreateCreation("Walker", generation: 3)]);
        var viewModel = new CreationsPresentationViewModel(repository);
        viewModel.Refresh();
        repository.Creations = new ThrowingAfterFirstCreationList(CreateCreation("Partial", generation: 4));

        viewModel.Refresh();

        viewModel.HasError.ShouldBeTrue();
        viewModel.LoadError.ShouldBeOfType<IOException>();
        viewModel.Cards.Single().Name.ShouldBe("Walker");
    }

    [Fact]
    public void Refresh_AfterRepositoryChanges_ReplacesCardsAndClearsPriorError()
    {
        var repository = new ConfigurableCreationRepository([CreateCreation("Walker", generation: 3)]);
        var viewModel = new CreationsPresentationViewModel(repository);
        viewModel.Refresh();
        repository.Exception = new IOException("temporary");
        viewModel.Refresh();
        repository.Exception = null;
        repository.Creations = [CreateCreation("Crawler", generation: 9)];

        viewModel.Refresh();

        viewModel.HasError.ShouldBeFalse();
        viewModel.LoadError.ShouldBeNull();
        viewModel.Cards.Single().Name.ShouldBe("Crawler");
        viewModel.Cards.Single().Training!.GenerationsText.ShouldBe("9 generations");
    }

    [Fact]
    public void Refresh_NotifiesObserversThatStateChanged()
    {
        var viewModel = new CreationsPresentationViewModel(new InMemoryCreationRepository());
        var propertyNames = new List<string?>();
        viewModel.PropertyChanged += (_, args) => propertyNames.Add(args.PropertyName);

        viewModel.Refresh();

        propertyNames.ShouldBe([null]);
    }

    [Fact]
    public void Refresh_WhenRepositoryThrowsProgrammingError_DoesNotHideIt()
    {
        var viewModel = new CreationsPresentationViewModel(new ThrowingCreationRepository(new InvalidOperationException("bug")));

        Should.Throw<InvalidOperationException>(viewModel.Refresh);
    }

    [Fact]
    public void RequestCommands_ForKnownCreation_ReturnIntents()
    {
        var repository = new InMemoryCreationRepository();
        var creation = CreateCreation("Walker", generation: 1);
        repository.Save(creation);
        var viewModel = new CreationsPresentationViewModel(repository);
        viewModel.Refresh();

        viewModel.RequestOpen(creation.Id).ShouldBe(new CreationCommandIntent(CreationCommandKind.Open, creation.Id));
        viewModel.RequestDuplicate(creation.Id).ShouldBe(new CreationCommandIntent(CreationCommandKind.Duplicate, creation.Id));
        viewModel.RequestDelete(creation.Id).ShouldBe(new CreationCommandIntent(CreationCommandKind.Delete, creation.Id));
    }

    [Fact]
    public void RequestCommands_ForUnknownCreation_ReturnNull()
    {
        var viewModel = new CreationsPresentationViewModel(new InMemoryCreationRepository());
        viewModel.Refresh();

        viewModel.RequestOpen(Guid.NewGuid()).ShouldBeNull();
        viewModel.RequestDuplicate(Guid.NewGuid()).ShouldBeNull();
        viewModel.RequestDelete(Guid.NewGuid()).ShouldBeNull();
    }

    [Fact]
    public void Constructor_WithNullRepository_Throws()
    {
        Should.Throw<ArgumentNullException>(() => new CreationsPresentationViewModel(null!));
    }

    private static CreationDef CreateCreation(string name, int generation, TrainingRunDef? bestRun = null)
    {
        return new CreationDef(
            Guid.NewGuid(),
            name,
            new CreatureDef(
                [new NodeDef(1, new Vector2D(0, 0), 1), new NodeDef(2, new Vector2D(2, 0), 1)],
                [new BeamDef(101, 1, 2)],
                [new SensorDef(201, 101, SensorKind.Accelerometer)]),
            TestTraining.State(generation, 1, bestRun ?? TestTraining.Run));
    }

    private sealed class ThrowingCreationRepository(Exception exception) : ICreationRepository
    {
        public IReadOnlyList<CreationDef> List() => throw exception;

        public CreationDef? Get(Guid id) => null;

        public void Save(CreationDef creation)
        {
        }

        public bool Delete(Guid id) => false;
    }

    private sealed class ConfigurableCreationRepository(IReadOnlyList<CreationDef> creations) : ICreationRepository
    {
        public IReadOnlyList<CreationDef> Creations { get; set; } = creations;

        public Exception? Exception { get; set; }

        public IReadOnlyList<CreationDef> List() => Exception is null ? Creations : throw Exception;

        public CreationDef? Get(Guid id) => Creations.FirstOrDefault(creation => creation.Id == id);

        public void Save(CreationDef creation)
        {
            Creations = [.. Creations, creation];
        }

        public bool Delete(Guid id)
        {
            var without = Creations.Where(creation => creation.Id != id).ToArray();
            var removed = without.Length != Creations.Count;
            Creations = without;
            return removed;
        }
    }

    private sealed class ThrowingAfterFirstCreationList(CreationDef first) : IReadOnlyList<CreationDef>
    {
        public int Count => 2;

        public CreationDef this[int index] => index == 0 ? first : throw new IOException("enumeration failed");

        public IEnumerator<CreationDef> GetEnumerator()
        {
            yield return first;
            throw new IOException("enumeration failed");
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
