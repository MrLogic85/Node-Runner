using NodeRunner.App.Repositories;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class CreationsPresentationViewModelTests
{
    [Fact]
    public void Refresh_ListsCardsByName_FormattedFromTheRepository()
    {
        var repository = new InMemoryCreationRepository();
        var trained = CreateCreation("Walker", generation: 12, new TrainingRunDef(1844, 306, 120, MapIds.Flat, frontDistance: 1910));
        var untrained = new CreationDef(Guid.NewGuid(), "Draft", trained.Creature);
        repository.Save(trained);
        repository.Save(untrained);
        repository.Save(CreateCreation("Ant", generation: 1));
        var viewModel = new CreationsPresentationViewModel(repository);
        var propertyNames = new List<string?>();
        viewModel.PropertyChanged += (_, args) => propertyNames.Add(args.PropertyName);

        viewModel.Refresh();

        propertyNames.ShouldBe([null]);
        viewModel.HasCards.ShouldBeTrue();
        viewModel.Cards.Select(card => card.Name).ShouldBe([UiText.AsWritten("Ant"), UiText.AsWritten("Draft"), UiText.AsWritten("Walker")]);
        viewModel.Cards[0].Training!.GenerationsText.ShouldBe(Generations(1));
        var walker = viewModel.Cards[2];
        walker.Id.ShouldBe(trained.Id);
        walker.Creature.ShouldBe(trained.Creature);
        walker.SummaryText.ShouldBeNull();
        walker.ThumbnailText.ShouldBe(UiText.Format(
            "{0} · {1} · {2}",
            UiText.Counted("{0} joint", "{0} joints", 2),
            UiText.Counted("{0} beam", "{0} beams", 1),
            UiText.Counted("{0} sensor", "{0} sensors", 1)));
        walker.Training.ShouldBe(new CreationCardTraining(Metre(19.1), Speed(3.1), Metre(1.2), MapIds.Flat, Generations(12)));
        (walker.CanOpen, walker.CanDuplicate, walker.CanDelete).ShouldBe((true, true, true));
        var draft = viewModel.Cards[1];
        draft.SummaryText.ShouldBe(UiText.Plain("Not trained yet. Tap to build."));
        draft.Training.ShouldBeNull();
    }

    [Fact]
    public void Refresh_ShowsARecoverableLoadError_KeepingTheLastGoodCards_UntilALoadSucceeds()
    {
        var repository = new ConfigurableCreationRepository([CreateCreation("Walker", generation: 3)]);
        var viewModel = new CreationsPresentationViewModel(repository);
        viewModel.HasCards.ShouldBeFalse();
        viewModel.Refresh();

        repository.Creations = new ThrowingAfterFirstCreationList(CreateCreation("Partial", generation: 4));
        viewModel.Refresh();
        var (enumerationError, cardsKept) = (viewModel.LoadError, viewModel.Cards.Single().Name);
        var diskError = new IOException("disk unavailable");
        repository.Exception = diskError;
        viewModel.Refresh();
        var listError = viewModel.LoadError;
        repository.Exception = null;
        repository.Creations = [CreateCreation("Crawler", generation: 9)];
        viewModel.Refresh();

        enumerationError.ShouldBeOfType<IOException>();
        cardsKept.ShouldBe(UiText.AsWritten("Walker"));
        listError.ShouldBeSameAs(diskError);
        viewModel.HasError.ShouldBeFalse();
        viewModel.LoadError.ShouldBeNull();
        viewModel.Cards.Single().Name.ShouldBe(UiText.AsWritten("Crawler"));
    }

    [Fact]
    public void Refresh_WhenRepositoryThrowsProgrammingError_DoesNotHideIt()
    {
        var viewModel = new CreationsPresentationViewModel(new ConfigurableCreationRepository([]) { Exception = new InvalidOperationException("bug") });

        Should.Throw<InvalidOperationException>(viewModel.Refresh);
    }

    [Fact]
    public void RequestCommands_ReturnIntentsForShownCreationsOnly()
    {
        var repository = new InMemoryCreationRepository();
        var creation = CreateCreation("Walker", generation: 1);
        repository.Save(creation);
        var viewModel = new CreationsPresentationViewModel(repository);
        viewModel.Refresh();

        viewModel.RequestOpen(creation.Id).ShouldBe(new CreationCommandIntent(CreationCommandKind.Open, creation.Id));
        viewModel.RequestDuplicate(creation.Id).ShouldBe(new CreationCommandIntent(CreationCommandKind.Duplicate, creation.Id));
        viewModel.RequestDelete(creation.Id).ShouldBe(new CreationCommandIntent(CreationCommandKind.Delete, creation.Id));
        viewModel.RequestOpen(Guid.NewGuid()).ShouldBeNull();
    }

    private static UiText Generations(int count) => UiText.Counted("{0} generation", "{0} generations", count);

    private static UiText Metre(double metres) => UiText.Format("{0} m", new FixedNumber(metres, 1));

    private static UiText Speed(double metresPerSecond) => UiText.Format("{0} m/s", new FixedNumber(metresPerSecond, 1));

    private static CreationDef CreateCreation(string name, int generation, TrainingRunDef? latest = null)
    {
        return new CreationDef(
            Guid.NewGuid(),
            name,
            new CreatureDef(
                [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(2, 0))],
                [new BeamDef(101, 1, 2)],
                [new SensorDef(201, 101, SensorKind.Accelerometer)]),
            TestTraining.State(generation, 1, latest ?? TestTraining.Run));
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
