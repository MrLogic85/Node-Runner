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
        var trained = CreateCreation("Walker", generation: 12);
        var untrained = new CreationDef(Guid.NewGuid(), "Draft", trained.Creature);
        repository.Save(trained);
        repository.Save(untrained);
        var viewModel = new CreationsPresentationViewModel(repository);

        viewModel.Refresh();

        viewModel.HasCards.ShouldBeTrue();
        var walker = viewModel.Cards.Single(card => card.Id == trained.Id);
        walker.Name.ShouldBe("Walker");
        walker.Creature.ShouldBe(trained.Creature);
        walker.SummaryText.ShouldBe("Generation 12 · trained brain");
        walker.NoteText.ShouldBe("Duplicate copies training");
        walker.ThumbnailText.ShouldBe("2 nodes · 1 beam · 1 core");
        walker.SavedStateText.ShouldBe("Saved training · generation 12");
        walker.UnlockCreditText.ShouldBe(string.Empty);
        walker.AchievementProgress.ShouldBe(0.6f);
        walker.AchievementProgressText.ShouldBe("Achievement progress");
        walker.IsExample.ShouldBeFalse();
        walker.CanOpen.ShouldBeTrue();
        walker.CanEdit.ShouldBeTrue();
        walker.CanDuplicate.ShouldBeTrue();
        walker.CanDelete.ShouldBeTrue();

        var draft = viewModel.Cards.Single(card => card.Id == untrained.Id);
        draft.SummaryText.ShouldBe("Ready to train");
        draft.NoteText.ShouldBe("Train or edit");
        draft.SavedStateText.ShouldBe("Untrained Creation");
    }

    [Fact]
    public void Refresh_WithUnlockAttribution_MarksWinningCreation()
    {
        var repository = new InMemoryCreationRepository();
        var credited = CreateCreation("Unlocker", generation: 12);
        var other = CreateCreation("Other", generation: 20);
        repository.Save(credited);
        repository.Save(other);
        var progression = new InMemoryProgressionRepository();
        progression.Save(new ProgressionDef(true, 12, credited.Id));
        var viewModel = new CreationsPresentationViewModel(repository, progression);

        viewModel.Refresh();

        viewModel.Cards.Single(card => card.Id == credited.Id)
            .UnlockCreditText.ShouldBe("Earned extra core unlock · generation 12");
        viewModel.Cards.Single(card => card.Id == credited.Id)
            .AchievementProgress.ShouldBe(1f);
        viewModel.HasAchievementCue.ShouldBeTrue();
        viewModel.Cards.Single(card => card.Id == other.Id)
            .UnlockCreditText.ShouldBe(string.Empty);
    }

    [Fact]
    public void Refresh_WithStarterExample_MarksExampleLocked()
    {
        var repository = new InMemoryCreationRepository();
        repository.Save(DefaultCreationTemplates.CreateStarterWorm());
        var viewModel = new CreationsPresentationViewModel(repository);

        viewModel.Refresh();

        var example = viewModel.Cards.Single();
        example.IsExample.ShouldBeTrue();
        example.NoteText.ShouldBe("Example");
        example.CanDelete.ShouldBeFalse();
        viewModel.CanRestoreExample.ShouldBeFalse();
    }

    [Fact]
    public void Refresh_WhenStarterExampleMissing_AllowsRestore()
    {
        var repository = new InMemoryCreationRepository();
        repository.Save(CreateCreation("Player", generation: 1));
        var viewModel = new CreationsPresentationViewModel(repository);

        viewModel.Refresh();

        viewModel.CanRestoreExample.ShouldBeTrue();
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
        viewModel.Cards.Single().SummaryText.ShouldBe("Generation 9 · trained brain");
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
        viewModel.RequestEdit(creation.Id).ShouldBe(new CreationCommandIntent(CreationCommandKind.Edit, creation.Id));
        viewModel.RequestDuplicate(creation.Id).ShouldBe(new CreationCommandIntent(CreationCommandKind.Duplicate, creation.Id));
        viewModel.RequestDelete(creation.Id).ShouldBe(new CreationCommandIntent(CreationCommandKind.Delete, creation.Id));
    }

    [Fact]
    public void RequestCommands_ForUnknownCreation_ReturnNull()
    {
        var viewModel = new CreationsPresentationViewModel(new InMemoryCreationRepository());
        viewModel.Refresh();

        viewModel.RequestOpen(Guid.NewGuid()).ShouldBeNull();
        viewModel.RequestEdit(Guid.NewGuid()).ShouldBeNull();
        viewModel.RequestDuplicate(Guid.NewGuid()).ShouldBeNull();
        viewModel.RequestDelete(Guid.NewGuid()).ShouldBeNull();
    }

    [Fact]
    public void Constructor_WithNullRepository_Throws()
    {
        Should.Throw<ArgumentNullException>(() => new CreationsPresentationViewModel(null!));
    }

    private static CreationDef CreateCreation(string name, int generation)
    {
        return new CreationDef(
            Guid.NewGuid(),
            name,
            new CreatureDef(
                [new NodeDef(new Vector2D(0, 0), 1), new NodeDef(new Vector2D(2, 0), 1)],
                [new BeamDef(0, 1)],
                [new CoreDef(0)]),
            new TrainingStateDef([2, 1], [0.1, -0.2, 0.3], generation, "Tanh"));
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
